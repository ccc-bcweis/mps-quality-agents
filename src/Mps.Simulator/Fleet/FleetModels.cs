// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Equipment;
using Mps.Domain.Identity;

namespace Mps.Simulator.Fleet;

public sealed record ChamberSpec
{
    public ChamberSpec(string id, string name)
    {
        Id = Require(id, nameof(id));
        Name = Require(name, nameof(name));
    }

    public string Id { get; }

    public string Name { get; }

    private static string Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", name);
        }

        return value.Trim();
    }
}

public sealed record RecipeStepSpec
{
    public RecipeStepSpec(string id, string name, IReadOnlyList<OperatingWindow> windows)
    {
        Id = Require(id, nameof(id));
        Name = Require(name, nameof(name));
        ArgumentNullException.ThrowIfNull(windows);
        Windows = windows.ToArray();
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyList<OperatingWindow> Windows { get; }

    public OperatingWindow? WindowFor(string parameter)
    {
        return Windows.FirstOrDefault(window => string.Equals(window.Parameter, parameter, StringComparison.Ordinal));
    }

    private static string Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", name);
        }

        return value.Trim();
    }
}

public sealed record RecipeSpec
{
    public RecipeSpec(string id, string name, IReadOnlyList<RecipeStepSpec> steps)
    {
        Id = Require(id, nameof(id));
        Name = Require(name, nameof(name));
        ArgumentNullException.ThrowIfNull(steps);
        Steps = steps.ToArray();
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyList<RecipeStepSpec> Steps { get; }

    public RecipeStepSpec? Step(string stepId)
    {
        return Steps.FirstOrDefault(step => string.Equals(step.Id, stepId, StringComparison.Ordinal));
    }

    private static string Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", name);
        }

        return value.Trim();
    }
}

/// <summary>
/// One fictional MPS tool, including the recipe steps and operating windows the simulator samples.
/// </summary>
public sealed class ToolSpec
{
    public ToolSpec(
        string id,
        string name,
        ToolFamily family,
        string model,
        string bay,
        IReadOnlyList<ChamberSpec> chambers,
        IReadOnlyList<RecipeSpec> recipes,
        IReadOnlyList<SensorSpec> sensors)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Tool id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tool name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("Model is required.", nameof(model));
        }

        if (string.IsNullOrWhiteSpace(bay))
        {
            throw new ArgumentException("Bay is required.", nameof(bay));
        }

        ArgumentNullException.ThrowIfNull(chambers);
        ArgumentNullException.ThrowIfNull(recipes);
        ArgumentNullException.ThrowIfNull(sensors);
        Id = id.Trim();
        Name = name.Trim();
        Family = family;
        Model = model.Trim();
        Bay = bay.Trim();
        Chambers = chambers.ToArray();
        Recipes = recipes.ToArray();
        Sensors = sensors.ToArray();
    }

    public string Id { get; }

    public string Name { get; }

    public ToolFamily Family { get; }

    public string Model { get; }

    public string Bay { get; }

    public IReadOnlyList<ChamberSpec> Chambers { get; }

    public IReadOnlyList<RecipeSpec> Recipes { get; }

    public IReadOnlyList<SensorSpec> Sensors { get; }

    public SensorSpec RequireParameterSensor(string chamberId, string parameter)
    {
        var matches = Sensors.Where(sensor =>
            string.Equals(sensor.ChamberId, chamberId, StringComparison.Ordinal)
            && string.Equals(sensor.Parameter, parameter, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException($"Tool {Id} chamber {chamberId} needs exactly one sensor for {parameter}. Found {matches.Length}.");
        }

        return matches[0];
    }

    public Tool ToDomainTool()
    {
        var chambers = Chambers.Select(chamber => new Chamber(chamber.Id, chamber.Name)).ToArray();
        return new Tool(new ToolId(Id), Name, Family, Model, Bay, chambers);
    }
}

/// <summary>
/// The eight-tool fleet loaded from <c>data/fleet</c>.
/// </summary>
public sealed class FleetCatalog
{
    public FleetCatalog(IReadOnlyList<ToolSpec> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        Tools = tools.ToArray();
    }

    public IReadOnlyList<ToolSpec> Tools { get; }

    public ToolSpec GetRequired(string toolId)
    {
        var tool = Tools.FirstOrDefault(candidate => string.Equals(candidate.Id, toolId, StringComparison.Ordinal));
        if (tool is null)
        {
            throw new FleetSpecException([$"{toolId} is not in the fleet catalog."]);
        }

        return tool;
    }

    public IReadOnlyList<Tool> ToDomainTools()
    {
        return Tools.Select(tool => tool.ToDomainTool()).ToArray();
    }
}
