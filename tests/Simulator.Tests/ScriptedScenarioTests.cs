// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Hypotheses;
using Mps.Simulator.Demo;
using Mps.Simulator.Fleet;
using Mps.Simulator.Scenarios;
using Mps.Simulator.Sites;
using Mps.Simulator.Telemetry;

namespace Mps.Simulator.Tests;

public sealed class ScriptedScenarioTests
{
    private static readonly FleetCatalog Catalog = FleetCatalogLoader.Load(RepoPaths.FleetDirectory());

    [Fact]
    public void CatalogCoversEachRootCauseClass()
    {
        ScriptedScenarioCatalog.All.Select(scenario => scenario.Id).Should().Equal("SC-1", "SC-2", "SC-3", "SC-4", "SC-6", "SC-5", "SC-7", "SC-8");
        ScriptedScenarioCatalog.All.Select(scenario => scenario.Category).Should().Equal(
            RootCauseCategory.EngineeringChange,
            RootCauseCategory.EquipmentCalibrationOrAlignment,
            RootCauseCategory.SupplyChainMaterial,
            RootCauseCategory.ProcessDrift,
            RootCauseCategory.EquipmentCalibrationOrAlignment,
            RootCauseCategory.SupplyChainMaterial,
            RootCauseCategory.EquipmentCalibrationOrAlignment,
            RootCauseCategory.ProcessDrift);
        var oscilloscope = ScriptedScenarioCatalog.All.Single(scenario => scenario.Id == "SC-6");
        oscilloscope.Parameter.Should().Be("rfSourcePower");
        oscilloscope.Shape.Should().Be(FaultShape.Oscillation);
        oscilloscope.Summary.Should().Contain("oscilloscope");
        ScriptedScenarioCatalog.TryGet("sc-1", out var engineering).Should().BeTrue();
        engineering.ToolIds.Should().Equal("E-02");
        engineering.Summary.Should().Contain("edge ring revision C");
        ScriptedScenarioCatalog.All.Single(scenario => scenario.Id == "SC-2").Summary.Should().Contain("thermocouple");
        ScriptedScenarioCatalog.All.Single(scenario => scenario.Id == "SC-3").ToolIds.Should().Equal("E-01", "E-02");
        var film = ScriptedScenarioCatalog.All.Single(scenario => scenario.Id == "SC-5");
        film.ToolIds.Should().Equal("D-03", "I-D-01");
        film.Parameter.Should().Be("filmThickness");
        film.MagnitudeKind.Should().Be(MagnitudeKind.Percent);
        film.Magnitude.Should().Be(-8d);
        ScriptedScenarioCatalog.All.Single(scenario => scenario.Id == "SC-7").Parameter.Should().Be("vibrationRms");
        ScriptedScenarioCatalog.All.Single(scenario => scenario.Id == "SC-8").ToolIds.Should().Equal("E-03");
        ScriptedScenarioCatalog.TryGet("SC-9", out _).Should().BeFalse();
    }

    [Fact]
    public void PresenterCardsCoverEveryScenario()
    {
        var cards = ScenarioCardCatalog.Load(Path.Combine(RepoPaths.Root(), "data", "scenarios", "catalog.yaml"));

        cards.Cards.Select(card => card.Id).Should().Equal(ScriptedScenarioCatalog.All.Select(scenario => scenario.Id));
        cards.Cards.Should().OnlyContain(card =>
            card.WhatsHappening.Length > 0
            && card.WhatWeSimulate.Length > 0
            && card.WhatAiShouldFind.Length > 0
            && card.ExpectedCause.Length > 0);
        cards.TryGet("SC-5", out var film).Should().BeTrue();
        film.ExpectedCause.Should().Contain("MX4419");
        cards.TryGet("SC-7", out var pump).Should().BeTrue();
        pump.WhatsHappening.Should().Contain("foreline pump");
        cards.TryGet("SC-8", out var recipe).Should().BeTrue();
        recipe.ExpectedCause.Should().Contain("E-03");
    }

