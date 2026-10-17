// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Collections.Concurrent;

using Mps.Application.Bus;
using Mps.Application.Errors;
using Mps.Application.Lifecycle;
using Mps.Application.Spc;
using Mps.Domain.Identity;
using Mps.Domain.Telemetry;
using Mps.Simulator.Telemetry;

namespace Mps.Api.Live;

/// <summary>
/// Runs Western Electric rules on series that have an injected fault. Alerts and tickets come from those points.
/// A new ticket starts an investigation without blocking the next tick.
/// </summary>
public sealed partial class StreamSpcPublisher
{
    [LoggerMessage(EventId = 7101, Level = LogLevel.Information, Message = "SPC opened a ticket for {ToolId} {Parameter} from the live stream.")]
    private static partial void TicketOpened(ILogger logger, ToolId toolId, string parameter);

    [LoggerMessage(EventId = 7102, Level = LogLevel.Information, Message = "Investigation for {IncidentId} was not started.")]
    private static partial void InvestigationSkipped(ILogger logger, Exception error, Guid incidentId);

    [LoggerMessage(EventId = 7103, Level = LogLevel.Warning, Message = "Investigation for {IncidentId} failed.")]
    private static partial void InvestigationFailed(ILogger logger, Exception error, Guid incidentId);

    private readonly PlantSimulation _plant;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<StreamSpcPublisher> _logger;
    private readonly ConcurrentDictionary<Guid, byte> _started = new();
    private readonly ConcurrentQueue<Task> _pending = new();

    public StreamSpcPublisher(PlantSimulation plant, IServiceScopeFactory scopes, ILogger<StreamSpcPublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(plant);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(logger);
        _plant = plant;
        _scopes = scopes;
        _logger = logger;
    }

    public void Clear()
    {
        _started.Clear();
        _watch.Clear();
        _checked.Clear();
    }

    public async Task PublishAsync(CancellationToken cancellationToken)
    {
        var samples = Capture();
        samples.AddRange(CaptureLimitBreaks(samples));
        var recovering = CaptureRecovering();
        if (samples.Count == 0 && recovering.Count == 0)
        {
            return;
        }

        await using var scope = _scopes.CreateAsyncScope();
        var spc = scope.ServiceProvider.GetRequiredService<SpcAlertService>();
        foreach (var sample in recovering)
        {
            await spc.ClearRecoveredAsync(sample, cancellationToken).ConfigureAwait(false);
        }

        foreach (var sample in samples)
        {
            var outcome = await spc.RaiseAsync(sample, cancellationToken).ConfigureAwait(false);
            if (outcome.Incident is null)
            {
                continue;
            }

            if (outcome.Ticket is not null)
            {
                TicketOpened(_logger, sample.ToolId, sample.Parameter);
            }

            // Start this investigation before the next tool overwrites the shared step pointer.
            // A printout can open the ticket first. The stream still starts the investigation for that case.
            if (!_started.TryAdd(outcome.Incident.Id.Value, 0))
            {
                continue;
            }

            // Run it off the stream: the investigation may wait for more readings to classify the shape,
            // and those readings come from this stream.
            // Capture the ticket it hangs off now, before the next tool moves the shared step pointer.
            var incidentId = outcome.Incident.Id;
            var hints = scope.ServiceProvider.GetService<ICascadeDirectory>();
            var correlation = outcome.Incident.CorrelationId;
            var parent = hints?.FindStep(correlation, "ticket") ?? hints?.FindStep(correlation, "alert");
            _pending.Enqueue(InvestigationParent.Run(parent, () => InvestigateAsync(incidentId)));
        }
    }

    public async Task DrainAsync()
    {
        while (_pending.TryDequeue(out var task))
        {
            await task.ConfigureAwait(false);
        }
    }

    /// <summary>Signals whose fault was removed. Watched for a while so their alerts can clear; never re-raised.</summary>
    private readonly ConcurrentDictionary<string, (string ToolId, string Chamber, string Parameter, DateTimeOffset Onset, DateTimeOffset LastSeen)> _watch = new(StringComparer.Ordinal);

    private static TimeSpan WatchFor => Mps.Application.Spc.SpcRuleSettings.Shared.RecoveryWatch;

    private List<SpcSample> Capture()
    {
        var samples = new List<SpcSample>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var now = _plant.SimulatedNow;
        foreach (var fault in _plant.Runtime.Simulator.ActiveFaults)
        {
            var chamber = string.IsNullOrWhiteSpace(fault.ChamberId) ? _plant.DefaultChamber(fault.ToolId) : fault.ChamberId;
            var key = fault.ToolId + "|" + chamber + "|" + fault.Parameter;
            if (!seen.Add(key))
            {
                continue;
            }

            _watch[key] = (fault.ToolId, chamber, fault.Parameter, fault.Onset, now);
            var sample = BuildSample(fault.ToolId, chamber, fault.Parameter, fault.Onset);
            if (sample is not null)
            {
                samples.Add(sample);
            }
        }

        return samples;
    }

