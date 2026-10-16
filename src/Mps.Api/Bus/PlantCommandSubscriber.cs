// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Demo;
using Mps.Api.Live;
using Mps.Bus;
using Mps.Simulator.Scenarios;

namespace Mps.Api.Bus;

/// <summary>
/// Applies a scenario or a manual fault when its command arrives. Scripted fault commands are records only:
/// the scenario command already applied every signal, and applying them again would stack the fault.
/// SC-6 runs like every other scenario; its oscilloscope sheet is added as one manual sample, not as a fault.
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
        _subscriptions.Add(_bus.Subscribe(EventTypes.FaultInject, OnFault));
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

        _board.Activate(scenario.Id);
        _plant.ApplyStream(scenario);
        return ValueTask.CompletedTask;
    }

    private ValueTask OnFault(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (PayloadReader.Bool(envelope.Payload, "scripted"))
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
