// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Hypotheses;
using Mps.Simulator.Demo;
using Mps.Simulator.Fleet;
using Mps.Simulator.Scenarios;
using Mps.Simulator.Telemetry;

namespace Mps.Simulator.Tests;

public sealed class ScriptedScenarioTests
{
    private static readonly FleetCatalog Catalog = FleetCatalogLoader.Load(RepoPaths.FleetDirectory());

    [Fact]
    public void CatalogCoversEachRootCauseClass()
    {
        ScriptedScenarioCatalog.All.Select(scenario => scenario.Id).Should().Equal("SC-1", "SC-2", "SC-3", "SC-4");
        ScriptedScenarioCatalog.All.Select(scenario => scenario.Category).Should().Equal(
            RootCauseCategory.EngineeringChange,
            RootCauseCategory.EquipmentCalibrationOrAlignment,
            RootCauseCategory.SupplyChainMaterial,
            RootCauseCategory.ProcessDrift);
        ScriptedScenarioCatalog.TryGet("sc-1", out var engineering).Should().BeTrue();
        engineering.ToolIds.Should().Equal("E-02");
        engineering.Summary.Should().Contain("edge ring revision C");
        ScriptedScenarioCatalog.All.Single(scenario => scenario.Id == "SC-2").Summary.Should().Contain("thermocouple");
        ScriptedScenarioCatalog.All.Single(scenario => scenario.Id == "SC-3").ToolIds.Should().Equal("E-01", "E-02");
        ScriptedScenarioCatalog.TryGet("SC-9", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("SC-1")]
    [InlineData("SC-2")]
    [InlineData("SC-3")]
    [InlineData("SC-4")]
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
