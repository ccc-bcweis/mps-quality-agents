// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using FluentAssertions;

using Microsoft.Extensions.Configuration;

using Mps.Api.Http;
using Mps.Api.Live;
using Mps.Application.Spc;
using Mps.Simulator.Fleet;
using Mps.Simulator.Scenarios;
using Mps.Simulator.Sites;
using Mps.Simulator.Telemetry;

using Xunit;

namespace Mps.Api.Tests;

/// <summary>
/// Chart and rules limits come from the recipe step windows at every moment: before, during and after a fault (#373),
/// every step defines every parameter (#367), and no limit leaves what the sensor can physically read (#368).
/// </summary>
public sealed class RecipeStepLimitsTests
{
    /// <summary>
    /// Bryan's SC-1 run (Oct 18): E-02 chamber A ESC temperature at 10x with a 5 s sample. The limits must keep
    /// stepping with the recipe while the chuck temperature oscillates, and never follow the faulty readings.
    /// </summary>
    [Fact]
    public void LimitsKeepFollowingTheRecipeStepsDuringAndAfterAFault()
    {
        var (plant, time) = Start();
        ScriptedScenarioCatalog.TryGet("SC-1", out var scenario).Should().BeTrue();
        var onset = plant.SimulatedNow;
        plant.ApplyStream(scenario);
        var steps = RecipeSchedule.StepSeconds;
        for (var second = 0; second < steps * 4; second++)
        {
            Tick(plant, time);
        }

        var live = LiveResponses.Tool(plant, "E-02")!;
        var series = live.Series.Single(item => item.Parameter == "escTemperature" && item.ChamberId == "E-02-A");
        var segments = series.Segments!;
        var tool = plant.Runtime.Fleet.GetRequired("E-02");
        var faulted = series.Points.Where(point => point.At >= onset).ToArray();
        faulted.Length.Should().BeGreaterThan(steps * 3, "readings keep streaming every second at 10x");

        foreach (var point in series.Points)
        {
            var segment = segments.Last(item => item.From <= point.At);
            var elapsed = (int)(point.At - plant.Runtime.Simulator.Origin).TotalSeconds;
            var cursor = RecipeSchedule.Locate(tool, elapsed);
            var window = cursor.Step.WindowFor("escTemperature")!;
            segment.Center.Should().Be(window.Nominal, $"the point at {point.At:O} was read in {cursor.Recipe.Name} / {cursor.Step.Name}");
            segment.Sigma.Should().Be(window.Sigma);
            segment.Step.Should().Be(cursor.Step.Name);
            segment.Recipe.Should().Be(cursor.Recipe.Name);
        }

        // During the fault the limits change at every step boundary, like before it.
        segments.Count(item => item.From >= onset).Should().BeGreaterThanOrEqualTo(3, "four steps ran during the fault");
        var faultMean = faulted.Average(point => point.Value);
        segments.Where(item => item.From >= onset).Should().OnlyContain(
            item => tool.Recipes.SelectMany(recipe => recipe.Steps).Any(step => step.WindowFor("escTemperature")!.Nominal == item.Center),
            $"every center is a recipe step nominal, never the mean of the faulty readings ({faultMean:F2})");

        // The rules engine scores each faulty reading against the same step window the chart draws.
        foreach (var point in faulted)
        {
            var segment = segments.Last(item => item.From <= point.At);
            var window = plant.WindowAt("E-02", "escTemperature", point.At)!;
            plant.Score("E-02", "escTemperature", point.Value, window).Should().BeApproximately((point.Value - segment.Center) / segment.Sigma, 1e-9);
        }
    }

    [Fact]
    public void EveryRecipeStepDefinesEveryParameterOfItsTool()
    {
        var (plant, _) = Start();
        var checkedWindows = 0;
        foreach (var tool in plant.Runtime.Fleet.Tools)
        {
            var parameters = FleetSchema.ToolParameters(tool);
            foreach (var recipe in tool.Recipes)
            {
                foreach (var step in recipe.Steps)
                {
                    foreach (var parameter in parameters)
                    {
                        var window = step.WindowFor(parameter);
                        window.Should().NotBeNull($"{tool.Id} {recipe.Id}/{step.Id} must define {parameter} (own window or idle)");
                        window!.Maximum.Should().BeGreaterThan(window.Minimum);
                        checkedWindows++;
                    }
                }
            }
        }

        checkedWindows.Should().BeGreaterThan(300);
    }

