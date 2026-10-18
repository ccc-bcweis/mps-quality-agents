// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Simulator.Fleet;

namespace Mps.Simulator.Telemetry;

/// <summary>
/// Deterministic recipe stepper. Each step lasts <see cref="StepSeconds"/> simulated seconds, then the next step, then the next recipe.
/// </summary>
public static class RecipeSchedule
{
    /// <summary>Seconds per recipe step (data/metadata/simulation.yaml).</summary>
    public static int StepSeconds => SimulationSettings.Shared.RecipeSchedule.StepSeconds;

    /// <summary>Seconds per wafer (data/metadata/simulation.yaml).</summary>
    public static int WaferSeconds => SimulationSettings.Shared.RecipeSchedule.WaferSeconds;

    /// <summary>Wafers per lot (data/metadata/simulation.yaml).</summary>
    public static int WafersPerLot => SimulationSettings.Shared.RecipeSchedule.WafersPerLot;

    public static RecipeCursor Locate(ToolSpec tool, int elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedSeconds);

        var flat = Flatten(tool);
        var index = (elapsedSeconds / StepSeconds) % flat.Count;
        return flat[index];
    }

    public static string RunId(string toolId, int elapsedSeconds)
    {
        var wafer = elapsedSeconds / WaferSeconds;
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"RUN-{toolId}-{wafer:D6}");
    }

    public static string LotId(string toolId, int elapsedSeconds)
    {
        var lot = elapsedSeconds / WaferSeconds / WafersPerLot;
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"LOT-{toolId}-{lot:D4}");
    }

    public static IReadOnlyList<string> Parameters(RecipeSpec recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var seen = new List<string>();
        foreach (var step in recipe.Steps)
        {
            foreach (var window in step.Windows)
            {
                if (!seen.Contains(window.Parameter, StringComparer.Ordinal))
                {
                    seen.Add(window.Parameter);
                }
            }
        }

        return seen;
    }

    public static OperatingWindow Resolve(RecipeCursor cursor, string parameter)
    {
        var onStep = cursor.Step.WindowFor(parameter);
        if (onStep is not null)
        {
            return onStep;
        }

        foreach (var step in cursor.Recipe.Steps)
        {
            var window = step.WindowFor(parameter);
            if (window is not null)
            {
                return window;
            }
        }

        throw new InvalidOperationException($"Recipe {cursor.Recipe.Id} has no window for {parameter}.");
    }

    private static List<RecipeCursor> Flatten(ToolSpec tool)
    {
        var flat = new List<RecipeCursor>();
        foreach (var recipe in tool.Recipes)
        {
            foreach (var step in recipe.Steps)
            {
                flat.Add(new RecipeCursor(recipe, step));
            }
        }

        if (flat.Count == 0)
        {
            throw new InvalidOperationException($"Tool {tool.Id} has no recipe steps.");
        }

        return flat;
    }
}

public readonly record struct RecipeCursor(RecipeSpec Recipe, RecipeStepSpec Step);
