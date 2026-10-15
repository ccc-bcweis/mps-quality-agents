// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Live;
using Mps.Application.Faults;
using Mps.Domain.Hypotheses;
using Mps.Simulator.Scenarios;
using Mps.Simulator.Telemetry;

namespace Mps.Api.Demo;

/// <summary>
/// Runs a named scenario: deterministic telemetry from the demo seed, plus the free-form fault record.
/// </summary>
public sealed class ScenarioTrigger
{
    private readonly PlantSimulation _plant;
    private readonly IFaultInjectionService _faults;
    private readonly ScriptedScenarioContext _context;
    private readonly ScenarioEvidenceBoard? _board;

    public ScenarioTrigger(PlantSimulation plant, IFaultInjectionService faults, ScriptedScenarioContext context, ScenarioEvidenceBoard? board = null)
    {
        ArgumentNullException.ThrowIfNull(plant);
        ArgumentNullException.ThrowIfNull(faults);
        ArgumentNullException.ThrowIfNull(context);
        _plant = plant;
        _faults = faults;
        _context = context;
        _board = board;
    }

    public async Task<ScenarioRun?> RunAsync(string scenarioId, CancellationToken cancellationToken)
    {
        if (!ScriptedScenarioCatalog.TryGet(scenarioId, out var scenario))
        {
            return null;
        }

        _context.Set(scenario.Id);
        _board?.Activate(scenario.Id);
        try
        {
            var telemetry = ScriptedScenarioCatalog.Sample(scenario, _plant.Runtime.Fleet, _plant.Runtime.Seed);
            _plant.Apply(scenario);
            FaultAccepted? first = null;
            foreach (var toolId in scenario.ToolIds)
            {
                var accepted = await _faults.InjectAsync(
                    new FaultInjectionRequest(
                        toolId,
                        ScriptedScenario.ChamberIdFor(toolId),
                        scenario.Parameter,
                        scenario.ShapeName,
                        SigmaOf(scenario),
                        scenario.Category,
                        scenario.Confounders),
                    cancellationToken).ConfigureAwait(false);
                first ??= accepted;
            }

            return new ScenarioRun(
                scenario.Id,
                _plant.Runtime.Seed,
                first!.FaultId,
                first.CorrelationId,
                telemetry.Select(point => new ScenarioPointResponse(point.ToolId, point.ChamberId, point.Parameter, point.At, point.Value)).ToArray());
        }
        finally
        {
            _context.Set(null);
        }
    }

    /// <summary>The injector takes sigma. A percent scenario (for example a 12 percent flow drop) maps to a clear 4.5 sigma step with the same sign.</summary>
    private static double SigmaOf(ScriptedScenario scenario) =>
        scenario.MagnitudeKind == MagnitudeKind.Percent ? Math.CopySign(4.5d, scenario.Magnitude) : scenario.Magnitude;
}

public sealed record ScenarioRun(
    string ScenarioId,
    int Seed,
    Guid FaultId,
    string CorrelationId,
    IReadOnlyList<ScenarioPointResponse> Telemetry);

public sealed record ScenarioPointResponse(string ToolId, string ChamberId, string Parameter, DateTimeOffset At, double Value);

public sealed record ScenarioDescription(
    string Id,
    string Title,
    RootCauseCategory RootCauseCategory,
    IReadOnlyList<string> ToolIds,
    string ChamberId,
    string Parameter,
    string Shape,
    string Summary,
    string WhatsHappening,
    string WhatWeSimulate,
    string WhatAiShouldFind,
    string ExpectedCause);