    [Fact]
    public void NoLimitLeavesWhatTheSensorCanPhysicallyRead()
    {
        var (plant, _) = Start();
        var rules = SpcRuleSettings.Shared;
        var clampedSomething = false;
        foreach (var tool in plant.Runtime.Fleet.Tools)
        {
            foreach (var sensor in tool.Sensors.Where(item => !item.IsCumulative))
            {
                var parameter = sensor.Parameter ?? sensor.Id;
                var range = plant.PhysicalRange(tool.Id, parameter);
                range.Should().NotBeNull($"{sensor.Id} has a physical range in the fleet data");
                var windows = sensor.Parameter is null
                    ? [plant.WindowAt(tool.Id, parameter, plant.SimulatedNow)!]
                    : tool.Recipes.SelectMany(recipe => recipe.Steps).Select(step => step.WindowFor(parameter)!).ToArray();
                foreach (var window in windows)
                {
                    var sigma = Math.Max(window.Sigma, 1e-6);
                    var raw = rules.ControlLimits(window.Nominal, sigma);
                    var (low, high) = plant.ClampedControlLimits(tool.Id, parameter, window.Nominal, sigma);
                    low.Should().BeGreaterThanOrEqualTo(range!.Value.Minimum, $"{tool.Id} {parameter}");
                    high.Should().BeLessThanOrEqualTo(range.Value.Maximum, $"{tool.Id} {parameter}");
                    clampedSomething |= raw.Low < low || raw.High > high;
                }
            }
        }

        clampedSomething.Should().BeTrue("reflected RF power has a center near zero, so its raw lower limit was negative");

        // The live readings the console shows carry the clamped limits too: no negative flow or power limit.
        var live = LiveResponses.Tool(plant, "E-02")!;
        foreach (var reading in live.Readings.Where(item => item.ControlLow is not null))
        {
            var range = plant.PhysicalRange("E-02", reading.Parameter ?? reading.SensorId)!.Value;
            reading.ControlLow!.Value.Should().BeGreaterThanOrEqualTo(range.Minimum, reading.SensorId);
            reading.ControlHigh!.Value.Should().BeLessThanOrEqualTo(range.Maximum, reading.SensorId);
        }

        live.Series.Where(item => item.Segments is not null).SelectMany(item => item.Segments!)
            .Should().OnlyContain(segment => segment.Floor != null && segment.Ceiling != null);
    }

    [Fact]
    public void AReadingTheSensorCannotProduceIsPastEveryClampedLimit()
    {
        var (plant, _) = Start();
        var window = plant.WindowAt("E-02", "E-02-A-RF-BIAS-RFL", plant.SimulatedNow)!;
        var critical = SpcRuleSettings.Shared.CriticalSigma;
        plant.Score("E-02", "E-02-A-RF-BIAS-RFL", -1, window).Should().BeLessThanOrEqualTo(-critical);
        plant.Score("E-02", "E-02-A-RF-BIAS-RFL", window.Nominal, window).Should().Be(0);
        plant.Score("E-02", "E-02-A-RF-BIAS-RFL", window.Maximum + 1, window).Should().BeGreaterThanOrEqualTo(critical);
    }

    private static (PlantSimulation Plant, ManualTime Time) Start()
    {
        var time = new ManualTime(DateTimeOffset.Parse("2026-10-18T06:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        var world = SitePlantGenerator.Load(Path.Combine(Root(), "data"), 4242);
        var plant = new PlantSimulation(world, time, new ConfigurationBuilder().Build(), new SimulationOptions { RealTime = true, PrimeSeconds = 600 });
        plant.TrySetSpeed(10, 5).Should().BeTrue();
        for (var second = 0; second < 30; second++)
        {
            Tick(plant, time);
        }

        return (plant, time);
    }

    private static void Tick(PlantSimulation plant, ManualTime time)
    {
        time.Now = time.Now.AddSeconds(1);
        plant.AdvanceTick();
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MPS.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class ManualTime(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
