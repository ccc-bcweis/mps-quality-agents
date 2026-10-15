// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Simulator.Fleet;
using Mps.Simulator.Scenarios;
using Mps.Simulator.Telemetry;

namespace Mps.Simulator.Demo;

/// <summary>
/// Seeded fleet simulator and clock. <see cref="Reset"/> restores both to the baseline origin.
/// </summary>
public sealed class DemoRuntime
{
    public const int DefaultSeed = 4242;

    public DemoRuntime(FleetCatalog fleet, int seed, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        Fleet = fleet;
        Seed = seed;
        Clock = new DemoClock(timeProvider);
        Simulator = new FleetSimulator(fleet, seed, DemoClock.BaselineOrigin);
    }

    public FleetCatalog Fleet { get; }

    public int Seed { get; }

    public DemoClock Clock { get; }

    public FleetSimulator Simulator { get; }

    public void Reset()
    {
        Simulator.Reset();
        Clock.Reset();
    }

    public void Apply(ScriptedScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        foreach (var signal in scenario.Signals(DemoClock.BaselineOrigin))
        {
            Simulator.Inject(signal);
        }
    }

    public bool TryApplyFreeForm(string toolId, string? chamberId, string parameter, string shape, double magnitude)
    {
        if (string.IsNullOrWhiteSpace(toolId) || string.IsNullOrWhiteSpace(parameter) || string.IsNullOrWhiteSpace(shape))
        {
            return false;
        }

        if (!Enum.TryParse<FaultShape>(shape, ignoreCase: true, out var parsed))
        {
            return false;
        }

        if (!double.IsFinite(magnitude))
        {
            return false;
        }

        var tool = Fleet.Tools.FirstOrDefault(candidate => string.Equals(candidate.Id, toolId.Trim(), StringComparison.Ordinal));
        if (tool is null || tool.Chambers.Count == 0)
        {
            return false;
        }

        var name = parameter.Trim();
        var known = tool.Recipes.Any(recipe => RecipeSchedule.Parameters(recipe).Contains(name, StringComparer.Ordinal));
        if (!known)
        {
            return false;
        }

        var chamber = string.IsNullOrWhiteSpace(chamberId) ? tool.Chambers[0].Id : chamberId.Trim();
        Simulator.Inject(new FaultSignal(tool.Id, chamber, name, parsed, MagnitudeKind.Sigma, magnitude, Clock.SimulatedNow));
        return true;
    }
}
