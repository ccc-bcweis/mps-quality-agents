// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Demo;
using Mps.Api.Live;
using Mps.Bus;
using Mps.Domain.Events;
using Mps.Simulator.Scenarios;

namespace Mps.Api.Bus;

/// <summary>
/// Applies a scenario or a manual fault when its command arrives. A record-only fault command is the record of a
/// signal the owning command already applied, so applying it again would stack the fault. Stopping a scenario
/// or a fault drops that correlation's enterprise records.
/// </summary>
public sealed class PlantCommandSubscriber : IHostedService, IDisposable
{
    private readonly IEventBus _bus;
    private readonly PlantSimulation _plant;
    private readonly ScenarioEvidenceBoard _board;
    private readonly List<IDisposable> _subscriptions = [];
    private int _disposed;

    public PlantCommandSubscriber(IEventBus bus, PlantSimulation plant, ScenarioEvidenceBoard board)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(plant);
        ArgumentNullException.ThrowIfNull(board);
        _bus = bus;
        _plant = plant;
        _board = board;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _subscriptions.Add(_bus.Subscribe(EventTypes.ScenarioStart, OnScenario));
        _subscriptions.Add(_bus.Subscribe(EventTypes.ScenarioStopped, OnScenarioStopped));
        _subscriptions.Add(_bus.Subscribe(EventTypes.FaultInject, OnFault));
        _subscriptions.Add(_bus.Subscribe(EventTypes.FaultCleared, OnFaultCleared));
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

    private ValueTask OnScenario(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var scenarioId = PayloadReader.String(envelope.Payload, "scenarioId");
        if (scenarioId is null)
        {
            return ValueTask.CompletedTask;
        }

        if (!ScriptedScenarioCatalog.TryGet(scenarioId, out var scenario))
        {
            return ValueTask.CompletedTask;
        }

        _plant.ApplyStream(scenario);
        return ValueTask.CompletedTask;
    }

    private ValueTask OnScenarioStopped(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var scenarioId = PayloadReader.String(envelope.Payload, "scenarioId");
        if (scenarioId is null)
        {
            // The injector stop clears every fault on the plant, so every registered correlation goes with them.
            _board.Clear();
        }
        else
        {
            _board.RemoveScenario(scenarioId);
        }

        return ValueTask.CompletedTask;
    }

    private ValueTask OnFaultCleared(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _board.Remove(envelope.CorrelationId);
        return ValueTask.CompletedTask;
    }

    private ValueTask OnFault(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (PayloadReader.Bool(envelope.Payload, "recordOnly"))
        {
            return ValueTask.CompletedTask;
        }

        var toolId = envelope.ToolId ?? PayloadReader.String(envelope.Payload, "toolId");
        var parameter = PayloadReader.String(envelope.Payload, "parameter");
        var shape = PayloadReader.String(envelope.Payload, "shape");
        var magnitude = PayloadReader.Number(envelope.Payload, "magnitude");
        if (toolId is null || parameter is null || shape is null || magnitude is null)
        {
            return ValueTask.CompletedTask;
        }

        var chamberId = envelope.ChamberId ?? PayloadReader.String(envelope.Payload, "chamberId");
        var minutes = PayloadReader.Number(envelope.Payload, "durationMinutes");
        _plant.TryApplyFreeForm(toolId, chamberId, parameter, shape, magnitude.Value, minutes is > 0 ? TimeSpan.FromMinutes(minutes.Value) : null);
        return ValueTask.CompletedTask;
    }
}
