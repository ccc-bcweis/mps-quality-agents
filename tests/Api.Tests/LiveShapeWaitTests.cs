// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Mps.Agents.Evidence;
using Mps.Agents.Impact;
using Mps.Agents.Investigation;
using Mps.Api.Live;
using Mps.Domain.Identity;
using Mps.Domain.Telemetry;
using Mps.Simulator.Scenarios;
using Mps.Simulator.Sites;

namespace Mps.Api.Tests;

/// <summary>
/// Regression: at 10x the SC-3 gas-flow step stayed "no clear shape" for every one of the 30 wait attempts,
/// so triage showed Running for ten minutes and the shape the AI was handed did not match the readings.
/// </summary>
public sealed class LiveShapeWaitTests
{
    private static readonly ShapeWait Wait = ShapeWait.Load(Path.Combine(Root(), "data", "metadata", "investigation.yaml"));

    [Theory]
    [InlineData("SC-3", SignalShape.Step)]
    [InlineData("SC-5", SignalShape.Step)]
    [InlineData("SC-2", SignalShape.Ramp)]
    [InlineData("SC-7", SignalShape.Ramp)]
    [InlineData("SC-1", SignalShape.Oscillation)]
    public void AtTenTimesTheShapeIsClearWellBeforeTheWaitRunsOut(string scenarioId, SignalShape expected)
    {
        var (plant, time, scenario) = Start(scenarioId);
        var opened = time.Now.AddSeconds(3);
        var budget = (int)(Wait.Attempts * Wait.Delay.TotalSeconds);
        var settledAt = -1;
        for (var second = 1; second <= budget / 2; second++)
        {
            Tick(plant, time);
            if (second % (int)Wait.Delay.TotalSeconds != 0)
            {
                continue;
            }

            var (window, violations) = Read(plant, scenario, opened);
            if (!Wait.NeedsMoreReadings(window, violations, opened))
            {
                settledAt = second;
                EvidencePackFactory.ShapeOf(window, violations).Should().Be(expected);
                break;
            }
        }

        settledAt.Should().BePositive($"{scenarioId} must leave the shape wait within half its budget");
    }

    [Fact]
    public void AnUnclearShapeStopsWaitingOnceEnoughReadingsHaveArrived()
    {
        var at = DateTimeOffset.UnixEpoch;
        var flat = Enumerable.Range(0, 200).Select(index => new TelemetryPoint(at.AddSeconds(index), 10d)).ToArray();
        var window = new TelemetryWindow(new ToolId("E-01"), "cf4Flow", flat[0].At, flat[^1].At, flat);
        var wait = new ShapeWait(30, TimeSpan.FromSeconds(20), 20, 60);

        wait.NeedsMoreReadings(window, [], at.AddSeconds(150)).Should().BeTrue("50 readings since onset is under the cap");
        wait.NeedsMoreReadings(window, [], at.AddSeconds(100)).Should().BeFalse("100 readings since onset is past the cap");
        (wait with { MaxReadingsSinceOnset = 0 }).NeedsMoreReadings(window, [], at.AddSeconds(100)).Should().BeTrue("no cap keeps waiting");
    }

    [Fact]
    public async Task AfterStopScenarioTheInvestigationStillReadsTheIncidentChamber()
    {
        var (plant, time, scenario) = Start("SC-3");
        for (var second = 0; second < 60; second++)
        {
            Tick(plant, time);
        }

        plant.ClearAll();
        var chamber = scenario.ToolIds[0] + "-A";
        var direct = plant.ReadParameter(scenario.ToolIds[0], scenario.Parameter, chamber, DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
        var tools = new DemoSignalTools(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), new CrossSiteMaterialCatalog(Path.Combine(Root(), "data", "seed", "enterprise.json")), plant);
        var window = await tools.GetWindowAsync(new ToolId(scenario.ToolIds[0]), chamber, scenario.Parameter, DateTimeOffset.MinValue, DateTimeOffset.MaxValue, CancellationToken.None);
        window.Points.Select(point => point.Value).Should().Equal(direct.Select(point => point.Value));
        window.Points.Min(point => point.Value).Should().BeLessThan(plant.WindowAt(scenario.ToolIds[0], scenario.Parameter, window.Points[^1].At)!.Minimum, "the step on the incident chamber is still in the readings");
    }

    private static (PlantSimulation Plant, ManualTime Time, ScriptedScenario Scenario) Start(string scenarioId)
    {
        var time = new ManualTime(DateTimeOffset.Parse("2026-10-17T06:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        var world = SitePlantGenerator.Load(Path.Combine(Root(), "data"), 4242);
        var plant = new PlantSimulation(world, time, new ConfigurationBuilder().Build(), new SimulationOptions { RealTime = true, PrimeSeconds = 600 });
        plant.TrySetSpeed(10, 15).Should().BeTrue();
        for (var second = 0; second < 60; second++)
        {
            Tick(plant, time);
        }

        ScriptedScenarioCatalog.TryGet(scenarioId, out var scenario).Should().BeTrue();
        plant.ApplyStream(scenario);
        return (plant, time, scenario);
    }

    private static void Tick(PlantSimulation plant, ManualTime time)
    {
        time.Now = time.Now.AddSeconds(1);
        plant.AdvanceTick();
    }

    private static (TelemetryWindow Window, IReadOnlyList<SpcViolation> Violations) Read(PlantSimulation plant, ScriptedScenario scenario, DateTimeOffset opened)
    {
        var tool = scenario.ToolIds[0];
        var from = opened.AddDays(-14);
        var until = opened.AddHours(1);
        var points = plant.ReadParameter(tool, scenario.Parameter, null, from, until);
        var window = new TelemetryWindow(new ToolId(tool), scenario.Parameter, points[0].At, points[^1].At, points)
        {
            Nominals = points.Select(point => plant.WindowAt(tool, scenario.Parameter, point.At)?.Nominal ?? double.NaN).ToArray(),
        };
        return (window, plant.Evaluate(tool, scenario.Parameter, from, until));
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
