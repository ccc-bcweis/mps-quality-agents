// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Collections.Concurrent;

using Mps.Api.Live;
using Mps.Application.Bus;
using Mps.Application.Errors;
using Mps.Application.Lifecycle;
using Mps.Application.Persistence;
using Mps.Application.Spc;
using Mps.Bus;
using Mps.Domain.Events;
using Mps.Domain.Identity;
using Mps.Simulator.Fleet;

namespace Mps.Api.Bus;

/// <summary>
/// Evaluates every telemetry reading with the SPC rule engine. Alerts change only when that state changes.
/// One investigation runs per incident. Shadow mode counts the same decisions and writes nothing.
/// </summary>
public sealed partial class SpcRuleSubscriber : IHostedService, IDisposable
{
    [LoggerMessage(EventId = 7101, Level = LogLevel.Information, Message = "SPC opened a ticket for {ToolId} {Parameter} from the live stream.")]
    private static partial void TicketOpened(ILogger logger, string toolId, string parameter);

    [LoggerMessage(EventId = 7102, Level = LogLevel.Information, Message = "Investigation for {IncidentId} was not started.")]
    private static partial void InvestigationSkipped(ILogger logger, Exception error, Guid incidentId);

    [LoggerMessage(EventId = 7103, Level = LogLevel.Warning, Message = "Investigation for {IncidentId} failed.")]
    private static partial void InvestigationFailed(ILogger logger, Exception error, Guid incidentId);

    [LoggerMessage(EventId = 7104, Level = LogLevel.Warning, Message = "An investigation did not stop within {Seconds} s of the reset cancelling it; the reset goes on without it.")]
    private static partial void DrainTimedOut(ILogger logger, double seconds);

    [LoggerMessage(EventId = 7105, Level = LogLevel.Information, Message = "SPC shadow would {Action} alert-tier {Rule} on {ToolId} {Parameter}.")]
    private static partial void ShadowAlert(ILogger logger, SpcAction action, string rule, string toolId, string parameter);

    [LoggerMessage(EventId = 7106, Level = LogLevel.Information, Message = "SPC raised {ToolId} {Parameter} and did not open a ticket; the hourly cap is {Cap}.")]
    private static partial void TicketCapped(ILogger logger, string toolId, string parameter, int cap);

    [LoggerMessage(EventId = 7107, Level = LogLevel.Debug, Message = "SPC shadow would {Action} watch-tier {Rule} on {ToolId} {Parameter}.")]
    private static partial void ShadowWatch(ILogger logger, SpcAction action, string rule, string toolId, string parameter);

    private readonly IEventBus _bus;
    private readonly PlantSimulation _plant;
    private readonly IServiceScopeFactory _scopes;
    private readonly SpcRuleEngine _engine;
    private readonly SpcTicketCap _cap;
    private readonly SpcShadowLedger _shadow;
    private readonly SpcOptions _options;
    private readonly ICascadeDirectory _cascade;
    private readonly ILogger<SpcRuleSubscriber> _logger;
    private readonly ConcurrentDictionary<Guid, byte> _started = new();
    private readonly ConcurrentQueue<Task> _pending = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<IDisposable> _subscriptions = [];
    private CancellationTokenSource _runs = new();
    private int _disposed;

