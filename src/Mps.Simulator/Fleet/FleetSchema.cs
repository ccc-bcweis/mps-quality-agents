// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

using Mps.Domain.Equipment;

namespace Mps.Simulator.Fleet;

/// <summary>
/// Schema rules for fleet YAML. <see cref="FleetCatalogLoader"/> runs them on every file before a catalog is returned.
/// </summary>
public static class FleetSchema
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
            }
        }
    }
}
