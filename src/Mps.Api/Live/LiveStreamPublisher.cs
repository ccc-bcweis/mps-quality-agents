// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.AspNetCore.SignalR;

using Mps.Api.Bus;
using Mps.Api.Demo;
using Mps.Api.Http;
using Mps.Api.Hubs;
using Mps.Application.Persistence;
using Mps.Bus;
using Mps.Domain.Events;

namespace Mps.Api.Live;

/// <summary>
/// Pushes live telemetry to the console over the fleet hub so pages stop polling. Kept samples are collected per
/// tool and sent at most once per <see cref="SimulationOptions.PushIntervalMilliseconds"/>: toolLive to the pages
/// watching that tool, fleetLive to the pages watching its plant (or every plant). Only watched tools are collected.
/// Every page also gets the plant clock, reset progress, and a resync after a reset rebuilds the data.
/// </summary>
public sealed partial class LiveStreamPublisher : IHostedService, IDisposable
{
    [LoggerMessage(EventId = 7201, Level = LogLevel.Warning, Message = "Live stream push failed.")]
    private static partial void PushFailed(ILogger logger, Exception error);

    private readonly IEventBus _bus;
    private readonly IHubContext<FleetHub> _hub;
    private readonly PlantSimulation _plant;
    private readonly LiveSubscriptions _subscriptions;
    private readonly ResetProgress _reset;
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _time;
    private readonly SimulationOptions _options;
    private readonly ILogger<LiveStreamPublisher> _logger;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _flush = new(1, 1);
    private readonly Dictionary<string, PendingTool> _pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _siteOf = new(StringComparer.Ordinal);
    private IDisposable? _subscription;
    private IDisposable? _manualSubscription;
    private DateTimeOffset _lastPush = DateTimeOffset.MinValue;
    private DateTimeOffset _lastClock = DateTimeOffset.MinValue;
    private bool _trailingScheduled;
    private int _disposed;

    public LiveStreamPublisher(
        IEventBus bus,
        IHubContext<FleetHub> hub,
        PlantSimulation plant,
        LiveSubscriptions subscriptions,
        ResetProgress reset,
        IServiceScopeFactory scopes,
        TimeProvider time,
        SimulationOptions options,
        ILogger<LiveStreamPublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentNullException.ThrowIfNull(plant);
        ArgumentNullException.ThrowIfNull(subscriptions);
        ArgumentNullException.ThrowIfNull(reset);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _bus = bus;
        _hub = hub;
        _plant = plant;
        _subscriptions = subscriptions;
        _reset = reset;
        _scopes = scopes;
        _time = time;
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _subscription = _bus.SubscribeBatch(EventTypes.TelemetryReading, OnTelemetry);
        _manualSubscription = _bus.Subscribe(EventTypes.DocumentUploaded, OnManualReading);
        _reset.Changed += OnResetChanged;
        return Task.CompletedTask;
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

        _reset.Changed -= OnResetChanged;
        _subscription?.Dispose();
        _subscription = null;
        _manualSubscription?.Dispose();
        _manualSubscription = null;
    }

