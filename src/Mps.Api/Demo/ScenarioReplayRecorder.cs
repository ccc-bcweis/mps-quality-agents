// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Bus;
using Mps.Domain.Events;

namespace Mps.Api.Demo;

/// <summary>
/// Subscribes for one scenario run and writes the bus messages that run produced.
/// Telemetry batches are taken from the batch listener. The catch-all listener skips readings so a single reading is not stored twice.
/// </summary>
public sealed class ScenarioReplayRecorder
{
    private readonly IEventBus _bus;
    private readonly ReplayStore _store;

    public ScenarioReplayRecorder(IEventBus bus, ReplayStore store)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(store);
        _bus = bus;
        _store = store;
    }

    public async Task<int> CaptureAsync(string scenarioId, Func<CancellationToken, Task> run, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioId);
        ArgumentNullException.ThrowIfNull(run);
        var captured = new List<ReplayMessage>();
        var gate = new object();
        using var telemetry = _bus.SubscribeBatch(EventTypes.TelemetryReading, (batch, _) =>
        {
            lock (gate)
            {
                foreach (var envelope in batch)
                {
                    captured.Add(ReplayMessage.From(envelope));
                }
            }

            return ValueTask.CompletedTask;
        });
        using var facts = _bus.Subscribe("*", (envelope, _) =>
        {
            if (EventTypes.IsTelemetry(envelope.Type))
            {
                return ValueTask.CompletedTask;
            }

            lock (gate)
            {
                captured.Add(ReplayMessage.From(envelope));
            }

            return ValueTask.CompletedTask;
        });

        await run(cancellationToken).ConfigureAwait(false);
        ReplayMessage[] snapshot;
        lock (gate)
        {
            snapshot = [.. captured];
        }

        _store.Save(scenarioId, snapshot);
        return snapshot.Length;
    }
}
