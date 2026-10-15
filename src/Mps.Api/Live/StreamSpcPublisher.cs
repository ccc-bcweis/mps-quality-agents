// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Collections.Concurrent;

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
    private static partial void TicketOpened(ILogger logger, string toolId, string parameter);

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

    public void Clear() => _started.Clear();

    public async Task PublishAsync(CancellationToken cancellationToken)
    {
        var samples = Capture();
        if (samples.Count == 0)
        {
            return;
        }

        await using var scope = _scopes.CreateAsyncScope();
        var spc = scope.ServiceProvider.GetRequiredService<SpcAlertService>();
        foreach (var sample in samples)
        {
            var outcome = await spc.RaiseAsync(sample, cancellationToken).ConfigureAwait(false);
            if (outcome.Incident is null)
            {
                continue;
            }

            if (outcome.Ticket is not null)
            {
                TicketOpened(_logger, sample.ToolId.ToString(), sample.Parameter);
            }

            // Start this investigation before the next tool overwrites the shared step pointer.
            // A printout can open the ticket first. The stream still starts the investigation for that case.
            if (!_started.TryAdd(outcome.Incident.Id.Value, 0))
            {
                continue;
            }

            await InvestigateAsync(outcome.Incident.Id).ConfigureAwait(false);
        }
    }

    public async Task DrainAsync()
    {
        while (_pending.TryDequeue(out var task))
        {
            await task.ConfigureAwait(false);
        }
    }

    private List<SpcSample> Capture()
    {
        var samples = new List<SpcSample>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fault in _plant.Runtime.Simulator.ActiveFaults)
        {
            var chamber = string.IsNullOrWhiteSpace(fault.ChamberId) ? _plant.DefaultChamber(fault.ToolId) : fault.ChamberId;
            var key = fault.ToolId + "|" + chamber + "|" + fault.Parameter;
            if (!seen.Add(key))
            {
                continue;
            }

            var points = _plant.Log.Window(fault.ToolId, chamber, fault.Parameter)
                .Where(point => point.At >= fault.Onset)
                .ToArray();
            if (points.Length == 0)
            {
                continue;
            }

            var zPoints = new List<TelemetryPoint>(points.Length);
            foreach (var point in points)
            {
                var window = _plant.WindowAt(fault.ToolId, fault.Parameter, point.At);
                if (window is null)
                {
                    continue;
                }

                var sigma = Math.Max(window.Sigma, 1e-6);
                zPoints.Add(new TelemetryPoint(point.At, (point.Value - window.Nominal) / sigma));
            }

            if (zPoints.Count == 0)
            {
                continue;
            }

            samples.Add(new SpcSample(
                new ToolId(fault.ToolId),
                chamber,
                fault.Parameter,
                new ControlBaseline(0d, 1d, null, null),
                zPoints));
        }

        return samples;
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
