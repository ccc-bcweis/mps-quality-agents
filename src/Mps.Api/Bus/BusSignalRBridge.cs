// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Microsoft.AspNetCore.SignalR;

using Mps.Api.Hubs;
using Mps.Bus;
using Mps.Domain.Events;

namespace Mps.Api.Bus;

/// <summary>
/// Forwards a bus fact to the fleet hub when the payload names a signal. Each message carries the changed record
/// (its "body"), so a page patches its list instead of downloading it again. A payload may also carry "related"
/// records, for example the ticket whose status follows an incident. Telemetry is not sent here: the
/// <see cref="Live.LiveStreamPublisher"/> sends throttled deltas to the pages that watch a tool or plant.
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
        var fleet = _hub.Clients.Group(FleetHub.FleetGroup);
        await fleet.SendAsync(signal, body, cancellationToken).ConfigureAwait(false);
        if (!envelope.Payload.TryGetProperty("related", out var related) || related.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in related.EnumerateArray())
        {
            var name = item.ValueKind == JsonValueKind.Object ? PayloadReader.String(item, "signal") : null;
            if (name is not null && item.TryGetProperty("body", out var relatedBody) && relatedBody.ValueKind == JsonValueKind.Object)
            {
                await fleet.SendAsync(name, relatedBody, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
