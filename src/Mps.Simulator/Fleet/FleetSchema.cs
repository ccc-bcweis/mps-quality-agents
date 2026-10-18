// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Text.RegularExpressions;

using Mps.Domain.Equipment;

namespace Mps.Simulator.Fleet;

/// <summary>
/// Schema rules for fleet YAML. <see cref="FleetCatalogLoader"/> runs them on every file before a catalog is returned.
/// </summary>
public static partial class FleetSchema
{
    public static IReadOnlyList<string> Evaluate(FleetCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var errors = new List<string>();
        if (catalog.Tools.Count != 8)
        {
            errors.Add($"The fleet must contain 8 tools. Found {catalog.Tools.Count}.");
        }

        foreach (var family in Enum.GetValues<ToolFamily>())
        {
            var members = catalog.Tools.Where(tool => tool.Family == family).ToArray();
            if (members.Length != 2)
            {
                errors.Add($"{family} must have 2 tools. Found {members.Length}.");
            }

            if (members.Select(tool => tool.Bay).Distinct(StringComparer.Ordinal).Count() != members.Length)
            {
                errors.Add($"{family} tools must sit in different bays.");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tool in members)
            {
                foreach (var recipe in tool.Recipes)
                {
                    foreach (var step in recipe.Steps)
                    {
                        foreach (var window in step.Windows)
                        {
                            seen.Add(window.Parameter);
                        }
                    }
                }
            }

            foreach (var required in FamilyParameterCatalog.For(family).Keys)
            {
                if (!seen.Contains(required))
                {
                    errors.Add($"{family} is missing an operating window for {required}.");
                }
            }
        }

        var bays = catalog.Tools.Select(tool => tool.Bay).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (bays.Length != 2)
        {
            errors.Add($"The fleet must span 2 bays. Found {bays.Length}.");
        }

        var toolIds = new HashSet<string>(StringComparer.Ordinal);
        var chamberIds = new HashSet<string>(StringComparer.Ordinal);
        var sensorIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tool in catalog.Tools)
        {
            var path = tool.Id;
            if (!toolIds.Add(tool.Id))
            {
                errors.Add($"{path}: duplicate tool id.");
            }

            errors.AddRange(EvaluateTool(tool, path));
            foreach (var chamber in tool.Chambers)
            {
                if (!chamberIds.Add(chamber.Id))
                {
                    errors.Add($"{path}: duplicate chamber id {chamber.Id}.");
                }
            }

            foreach (var sensor in tool.Sensors)
            {
                if (!sensorIds.Add(sensor.Id))
                {
                    errors.Add($"{path}: duplicate sensor id {sensor.Id}.");
                }
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> EvaluateWindow(ToolFamily family, OperatingWindow window, string path)
    {
        ArgumentNullException.ThrowIfNull(window);
        var errors = new List<string>();
        var limits = FamilyParameterCatalog.For(family);
        if (!limits.TryGetValue(window.Parameter, out var limit))
        {
            errors.Add($"{path}: {window.Parameter} is not a {family} parameter.");
            return errors;
        }

        if (!string.Equals(window.Unit, limit.Unit, StringComparison.Ordinal))
        {
            errors.Add($"{path}: {window.Parameter} unit is {window.Unit}. The schema requires {limit.Unit}.");
        }

        if (window.Minimum < limit.Minimum || window.Maximum > limit.Maximum)
        {
            errors.Add(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{path}: {window.Parameter} window {window.Minimum}-{window.Maximum} is outside the family envelope {limit.Minimum}-{limit.Maximum} {limit.Unit}."));
        }

        return errors;
    }

    private static IEnumerable<string> EvaluateTool(ToolSpec tool, string path)
    {
        if (!string.Equals(tool.Model, FamilyParameterCatalog.ModelFor(tool.Family), StringComparison.Ordinal))
        {
            yield return $"{path}: model {tool.Model} does not match {FamilyParameterCatalog.ModelFor(tool.Family)}.";
        }

        var prefix = FamilyParameterCatalog.IdPrefix(tool.Family);
        if (tool.Id.Length != 4 || !tool.Id.StartsWith(prefix + "-", StringComparison.Ordinal) || !char.IsDigit(tool.Id[2]) || !char.IsDigit(tool.Id[3]))
        {
            yield return $"{path}: tool id must look like {prefix}-01.";
        }

        if (tool.Chambers.Count == 0)
        {
            yield return $"{path}: a tool needs at least one chamber.";
        }

        if (tool.Recipes.Count == 0)
        {
            yield return $"{path}: a tool needs at least one recipe.";
        }

        var recipeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var recipe in tool.Recipes)
        {
            if (!recipeIds.Add(recipe.Id))
            {
                yield return $"{path}: duplicate recipe {recipe.Id}.";
            }

            if (recipe.Steps.Count < 2)
            {
                yield return $"{path}/{recipe.Id}: a recipe needs at least two steps.";
            }

            var stepIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in recipe.Steps)
            {
                if (!stepIds.Add(step.Id))
                {
                    yield return $"{path}/{recipe.Id}: duplicate step {step.Id}.";
                }

                if (step.Windows.Count == 0)
                {
                    yield return $"{path}/{recipe.Id}/{step.Id}: a step needs an operating window.";
                }

                var parameters = new HashSet<string>(StringComparer.Ordinal);
                foreach (var window in step.Windows)
                {
                    if (!parameters.Add(window.Parameter))
                    {
                        yield return $"{path}/{recipe.Id}/{step.Id}: duplicate window for {window.Parameter}.";
                    }

                    foreach (var error in EvaluateWindow(tool.Family, window, $"{path}/{recipe.Id}/{step.Id}"))
                    {
                        yield return error;
                    }
                }

                // Every step defines every parameter its tool measures: its own window, or idle with a window. Nothing
                // falls back to the sensor's physical range (#367).
                foreach (var required in ToolParameters(tool))
                {
                    if (!parameters.Contains(required))
                    {
                        yield return $"{path}/{recipe.Id}/{step.Id}: the step does not define {required}. Give it a window, or mark it idle with a window.";
                    }
                }
            }
        }

        foreach (var error in EvaluateSensors(tool, path))
        {
            yield return error;
        }
    }

    /// <summary>The process parameters a tool measures: its chamber sensors that carry a parameter (not reflected power or tool counters).</summary>
    public static IReadOnlyList<string> ToolParameters(ToolSpec tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return tool.Sensors
            .Where(sensor => sensor.Parameter is not null && sensor.TracksSensorId is null && sensor.ChamberId is not null)
            .Select(sensor => sensor.Parameter!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> EvaluateSensors(ToolSpec tool, string path)
    {
        if (tool.Sensors.Count == 0)
        {
            yield return $"{path}: sensors are required.";
            yield break;
        }

        var anchors = new HashSet<string>(StringComparer.Ordinal);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sensor in tool.Sensors)
        {
            foreach (var error in EvaluateSensor(tool, sensor, path))
            {
                yield return error;
            }

            if (!seenIds.Add(sensor.Id))
            {
                yield return $"{path}: duplicate sensor id {sensor.Id}.";
            }

            if (!anchors.Add(sensor.SchematicAnchor))
            {
                yield return $"{path}: duplicate schematic anchor {sensor.SchematicAnchor}.";
            }
        }

        foreach (var chamber in tool.Chambers)
        {
            foreach (var parameter in RecipeParameters(tool))
            {
                var count = tool.Sensors.Count(sensor =>
                    string.Equals(sensor.ChamberId, chamber.Id, StringComparison.Ordinal)
                    && string.Equals(sensor.Parameter, parameter, StringComparison.Ordinal));
                if (count != 1)
                {
                    yield return $"{path}: chamber {chamber.Id} needs exactly one sensor for {parameter}. Found {count}.";
                }
            }
        }

        var present = tool.Sensors.Select(sensor => sensor.Type).ToHashSet(StringComparer.Ordinal);
        foreach (var required in SensorCatalog.RequiredTypes(tool.Family))
        {
            if (!present.Contains(required))
            {
                yield return $"{path}: missing a {required} sensor.";
            }
        }

        if (tool.Family == ToolFamily.Clean && present.Contains(SensorCatalog.RfForwardPower))
        {
            yield return $"{path}: a clean tool has megasonic power, not an RF generator.";
        }
    }

    private static IEnumerable<string> EvaluateSensor(ToolSpec tool, SensorSpec sensor, string path)
    {
        var where = $"{path}/{sensor.Id}";
        if (!SensorIdPattern().IsMatch(sensor.Id) || !sensor.Id.StartsWith(tool.Id + "-", StringComparison.Ordinal))
        {
            yield return $"{where}: sensor id must start with {tool.Id}-.";
        }

        if (!AnchorPattern().IsMatch(sensor.SchematicAnchor))
        {
            yield return $"{where}: schematic anchor must look like a.chamber.pressure or tool.runRate.";
        }

        if (!SensorCatalog.IsType(sensor.Type))
        {
            yield return $"{where}: type {sensor.Type} is not in the sensor catalog.";
        }

        if (!SensorCatalog.IsSubsystem(sensor.Subsystem))
        {
            yield return $"{where}: subsystem {sensor.Subsystem} is not in the sensor catalog.";
        }

        if (SensorCatalog.IsType(sensor.Type) && !SensorCatalog.AllowsUnit(sensor.Type, sensor.Unit))
        {
            yield return $"{where}: unit {sensor.Unit} is not valid for {sensor.Type}.";
        }

        var toolScoped = sensor.Type is SensorCatalog.RunRate or SensorCatalog.Throughput or SensorCatalog.Yield
            || (sensor.Type == SensorCatalog.Output && sensor.Parameter is null);
        if (toolScoped)
        {
            if (!sensor.IsToolScope || sensor.Parameter is not null || sensor.TracksSensorId is not null)
            {
                yield return $"{where}: a {sensor.Type} point is tool-scoped and has no chamber, parameter, or tracked sensor.";
            }

            if (!sensor.SchematicAnchor.StartsWith("tool.", StringComparison.Ordinal))
            {
                yield return $"{where}: a tool-scoped anchor starts with tool.";
            }

            yield break;
        }

        if (sensor.Type == SensorCatalog.RfReflectedPower)
        {
            foreach (var error in EvaluateReflected(tool, sensor, where))
            {
                yield return error;
            }

            yield break;
        }

        if (sensor.ChamberId is null || sensor.Parameter is null || sensor.TracksSensorId is not null)
        {
            yield return $"{where}: a {sensor.Type} point names a chamber and a parameter and does not track another sensor.";
            yield break;
        }

        if (!tool.Chambers.Any(chamber => string.Equals(chamber.Id, sensor.ChamberId, StringComparison.Ordinal)))
        {
            yield return $"{where}: chamber {sensor.ChamberId} is not on this tool.";
        }

        var slot = sensor.ChamberId[^1] == 'A' ? "a." : sensor.ChamberId[^1] == 'B' ? "b." : null;
        if (slot is null || !sensor.SchematicAnchor.StartsWith(slot, StringComparison.Ordinal))
        {
            yield return $"{where}: schematic anchor must start with the chamber slot.";
        }

        var limits = FamilyParameterCatalog.For(tool.Family);
        if (!limits.TryGetValue(sensor.Parameter, out var envelope))
        {
            yield return $"{where}: {sensor.Parameter} is not a {tool.Family} parameter.";
            yield break;
        }

        if (!string.Equals(sensor.Unit, envelope.Unit, StringComparison.Ordinal))
        {
            yield return $"{where}: unit {sensor.Unit} does not match the family unit {envelope.Unit}.";
        }

        if (!Nearly(sensor.Limits.Minimum, envelope.Minimum) || !Nearly(sensor.Limits.Maximum, envelope.Maximum))
        {
            yield return string.Create(
                CultureInfo.InvariantCulture,
                $"{where}: spec limits must be the family envelope {envelope.Minimum}-{envelope.Maximum} {envelope.Unit}.");
        }

        if (!RecipeNominals(tool, sensor.Parameter).Any(nominal => Nearly(nominal, sensor.Limits.Nominal)))
        {
            yield return $"{where}: nominal must match a recipe window for {sensor.Parameter}.";
        }
    }

    private static IEnumerable<string> EvaluateReflected(ToolSpec tool, SensorSpec sensor, string where)
    {
        if (sensor.ChamberId is null || sensor.Parameter is not null || sensor.TracksSensorId is null)
        {
            yield return $"{where}: reflected power names a chamber and the forward sensor it tracks.";
            yield break;
        }

        var reflectedSlot = sensor.ChamberId[^1] == 'A' ? "a." : sensor.ChamberId[^1] == 'B' ? "b." : null;
        if (reflectedSlot is null || !sensor.SchematicAnchor.StartsWith(reflectedSlot, StringComparison.Ordinal))
        {
            yield return $"{where}: schematic anchor must start with the chamber slot.";
        }

        if (!tool.Chambers.Any(chamber => string.Equals(chamber.Id, sensor.ChamberId, StringComparison.Ordinal)))
        {
            yield return $"{where}: chamber {sensor.ChamberId} is not on this tool.";
        }

        var forward = tool.Sensors.FirstOrDefault(candidate => string.Equals(candidate.Id, sensor.TracksSensorId, StringComparison.Ordinal));
        if (forward is null || forward.Type != SensorCatalog.RfForwardPower || !string.Equals(forward.ChamberId, sensor.ChamberId, StringComparison.Ordinal))
        {
            yield return $"{where}: tracksSensorId must be an RF forward sensor on the same chamber.";
            yield break;
        }

        if (!Nearly(sensor.Limits.Minimum, 0d)
            || !Nearly(sensor.Limits.Maximum, forward.Limits.Maximum * 0.15d)
            || !Nearly(sensor.Limits.Nominal, forward.Limits.Nominal * 0.03d))
        {
            yield return $"{where}: reflected spec is 0 to 15 percent of the forward maximum, with nominal at 3 percent of the forward nominal.";
        }
    }

    private static IEnumerable<string> RecipeParameters(ToolSpec tool)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var recipe in tool.Recipes)
        {
            foreach (var step in recipe.Steps)
            {
                foreach (var window in step.Windows)
                {
                    if (seen.Add(window.Parameter))
                    {
                        yield return window.Parameter;
                    }
                }
            }
        }
    }

    private static IEnumerable<double> RecipeNominals(ToolSpec tool, string parameter)
    {
        foreach (var recipe in tool.Recipes)
        {
            foreach (var step in recipe.Steps)
            {
                var window = step.WindowFor(parameter);
                if (window is not null)
                {
                    yield return window.Nominal;
                }
            }
        }
    }

    private static bool Nearly(double left, double right) => Math.Abs(left - right) <= 1e-6d;

    [GeneratedRegex("^[A-Z]-[0-9]{2}(-[A-Z0-9]+)+$")]
    private static partial Regex SensorIdPattern();

    [GeneratedRegex("^(a|b|tool)(\\.[a-z][A-Za-z0-9]*)+$")]
    private static partial Regex AnchorPattern();
}