    [Fact]
    public void PlantScenariosSampleTheCloneToolsFromTheSameSeed()
    {
        var fleet = PlantFleet();
        foreach (var id in new[] { "SC-5", "SC-7", "SC-8" })
        {
            ScriptedScenarioCatalog.TryGet(id, out var scenario).Should().BeTrue();
            var first = ScriptedScenarioCatalog.Sample(scenario, fleet, DemoRuntime.DefaultSeed);
            var second = ScriptedScenarioCatalog.Sample(scenario, fleet, DemoRuntime.DefaultSeed);
            var other = ScriptedScenarioCatalog.Sample(scenario, fleet, 7);

            first.Should().NotBeEmpty();
            first.Select(point => point.ToolId).Distinct().Should().Equal(scenario.ToolIds);
            second.Select(point => point.Value).Should().Equal(first.Select(point => point.Value));
            other.Select(point => point.Value).Should().NotEqual(first.Select(point => point.Value));
        }

        ScriptedScenarioCatalog.TryGet("SC-5", out var film).Should().BeTrue();
        var faulted = ScriptedScenarioCatalog.Sample(film, fleet, DemoRuntime.DefaultSeed);
        var tool = fleet.GetRequired("D-03");
        var origin = DemoClock.BaselineOrigin;
        var plain = FleetTelemetry.Generate(tool, "D-03-A", "filmThickness", DemoRuntime.DefaultSeed, origin, origin, origin.AddSeconds(ScriptedScenarioCatalog.SampleSeconds));
        faulted.Where(point => point.ToolId == "D-03").Select(point => point.Value).Should().NotEqual(plain.Select(point => point.Value));
    }

    [Theory]
    [InlineData("SC-1")]
    [InlineData("SC-2")]
    [InlineData("SC-3")]
    [InlineData("SC-4")]
    [InlineData("SC-6")]
    public void SameSeedProducesTheSameTelemetry(string id)
    {
        ScriptedScenarioCatalog.TryGet(id, out var scenario).Should().BeTrue();
        var first = ScriptedScenarioCatalog.Sample(scenario, Catalog, DemoRuntime.DefaultSeed);
        var second = ScriptedScenarioCatalog.Sample(scenario, Catalog, DemoRuntime.DefaultSeed);
        var other = ScriptedScenarioCatalog.Sample(scenario, Catalog, 7);

        first.Should().NotBeEmpty();
        second.Select(point => point.Value).Should().Equal(first.Select(point => point.Value));
        second.Select(point => point.At).Should().Equal(first.Select(point => point.At));
        other.Select(point => point.Value).Should().NotEqual(first.Select(point => point.Value));
    }

    [Fact]
    public void ScenariosDifferFromEachOtherAndFromTheUnfaultedSeries()
    {
        var engineering = Sample("SC-1");
        var calibration = Sample("SC-2");
        var material = Sample("SC-3");
        var drift = Sample("SC-4");
        var tool = Catalog.GetRequired("E-02");
        var origin = DemoClock.BaselineOrigin;
        var plain = FleetTelemetry.Generate(tool, "E-02-A", "escTemperature", DemoRuntime.DefaultSeed, origin, origin, origin.AddSeconds(ScriptedScenarioCatalog.SampleSeconds));

        engineering.Select(point => point.Value).Should().NotEqual(plain.Select(point => point.Value));
        calibration.Select(point => point.Value).Should().NotEqual(engineering.Select(point => point.Value));
        material.Should().Contain(point => point.ToolId == "E-01").And.Contain(point => point.ToolId == "E-02");
        drift.Select(point => point.Parameter).Should().OnlyContain(parameter => parameter == "etchRate");
    }

    [Fact]
    public void ResetRewindsTheClockAndDropsInjectedFaults()
    {
        var time = new ManualTime(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero));
        var runtime = new DemoRuntime(Catalog, DemoRuntime.DefaultSeed, time);
        ScriptedScenarioCatalog.TryGet("SC-1", out var scenario).Should().BeTrue();
        runtime.Apply(scenario);
        time.Now = time.Now.AddSeconds(10);
        runtime.Clock.SimulatedNow.Should().BeAfter(DemoClock.BaselineOrigin);
        runtime.Simulator.ActiveFaults.Should().NotBeEmpty();

        runtime.Reset();

        runtime.Clock.SimulatedNow.Should().Be(DemoClock.BaselineOrigin);
        runtime.Simulator.SimulatedNow.Should().Be(DemoClock.BaselineOrigin);
        runtime.Simulator.ActiveFaults.Should().BeEmpty();
        runtime.Seed.Should().Be(DemoRuntime.DefaultSeed);
    }

    private static FleetCatalog PlantFleet()
    {
        var world = SitePlantGenerator.Load(Path.Combine(RepoPaths.Root(), "data"), DemoRuntime.DefaultSeed);
        var tools = world.Sites
            .SelectMany(site => site.Tools)
            .Select(tool => tool.Spec)
            .GroupBy(tool => tool.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return new FleetCatalog(tools);
    }

    private static IReadOnlyList<ScenarioPoint> Sample(string id)
    {
        ScriptedScenarioCatalog.TryGet(id, out var scenario).Should().BeTrue();
        return ScriptedScenarioCatalog.Sample(scenario, Catalog, DemoRuntime.DefaultSeed);
    }

    private sealed class ManualTime : TimeProvider
    {
        public ManualTime(DateTimeOffset now)
        {
            Now = now;
        }

        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow()
        {
            return Now;
        }
    }
}