    public SpcRuleSubscriber(
        IEventBus bus,
        PlantSimulation plant,
        IServiceScopeFactory scopes,
        SpcRuleEngine engine,
        SpcTicketCap cap,
        SpcShadowLedger shadow,
        SpcOptions options,
        ICascadeDirectory cascade,
        ILogger<SpcRuleSubscriber> logger)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(plant);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(cap);
        ArgumentNullException.ThrowIfNull(shadow);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(cascade);
        ArgumentNullException.ThrowIfNull(logger);
        _bus = bus;
        _plant = plant;
        _scopes = scopes;
        _engine = engine;
        _cap = cap;
        _shadow = shadow;
        _options = options;
        _cascade = cascade;
        _logger = logger;
    }

    public SpcShadowLedger Shadow => _shadow;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await RebuildFromStoreAsync(cancellationToken).ConfigureAwait(false);
        _subscriptions.Add(_bus.Subscribe(EventTypes.TelemetryReading, OnReading));
        _subscriptions.Add(_bus.Subscribe(EventTypes.ResetHard, OnPlantReset));
        _subscriptions.Add(_bus.Subscribe(EventTypes.ResetSoft, OnPlantReset));
        _subscriptions.Add(_bus.Subscribe(EventTypes.ScenarioStopped, OnPlantReset));
        _subscriptions.Add(_bus.Subscribe(EventTypes.FaultCleared, OnPlantReset));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        IDisposable[] snapshot;
        lock (_subscriptions)
        {
            snapshot = [.. _subscriptions];
            _subscriptions.Clear();
        }

        foreach (var subscription in snapshot)
        {
            subscription.Dispose();
        }

        _gate.Dispose();
        Volatile.Read(ref _runs).Dispose();
    }

    /// <summary>Drops investigation deduplication so the next incident can start. Does not clear alerts or rule state.</summary>
    public void Clear() => _started.Clear();

    /// <summary>A hard or soft reset restarts the plant clock, so the hourly ticket count starts over.</summary>
    public void ResetTicketCap() => _cap.Reset();

    public Task DrainAsync() => DrainAsync(Timeout.InfiniteTimeSpan);

    public Task<bool> CancelAndDrainAsync(TimeSpan limit)
    {
        var previous = Interlocked.Exchange(ref _runs, new CancellationTokenSource());
        previous.Cancel();
        return DrainAsync(limit);
    }

    public async Task<bool> DrainAsync(TimeSpan limit)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (_pending.TryDequeue(out var task))
        {
            var left = limit == Timeout.InfiniteTimeSpan ? limit : TimeSpan.FromTicks(Math.Max(0, (limit - watch.Elapsed).Ticks));
            try
            {
                await task.WaitAsync(left).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                DrainTimedOut(_logger, limit.TotalSeconds);
                return false;
            }
        }

        return true;
    }

    /// <summary>Loads active alerts into the engine and empties every ring.</summary>
    public async Task RebuildFromStoreAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var alerts = await scope.ServiceProvider.GetRequiredService<IAlertLog>().ListAsync(null, cancellationToken).ConfigureAwait(false);
            var seeds = new List<SpcSeriesSeed>();
            foreach (var alert in alerts)
            {
                if (!alert.IsActive)
                {
                    continue;
                }

                seeds.Add(new SpcSeriesSeed(
                    alert.ToolId.ToString(),
                    alert.ChamberId,
                    alert.Parameter,
                    alert.Severity,
                    alert.Rule,
                    alert.CorrelationId,
                    alert.LastSeenAt));
            }

            _engine.Rebuild(seeds);
        }
        finally
        {
            _gate.Release();
        }
    }

    private ValueTask OnPlantReset(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        _ = envelope;
        return new ValueTask(RebuildFromStoreAsync(cancellationToken));
    }

    private async ValueTask OnReading(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        var parameter = PayloadReader.String(envelope.Payload, "parameter");
        var value = PayloadReader.Number(envelope.Payload, "value");
        if (parameter is null || value is null || string.IsNullOrWhiteSpace(envelope.ToolId))
        {
            return;
        }

        var kind = PayloadReader.String(envelope.Payload, "kind") ?? KindOf(envelope.ToolId, parameter);
        if (!_engine.Evaluates(parameter, kind))
        {
            return;
        }

        var window = _plant.WindowAt(envelope.ToolId, parameter, envelope.OccurredAt);
        if (window is null)
        {
            return;
        }

        var sigma = Math.Max(window.Sigma, 1e-6d);
        var reading = new SpcReading(
            envelope.ToolId,
            envelope.ChamberId,
            parameter,
            kind,
            envelope.OccurredAt,
            _plant.Score(envelope.ToolId, parameter, value.Value, window),
            value.Value,
            window.Nominal,
            sigma,
            window.Minimum,
            window.Maximum,
            envelope.CorrelationId,
            window.Unit);
        var display = new SpcDisplay(window.Nominal, sigma, window.Unit, _plant.FamilyLabel(envelope.ToolId));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var observation = _engine.Observe(reading);
            if (observation is null || observation.Action == SpcAction.None)
            {
                return;
            }

            if (_options.IsShadow)
            {
                NoteShadow(observation);
                return;
            }

            await using var scope = _scopes.CreateAsyncScope();
            var spc = scope.ServiceProvider.GetRequiredService<SpcAlertService>();
            // The alert event is published inside ApplyAsync and parents to the current reading.
            // Remember this envelope before that, so a later point in the same batch cannot take the link.
            _cascade.RememberReading(reading.ToolId, reading.Parameter, envelope.EventId, envelope.CorrelationId);
            var outcome = await spc.ApplyAsync(observation, display, cancellationToken).ConfigureAwait(false);
            if (outcome.Debounced && observation.Action == SpcAction.Raise)
            {
                _engine.RevertRaise(reading.ToolId, reading.ChamberId, reading.Parameter);
            }

            if (outcome.TicketSuppressed)
            {
                TicketCapped(_logger, reading.ToolId, reading.Parameter, _engine.Settings.TicketCap.PerToolPerHour);
            }

            if (outcome.Ticket is not null)
            {
                TicketOpened(_logger, reading.ToolId, reading.Parameter);
            }

            if (outcome.Incident is null || !_started.TryAdd(outcome.Incident.Id.Value, 0))
            {
                return;
            }

            var incidentId = outcome.Incident.Id;
            var hints = scope.ServiceProvider.GetService<ICascadeDirectory>();
            var parent = hints?.FindStep(outcome.Incident.CorrelationId, "ticket") ?? hints?.FindStep(outcome.Incident.CorrelationId, "alert");
            var stop = Volatile.Read(ref _runs).Token;
            _pending.Enqueue(InvestigationParent.Run(parent, () => InvestigateAsync(incidentId, stop)));
        }
        finally
        {
            _gate.Release();
        }
    }

    private void NoteShadow(SpcObservation observation)
    {
        var wouldTicket = false;
        var suppressed = false;
        if (observation.Action is SpcAction.Raise or SpcAction.Escalate
            && observation.Tier == SpcTier.Alert
            && observation.Severity >= Mps.Domain.Incidents.Severity.High)
        {
            if (_cap.TryAdmit(observation.Reading.ToolId, observation.Reading.At, _engine.Settings.TicketCap.PerToolPerHour))
            {
                wouldTicket = true;
            }
            else
            {
                suppressed = true;
            }
        }

        _shadow.Note(observation, wouldTicket, suppressed);
        if (observation.Tier == SpcTier.Alert)
        {
            ShadowAlert(_logger, observation.Action, observation.RuleText, observation.Reading.ToolId, observation.Reading.Parameter);
        }
        else if (observation.Tier == SpcTier.Watch)
        {
            ShadowWatch(_logger, observation.Action, observation.RuleText, observation.Reading.ToolId, observation.Reading.Parameter);
        }
    }

    private string? KindOf(string toolId, string parameter)
    {
        try
        {
            var tool = _plant.Runtime.Fleet.GetRequired(toolId);
            var sensor = tool.Sensors.FirstOrDefault(item =>
                string.Equals(item.Parameter, parameter, StringComparison.Ordinal)
                || string.Equals(item.Id, parameter, StringComparison.Ordinal));
            return sensor?.Kind;
        }
        catch (FleetSpecException)
        {
            return null;
        }
    }

    private async Task InvestigateAsync(IncidentId incidentId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var investigations = scope.ServiceProvider.GetRequiredService<IInvestigationService>();
            await investigations.StartAsync(incidentId, cancellationToken).ConfigureAwait(false);
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
