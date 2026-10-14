// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Equipment;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Simulator.Fleet;

/// <summary>
/// Loads <c>data/fleet/*.yaml</c>. Tools omit recipes to inherit the family recipe set.
/// </summary>
public static class FleetCatalogLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static FleetCatalog Load(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Fleet directory is required.", nameof(directory));
        }

        if (!Directory.Exists(directory))
        {
            throw new FleetSpecException([$"Fleet directory does not exist: {directory}"]);
        }

        var errors = new List<string>();
        var tools = new List<ToolSpec>();
        var families = new HashSet<ToolFamily>();
        var files = Directory.GetFiles(directory, "*.yaml").Order(StringComparer.Ordinal).ToArray();
        if (files.Length == 0)
        {
            errors.Add($"No fleet YAML files in {directory}.");
        }

        foreach (var file in files)
        {
            var source = Path.GetFileName(file);
            FleetFileDto dto;
            try
            {
                dto = Deserializer.Deserialize<FleetFileDto>(File.ReadAllText(file)) ?? new FleetFileDto();
            }
            catch (Exception exception)
            {
                errors.Add($"{source}: {exception.Message}");
                continue;
            }

            if (!MapFamily(dto, source, errors, out var family, out var recipes))
            {
                continue;
            }

            if (!families.Add(family))
            {
                errors.Add($"{source}: duplicate family {dto.Family}.");
                continue;
            }

            MapTools(dto, source, family, recipes, tools, errors);
        }

        if (families.Count != 4)
        {
            errors.Add($"Expected one YAML file for each of the 4 families. Found {families.Count}.");
        }

        if (errors.Count > 0)
        {
            throw new FleetSpecException(errors);
        }

        var catalog = new FleetCatalog(tools);
        var schemaErrors = FleetSchema.Evaluate(catalog);
        if (schemaErrors.Count > 0)
        {
            throw new FleetSpecException(schemaErrors);
        }

        return catalog;
    }

    private static bool MapFamily(
        FleetFileDto dto,
        string source,
        List<string> errors,
        out ToolFamily family,
        out IReadOnlyList<RecipeSpec> recipes)
    {
        family = default;
        recipes = [];
        if (!FamilyParameterCatalog.TryParseFamily(dto.Family, out family))
        {
            errors.Add($"{source}: family '{dto.Family}' is not deposition, etch, strip, or clean.");
            return false;
        }

        if (!string.Equals(dto.Model?.Trim(), FamilyParameterCatalog.ModelFor(family), StringComparison.Ordinal))
        {
            errors.Add($"{source}: model must be {FamilyParameterCatalog.ModelFor(family)}.");
        }

        recipes = MapRecipes(dto.Recipes, $"{source}/recipes", errors);
        if (recipes.Count == 0)
        {
            errors.Add($"{source}: family recipes are required.");
            return false;
        }

        return true;
    }

    private static void MapTools(
        FleetFileDto dto,
        string source,
        ToolFamily family,
        IReadOnlyList<RecipeSpec> familyRecipes,
        List<ToolSpec> tools,
        List<string> errors)
    {
        if (dto.Tools is null || dto.Tools.Count == 0)
        {
            errors.Add($"{source}: tools are required.");
            return;
        }

        for (var index = 0; index < dto.Tools.Count; index++)
        {
            var tool = dto.Tools[index];
            var path = $"{source}/tools[{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";
            var recipes = tool.Recipes is { Count: > 0 }
                ? MapRecipes(tool.Recipes, path + "/recipes", errors)
                : familyRecipes;
            var chambers = MapChambers(tool.Chambers, path, errors);
            var sensors = MapSensors(tool.Sensors, path, errors);
            if (string.IsNullOrWhiteSpace(tool.Id) || string.IsNullOrWhiteSpace(tool.Name) || string.IsNullOrWhiteSpace(tool.Bay))
            {
                errors.Add($"{path}: id, name, and bay are required.");
                continue;
            }

            if (chambers.Count == 0 || recipes.Count == 0 || sensors.Count == 0)
            {
                continue;
            }

            tools.Add(new ToolSpec(tool.Id, tool.Name, family, dto.Model!.Trim(), tool.Bay, chambers, recipes, sensors));
        }
    }

    private static List<SensorSpec> MapSensors(List<SensorDto>? sensors, string path, List<string> errors)
    {
        if (sensors is null || sensors.Count == 0)
        {
            errors.Add($"{path}: sensors are required.");
            return [];
        }

        var mapped = new List<SensorSpec>();
        for (var index = 0; index < sensors.Count; index++)
        {
            var sensor = sensors[index];
            var sensorPath = $"{path}/sensors[{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";
            if (string.IsNullOrWhiteSpace(sensor.Id)
                || string.IsNullOrWhiteSpace(sensor.Type)
                || string.IsNullOrWhiteSpace(sensor.Unit)
                || string.IsNullOrWhiteSpace(sensor.Subsystem)
                || string.IsNullOrWhiteSpace(sensor.SchematicAnchor))
            {
                errors.Add($"{sensorPath}: id, type, unit, subsystem, and schematicAnchor are required.");
                continue;
            }

            if (sensor.Minimum is null || sensor.Maximum is null || sensor.Nominal is null || sensor.WarningMinimum is null || sensor.WarningMaximum is null)
            {
                errors.Add($"{sensorPath}: minimum, maximum, nominal, warningMinimum, and warningMaximum are required.");
                continue;
            }

            try
            {
                mapped.Add(new SensorSpec(
                    sensor.Id,
                    sensor.Type,
                    sensor.Unit,
                    sensor.Subsystem,
                    sensor.SchematicAnchor,
                    new SensorLimits(sensor.Minimum.Value, sensor.Maximum.Value, sensor.Nominal.Value, sensor.WarningMinimum.Value, sensor.WarningMaximum.Value),
                    sensor.ChamberId,
                    sensor.Parameter,
                    sensor.TracksSensorId));
            }
            catch (ArgumentException exception)
            {
                errors.Add($"{sensorPath}: {exception.Message}");
            }
        }

        return mapped;
    }

    private static List<ChamberSpec> MapChambers(List<ChamberDto>? chambers, string path, List<string> errors)
    {
        if (chambers is null || chambers.Count == 0)
        {
            errors.Add($"{path}: chambers are required.");
            return [];
        }

        var mapped = new List<ChamberSpec>();
        for (var index = 0; index < chambers.Count; index++)
        {
            var chamber = chambers[index];
            if (string.IsNullOrWhiteSpace(chamber.Id) || string.IsNullOrWhiteSpace(chamber.Name))
            {
                errors.Add($"{path}/chambers[{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}]: id and name are required.");
                continue;
            }

            mapped.Add(new ChamberSpec(chamber.Id, chamber.Name));
        }

        return mapped;
    }

    private static List<RecipeSpec> MapRecipes(List<RecipeDto>? recipes, string path, List<string> errors)
    {
        if (recipes is null)
        {
            return [];
        }

        var mapped = new List<RecipeSpec>();
        for (var index = 0; index < recipes.Count; index++)
        {
            var recipe = recipes[index];
            var recipePath = $"{path}[{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";
            if (string.IsNullOrWhiteSpace(recipe.Id) || string.IsNullOrWhiteSpace(recipe.Name))
            {
                errors.Add($"{recipePath}: recipe id and name are required.");
                continue;
            }

            var steps = MapSteps(recipe.Steps, recipePath, errors);
            if (steps.Count == 0)
            {
                continue;
            }

            mapped.Add(new RecipeSpec(recipe.Id, recipe.Name, steps));
        }

        return mapped;
    }

    private static List<RecipeStepSpec> MapSteps(List<StepDto>? steps, string path, List<string> errors)
    {
        if (steps is null || steps.Count == 0)
        {
            errors.Add($"{path}: steps are required.");
            return [];
        }

        var mapped = new List<RecipeStepSpec>();
        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            var stepPath = $"{path}/steps[{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";
            if (string.IsNullOrWhiteSpace(step.Id) || string.IsNullOrWhiteSpace(step.Name))
            {
                errors.Add($"{stepPath}: step id and name are required.");
                continue;
            }

            var windows = MapWindows(step.Windows, stepPath, errors);
            if (windows.Count == 0)
            {
                continue;
            }

            mapped.Add(new RecipeStepSpec(step.Id, step.Name, windows));
        }

        return mapped;
    }

    private static List<OperatingWindow> MapWindows(List<WindowDto>? windows, string path, List<string> errors)
    {
        if (windows is null || windows.Count == 0)
        {
            errors.Add($"{path}: windows are required.");
            return [];
        }

        var mapped = new List<OperatingWindow>();
        for (var index = 0; index < windows.Count; index++)
        {
            var window = windows[index];
            var windowPath = $"{path}/windows[{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";
            if (string.IsNullOrWhiteSpace(window.Parameter) || string.IsNullOrWhiteSpace(window.Unit))
            {
                errors.Add($"{windowPath}: parameter and unit are required.");
                continue;
            }

            if (window.Minimum is null || window.Maximum is null || window.Nominal is null)
            {
                errors.Add($"{windowPath}: minimum, maximum, and nominal are required.");
                continue;
            }

            try
            {
                mapped.Add(new OperatingWindow(window.Parameter, window.Unit, window.Minimum.Value, window.Maximum.Value, window.Nominal.Value));
            }
            catch (ArgumentException exception)
            {
                errors.Add($"{windowPath}: {exception.Message}");
            }
        }

        return mapped;
    }

    private sealed class FleetFileDto
    {
        public string? Family { get; set; }

        public string? Model { get; set; }

        public List<RecipeDto>? Recipes { get; set; }

        public List<ToolDto>? Tools { get; set; }
    }

    private sealed class ToolDto
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Bay { get; set; }

        public List<ChamberDto>? Chambers { get; set; }

        public List<RecipeDto>? Recipes { get; set; }

        public List<SensorDto>? Sensors { get; set; }
    }

    private sealed class SensorDto
    {
        public string? Id { get; set; }

        public string? Type { get; set; }

        public string? Unit { get; set; }

        public string? Subsystem { get; set; }

        public string? SchematicAnchor { get; set; }

        public string? ChamberId { get; set; }

        public string? Parameter { get; set; }

        public string? TracksSensorId { get; set; }

        public double? Minimum { get; set; }

        public double? Maximum { get; set; }

        public double? Nominal { get; set; }

        public double? WarningMinimum { get; set; }

        public double? WarningMaximum { get; set; }
    }

    private sealed class ChamberDto
    {
        public string? Id { get; set; }

        public string? Name { get; set; }
    }

    private sealed class RecipeDto
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public List<StepDto>? Steps { get; set; }
    }

    private sealed class StepDto
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public List<WindowDto>? Windows { get; set; }
    }

    private sealed class WindowDto
    {
        public string? Parameter { get; set; }

        public string? Unit { get; set; }

        public double? Minimum { get; set; }

        public double? Maximum { get; set; }

        public double? Nominal { get; set; }
    }
}
