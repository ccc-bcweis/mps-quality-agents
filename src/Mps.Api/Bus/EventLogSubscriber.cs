// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Bus;
using Mps.Bus;

namespace Mps.Api.Bus;

/// <summary>
/// Writes commands and domain facts to the event log, and readings to the telemetry table.
/// A multi-reading batch stays on the batch path. The catch-all listener ignores those rows.
/// </summary>
public sealed class EventLogSubscriber : IHostedService, IDisposable
{
    private readonly IEventBus _bus;
    private readonly SqliteEventStore _store;
    private readonly ICascadeDirectory _cascade;
    private readonly List<IDisposable> _subscriptions = [];
    private int _disposed;

    public EventLogSubscriber(IEventBus bus, SqliteEventStore store, ICascadeDirectory cascade)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(cascade);
        _bus = bus;
        _store = store;
        _cascade = cascade;
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

    private ValueTask OnEvent(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (EventTypes.IsTelemetry(envelope.Type))
        {
            RememberReading(envelope);
            return ValueTask.CompletedTask;
        }

        _store.AppendLog(envelope);
        if (string.Equals(envelope.Type, EventTypes.FaultInject, StringComparison.Ordinal))
        {
            var parameter = PayloadReader.String(envelope.Payload, "parameter");
            if (!string.IsNullOrWhiteSpace(envelope.ToolId) && parameter is not null)
            {
                _cascade.RememberFault(envelope.CorrelationId, envelope.ToolId, parameter, envelope.EventId);
            }

            _cascade.RememberStep(envelope.CorrelationId, "fault", envelope.EventId);
        }

        return ValueTask.CompletedTask;
    }

    private ValueTask OnTelemetry(IReadOnlyList<EventEnvelope> batch, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _store.AppendTelemetry(batch);
        foreach (var envelope in batch)
        {
            RememberReading(envelope);
        }

        return ValueTask.CompletedTask;
    }

    private void RememberReading(EventEnvelope envelope)
    {
        var parameter = PayloadReader.String(envelope.Payload, "parameter");
        if (string.IsNullOrWhiteSpace(envelope.ToolId) || parameter is null)
        {
            return;
        }

        _cascade.RememberReading(envelope.ToolId, parameter, envelope.EventId, envelope.CorrelationId);
    }
}