    /// <summary>
    /// Every periodic series is checked against its limits, faulted or not, so nothing is charted with limits and
    /// never checked. Only a reading beyond <see cref="SpcCoverage.LimitSigma"/> sigma or outside its window is sent
    /// on to the rules engine; quiet series cost nothing.
    /// </summary>
    private List<SpcSample> CaptureLimitBreaks(List<SpcSample> faulted)
    {
        var samples = new List<SpcSample>();
        var taken = new HashSet<string>(
            faulted.Select(sample => sample.ToolId + "|" + sample.ChamberId + "|" + sample.Parameter),
            StringComparer.Ordinal);
        var now = _plant.SimulatedNow;
        foreach (var reading in _plant.Log.Snapshot())
        {
            if (!SpcCoverage.IsEvaluated(reading.Parameter, reading.SensorId))
            {
                continue;
            }

            var key = reading.ToolId + "|" + reading.ChamberId + "|" + reading.Parameter;
            if (taken.Contains(key))
            {
                continue;
            }

            // Every point that arrived since the last check, not only the newest: a single breaking sample
            // (a printout, or a spike followed by a normal reading in the same tick) must still reach the rules.
            var checkedTo = _checked.TryGetValue(key, out var last) ? last : DateTimeOffset.MinValue;
            var fresh = _plant.Log.Window(reading.ToolId, reading.ChamberId, reading.Parameter, SpcCoverage.RecentReadings)
                .Where(point => point.At > checkedTo)
                .ToArray();
            _checked[key] = reading.At;
            if (!fresh.Any(point => Breaks(reading.ToolId, reading.Parameter, point)))
            {
                continue;
            }

            var recent = _plant.Log.Window(reading.ToolId, reading.ChamberId, reading.Parameter, SpcCoverage.RecentReadings);
            var since = recent.Count > 0 ? recent[0].At : reading.At;
            var sample = BuildSample(reading.ToolId, reading.ChamberId, reading.Parameter, since);
            if (sample is not null)
            {
                _watch[key] = (reading.ToolId, reading.ChamberId, reading.Parameter, since, now);
                samples.Add(sample);
            }
        }

        return samples;
    }

    private readonly ConcurrentDictionary<string, DateTimeOffset> _checked = new(StringComparer.Ordinal);

    private bool Breaks(string toolId, string parameter, LivePoint point)
    {
        var window = _plant.WindowAt(toolId, parameter, point.At);
        if (window is null)
        {
            return false;
        }

        var z = (point.Value - window.Nominal) / Math.Max(window.Sigma, 1e-6);
        return Math.Abs(z) > SpcCoverage.LimitSigma || point.Value < window.Minimum || point.Value > window.Maximum;
    }

    /// <summary>Samples for signals whose fault is gone, used only to clear their alerts.</summary>
    private List<SpcSample> CaptureRecovering()
    {
        var samples = new List<SpcSample>();
        var active = new HashSet<string>(
            _plant.Runtime.Simulator.ActiveFaults.Select(fault =>
                fault.ToolId + "|" + (string.IsNullOrWhiteSpace(fault.ChamberId) ? _plant.DefaultChamber(fault.ToolId) : fault.ChamberId) + "|" + fault.Parameter),
            StringComparer.Ordinal);
        var now = _plant.SimulatedNow;
        foreach (var pair in _watch)
        {
            if (active.Contains(pair.Key))
            {
                continue;
            }

            if (now - pair.Value.LastSeen > WatchFor)
            {
                _watch.TryRemove(pair.Key, out _);
                continue;
            }

            var sample = BuildSample(pair.Value.ToolId, pair.Value.Chamber, pair.Value.Parameter, pair.Value.Onset);
            if (sample is not null)
            {
                samples.Add(sample);
            }
        }

        return samples;
    }

    private SpcSample? BuildSample(string toolId, string chamber, string parameter, DateTimeOffset onset)
    {
        var points = _plant.Log.Window(toolId, chamber, parameter)
            .Where(point => point.At >= onset)
            .ToArray();
        if (points.Length == 0)
        {
            return null;
        }

        var zPoints = new List<TelemetryPoint>(points.Length);
        foreach (var point in points)
        {
            var window = _plant.WindowAt(toolId, parameter, point.At);
            if (window is null)
            {
                continue;
            }

            var sigma = Math.Max(window.Sigma, 1e-6);
            zPoints.Add(new TelemetryPoint(point.At, (point.Value - window.Nominal) / sigma));
        }

        if (zPoints.Count == 0)
        {
            return null;
        }

        var lastWindow = _plant.WindowAt(toolId, parameter, points[^1].At);
        return new SpcSample(
            new ToolId(toolId),
            chamber,
            parameter,
            ControlBaseline.StandardScore,
            zPoints)
        {
            Display = lastWindow is null ? null : new SpcDisplay(lastWindow.Nominal, Math.Max(lastWindow.Sigma, 1e-6), lastWindow.Unit, _plant.FamilyLabel(toolId)),
        };
    }

    private async Task InvestigateAsync(IncidentId incidentId)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var investigations = scope.ServiceProvider.GetRequiredService<IInvestigationService>();
            await investigations.StartAsync(incidentId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is LifecycleConflictException or ResourceNotFoundException or OperationCanceledException)
        {
            InvestigationSkipped(_logger, ex, incidentId.Value);
        }
        catch (Exception ex)
        {
            InvestigationFailed(_logger, ex, incidentId.Value);
        }
    }
}
