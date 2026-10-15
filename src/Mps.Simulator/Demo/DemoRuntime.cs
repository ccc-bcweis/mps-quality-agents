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

    public DemoRuntime(FleetCatalog fleet, int seed, TimeProvider? timeProvider = null, DateTimeOffset? origin = null)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        Fleet = fleet;
        Seed = seed;
        Clock = new DemoClock(timeProvider);
        Simulator = new FleetSimulator(fleet, seed, origin ?? DemoClock.BaselineOrigin);
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

    /// <summary>Resets and restarts the simulator clock on <paramref name="origin"/>.</summary>
    public void Reset(DateTimeOffset origin)
    {
        Simulator.Reset(origin);
        Clock.Reset();
    }

    public void Apply(ScriptedScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        Simulator.InjectAtSimulatedNow(scenario.Signals);
    }

    public bool TryApplyFreeForm(string toolId, string? chamberId, string parameter, string shape, double magnitude, double pace = 1d)
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

        var name = ResolveParameter(tool, parameter.Trim());
        if (name is null)
        {
            return false;
        }

        var chamber = string.IsNullOrWhiteSpace(chamberId) ? tool.Chambers[0].Id : chamberId.Trim();
        Simulator.InjectAtSimulatedNow(onset =>
        [
            new FaultSignal(tool.Id, chamber, name, parsed, MagnitudeKind.Sigma, magnitude, onset, periodSeconds: 1200d, rampSeconds: 7200d).Paced(pace),
        ]);
        return true;
    }

    /// <summary>
    /// The injector form uses a few friendly names. Map those onto the recipe parameter the simulator actually emits.
    /// </summary>
    private static string? ResolveParameter(ToolSpec tool, string requested)
    {
        if (RecipeHas(tool, requested))
        {
            return requested;
        }

        if (requested.Equals("gasFlow", StringComparison.OrdinalIgnoreCase))
        {
            return FirstRecipeParameter(tool, "cf4Flow", "o2Flow", "sih4Flow", "n2h2Flow");
        }

        if (requested.Equals("calibrationOffset", StringComparison.OrdinalIgnoreCase))
        {
            return FirstRecipeParameter(tool, "chamberPressure", "temperature", "escTemperature");
        }

        if (requested.Equals("particleAdders", StringComparison.OrdinalIgnoreCase))
        {
            return FirstRecipeParameter(tool, "particlesAdders");
        }

        return null;
    }

    private static string? FirstRecipeParameter(ToolSpec tool, params string[] names)
    {
        foreach (var name in names)
        {
            if (RecipeHas(tool, name))
            {
                return name;
            }
        }

        return null;
    }

    private static bool RecipeHas(ToolSpec tool, string parameter) =>
        tool.Recipes.Any(recipe => RecipeSchedule.Parameters(recipe).Contains(parameter, StringComparer.Ordinal));
}
