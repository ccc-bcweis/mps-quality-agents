// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Microsoft.AspNetCore.SignalR;

using Mps.Api.Hubs;
using Mps.Bus;

namespace Mps.Api.Bus;

/// <summary>
/// Forwards a bus fact to the fleet hub when the payload names a signal.
/// Telemetry batches push one latest value per tool and parameter. Polling remains the fallback.
/// </summary>
public sealed class BusSignalRBridge : IHostedService, IDisposable
{
    private readonly IEventBus _bus;
    private readonly IHubContext<FleetHub> _hub;
    private readonly List<IDisposable> _subscriptions = [];
    private int _disposed;

    public BusSignalRBridge(IEventBus bus, IHubContext<FleetHub> hub)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(hub);
        _bus = bus;
        _hub = hub;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _subscriptions.Add(_bus.Subscribe("*", OnEvent));
        _subscriptions.Add(_bus.SubscribeBatch(EventTypes.TelemetryReading, OnTelemetry));
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
    }

    private async ValueTask OnEvent(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (EventTypes.IsTelemetry(envelope.Type))
        {
            return;
        }

        var signal = PayloadReader.String(envelope.Payload, "signal");
        if (signal is null)
        {
            return;
        }

        var body = envelope.Payload.TryGetProperty("body", out var raw) && raw.ValueKind == JsonValueKind.Object
            ? raw
            : envelope.Payload;
        await SendAsync(signal, envelope.ToolId, body, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask OnTelemetry(IReadOnlyList<EventEnvelope> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        var latest = new Dictionary<string, TelemetryPush>(StringComparer.Ordinal);
        foreach (var envelope in batch)
        {
            var parameter = PayloadReader.String(envelope.Payload, "parameter");
            var value = PayloadReader.Number(envelope.Payload, "value");
            if (string.IsNullOrWhiteSpace(envelope.ToolId) || parameter is null || value is null)
            {
                continue;
            }

            latest[envelope.ToolId + "|" + parameter] = new TelemetryPush(
                envelope.ToolId,
                parameter,
                value.Value,
                envelope.OccurredAt,
                envelope.EventId);
        }

        if (latest.Count == 0)
        {
            return;
        }

        await SendAsync("telemetry", null, new { points = latest.Values.ToArray() }, cancellationToken).ConfigureAwait(false);
    }

    private Task SendAsync(string eventName, string? toolId, object payload, CancellationToken cancellationToken)
    {
        var fleet = _hub.Clients.Group(FleetHub.FleetGroup).SendAsync(eventName, payload, cancellationToken);
        if (string.IsNullOrWhiteSpace(toolId))
        {
            return fleet;
        }

        var tool = _hub.Clients.Group(FleetHub.ToolGroup(toolId)).SendAsync(eventName, payload, cancellationToken);
        return Task.WhenAll(fleet, tool);
    }

    private sealed record TelemetryPush(string ToolId, string Parameter, double Value, DateTimeOffset At, string EventId);
}
