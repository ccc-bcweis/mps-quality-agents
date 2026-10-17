// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Simulator.Fleet;
using Mps.Simulator.Telemetry;

namespace Mps.Simulator.Tests;

/// <summary>
/// Regression: pump vibration on D-02 went silent whenever the tool ran a recipe without a vibration window,
/// so a vibration fault could not raise an alarm for half of every cycle.
/// </summary>
public sealed class ChamberSensorStreamingTests
{
    private static readonly FleetCatalog Catalog = FleetCatalogLoader.Load(RepoPaths.FleetDirectory());

    [Fact]
    public void EveryChamberSensorStreamsUnderEveryRecipe()
    {
        var simulator = new FleetSimulator(Catalog, 4242, DemoClock.BaselineOrigin);
        var longest = Catalog.Tools.Max(tool => tool.Recipes.Sum(recipe => recipe.Steps.Count)) * RecipeSchedule.StepSeconds;
        var frames = simulator.Advance(TimeSpan.FromSeconds(longest), 1);

        foreach (var tool in Catalog.Tools)
        {
            foreach (var sensor in tool.Sensors.Where(sensor => sensor.ChamberId is not null && sensor.Parameter is not null && sensor.TracksSensorId is null))
            {
                var seen = frames.Where(frame => frame.ToolId == tool.Id && frame.SensorId == sensor.Id).Select(frame => frame.RecipeId).Distinct().ToArray();
                seen.Should().BeEquivalentTo(tool.Recipes.Select(recipe => recipe.Id), $"{sensor.Id} must read under every recipe");
            }
        }
    }

    [Fact]
    public void PumpVibrationOnD02KeepsReadingDuringTheNitrideRecipe()
    {
        var simulator = new FleetSimulator(Catalog, 4242, DemoClock.BaselineOrigin);
        var tool = Catalog.GetRequired("D-02");
        var cycle = tool.Recipes.Sum(recipe => recipe.Steps.Count) * RecipeSchedule.StepSeconds;
        var frames = simulator.Advance(TimeSpan.FromSeconds(cycle), 1);

        var vibration = frames.Where(frame => frame.ToolId == "D-02" && frame.Parameter == "vibrationRms" && frame.ChamberId == "D-02-A").ToArray();
        vibration.Should().HaveCount(cycle);
        var sensor = tool.RequireParameterSensor("D-02-A", "vibrationRms");
        vibration.Should().OnlyContain(frame => frame.Value >= sensor.Limits.Minimum && frame.Value <= sensor.Limits.Maximum);
    }
}