    /// <summary>A reading entered from a printout: tells the tool page to reload its manual readings.</summary>
    public async ValueTask OnManualReading(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var toolId = envelope.ToolId;
        if (string.IsNullOrWhiteSpace(toolId) || !_subscriptions.Watched(FleetHub.ToolGroup(toolId)))
        {
            return;
        }

        try
        {
            var delta = LiveResponses.ToolDelta(_plant, toolId, null, [], resync: false, manual: true);
            if (delta is not null)
            {
                await _hub.Clients.Group(FleetHub.ToolGroup(toolId)).SendAsync("toolLive", delta, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PushFailed(_logger, ex);
        }
    }

    /// <summary>Collects a telemetry batch; sends when the push interval has passed, else schedules one trailing send.</summary>
    public async ValueTask OnTelemetry(IReadOnlyList<EventEnvelope> batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Count == 0 || _reset.Current.Running)
        {
            return;
        }

        Collect(batch);
        var now = _time.GetUtcNow();
        bool due;
        lock (_gate)
        {
            due = now - _lastPush >= Interval;
            if (!due && !_trailingScheduled && _pending.Count > 0)
            {
                _trailingScheduled = true;
                _ = TrailingAsync(Interval - (now - _lastPush));
            }
        }

        if (due)
        {
            await FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Sends everything collected so far. Tests call it directly.</summary>
    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        await _flush.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Dictionary<string, PendingTool> taken;
            var now = _time.GetUtcNow();
            bool clockDue;
            lock (_gate)
            {
                taken = new Dictionary<string, PendingTool>(_pending, StringComparer.Ordinal);
                _pending.Clear();
                _lastPush = now;
                _trailingScheduled = false;
                clockDue = now - _lastClock >= TimeSpan.FromSeconds(_options.ClockIntervalSeconds);
                if (clockDue)
                {
                    _lastClock = now;
                }
            }

            if (clockDue)
            {
                await _hub.Clients.Group(FleetHub.FleetGroup)
                    .SendAsync("clock", new LiveClockMessage(_plant.SimulatedNow, _plant.Speed, _plant.StrideSeconds, _plant.RunningScenarioId), cancellationToken)
                    .ConfigureAwait(false);
            }

            if (taken.Count == 0)
            {
                return;
            }

            var status = await AlertStatusAsync(cancellationToken).ConfigureAwait(false);
            foreach (var (toolId, pending) in taken)
            {
                var group = FleetHub.ToolGroup(toolId);
                if (!_subscriptions.Watched(group))
                {
                    continue;
                }

                var delta = LiveResponses.ToolDelta(_plant, toolId, status, pending.Points, pending.Resync);
                if (delta is not null)
                {
                    await _hub.Clients.Group(group).SendAsync("toolLive", delta, cancellationToken).ConfigureAwait(false);
                }
            }

            await SendFleetAsync(taken, status, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PushFailed(_logger, ex);
        }
        finally
        {
            _flush.Release();
        }
    }

    private TimeSpan Interval => TimeSpan.FromMilliseconds(_options.PushIntervalMilliseconds);

    private async Task SendFleetAsync(Dictionary<string, PendingTool> taken, IReadOnlyDictionary<string, string> status, CancellationToken cancellationToken)
    {
        var allGroup = FleetHub.SiteGroup(LiveSubscriptions.AllSites);
        var all = _subscriptions.Watched(allGroup);
        var bySite = taken.Values
            .GroupBy(pending => pending.SiteId, StringComparer.OrdinalIgnoreCase)
            .Where(group => all || _subscriptions.Watched(FleetHub.SiteGroup(group.Key)))
            .ToArray();
        if (bySite.Length == 0)
        {
            return;
        }

        var now = _plant.SimulatedNow;
        var rowsBySite = bySite.ToDictionary(
            group => group.Key,
            group => LiveResponses.FleetRows(_plant, group.Select(pending => pending.ToolId), status),
            StringComparer.OrdinalIgnoreCase);
        foreach (var (siteId, rows) in rowsBySite)
        {
            var group = FleetHub.SiteGroup(siteId);
            if (rows.Count > 0 && _subscriptions.Watched(group))
            {
                await _hub.Clients.Group(group).SendAsync("fleetLive", new FleetLiveDelta(now, rows), cancellationToken).ConfigureAwait(false);
            }
        }

        if (all)
        {
            var rows = rowsBySite.Values.SelectMany(row => row).OrderBy(row => row.ToolId, StringComparer.Ordinal).ToArray();
            if (rows.Length > 0)
            {
                await _hub.Clients.Group(allGroup).SendAsync("fleetLive", new FleetLiveDelta(now, rows), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void Collect(IReadOnlyList<EventEnvelope> batch)
    {
        if (!_subscriptions.Any)
        {
            return;
        }

        var allSites = _subscriptions.Watched(FleetHub.SiteGroup(LiveSubscriptions.AllSites));
        lock (_gate)
        {
            foreach (var envelope in batch)
            {
                var toolId = envelope.ToolId;
                var parameter = PayloadReader.String(envelope.Payload, "parameter");
                var value = PayloadReader.Number(envelope.Payload, "value");
                if (string.IsNullOrWhiteSpace(toolId) || parameter is null || value is null)
                {
                    continue;
                }

                if (!_pending.TryGetValue(toolId, out var pending))
                {
                    var siteId = SiteOf(toolId);
                    var watched = allSites
                        || _subscriptions.Watched(FleetHub.ToolGroup(toolId))
                        || _subscriptions.Watched(FleetHub.SiteGroup(siteId));
                    if (!watched)
                    {
                        continue;
                    }

                    pending = new PendingTool(toolId, siteId);
                    _pending.Add(toolId, pending);
                }

                pending.Add(new LiveStreamPoint(envelope.ChamberId ?? _plant.DefaultChamber(toolId), parameter, envelope.OccurredAt, value.Value), _options.MaxPushPointsPerSeries);
            }
        }
    }

    private string SiteOf(string toolId)
    {
        if (!_siteOf.TryGetValue(toolId, out var siteId))
        {
            siteId = LiveResponses.SiteOf(_plant, toolId);
            _siteOf[toolId] = siteId;
        }

        return siteId;
    }

    private async Task<IReadOnlyDictionary<string, string>> AlertStatusAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var alerts = scope.ServiceProvider.GetRequiredService<IAlertLog>();
        return ToolAlertStatus.ByTool(await alerts.ListAsync(null, cancellationToken).ConfigureAwait(false));
    }

    private async Task TrailingAsync(TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.Zero, _time).ConfigureAwait(false);
            if (Volatile.Read(ref _disposed) == 0)
            {
                await FlushAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (ObjectDisposedException)
        {
            // The host stopped while the send waited.
        }
    }

    private void OnResetChanged(ResetProgressSnapshot snapshot)
    {
        if (snapshot.Running)
        {
            lock (_gate)
            {
                _pending.Clear();
            }
        }

        _ = SendResetAsync(snapshot);
    }

    private async Task SendResetAsync(ResetProgressSnapshot snapshot)
    {
        try
        {
            var fleet = _hub.Clients.Group(FleetHub.FleetGroup);
            await fleet.SendAsync("resetProgress", snapshot).ConfigureAwait(false);
            if (string.Equals(snapshot.Phase, "done", StringComparison.Ordinal))
            {
                await fleet.SendAsync("resync", new LiveResyncMessage("reset")).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PushFailed(_logger, ex);
        }
    }

    private sealed class PendingTool(string toolId, string siteId)
    {
        private readonly Dictionary<(string Chamber, string Parameter), int> _counts = [];

        public string ToolId { get; } = toolId;

        public string SiteId { get; } = siteId;

        public List<LiveStreamPoint> Points { get; } = [];

        public bool Resync { get; private set; }

        public void Add(LiveStreamPoint point, int maxPerSeries)
        {
            if (Resync)
            {
                return;
            }

            var key = (point.ChamberId, point.Parameter);
            var count = _counts.GetValueOrDefault(key) + 1;
            _counts[key] = count;
            if (count > maxPerSeries)
            {
                // A burst (priming, a seeded week) is cheaper as one reload than as a huge message.
                Resync = true;
                Points.Clear();
                return;
            }

            Points.Add(point);
        }
    }
}
