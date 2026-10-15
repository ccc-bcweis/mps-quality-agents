// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Simulator.Fleet;
using Mps.Simulator.Sites;
using Mps.Simulator.Telemetry;

namespace Mps.Simulator.Tests;

public sealed class LiveSimulationTests
{
    private static readonly FleetCatalog Catalog = FleetCatalogLoader.Load(RepoPaths.FleetDirectory());

    private static readonly DateTimeOffset Origin = DemoClock.BaselineOrigin;

    [Fact]
    public void ADecimatedStrideMatchesTheOneHertzSeries()
    {
        var full = new FleetSimulator(Catalog, 4242, Origin);
        var thin = new FleetSimulator(Catalog, 4242, Origin);

        var every = Series(full.Advance(TimeSpan.FromSeconds(30), 1));
        var kept = Series(thin.Advance(TimeSpan.FromSeconds(30), 15));
        var expected = every.Where(frame => Keep(frame.SimulatedAt)).ToArray();

        kept.Select(frame => frame.SimulatedAt).Should().Equal(expected.Select(frame => frame.SimulatedAt));
        kept.Select(frame => frame.Value).Should().Equal(expected.Select(frame => frame.Value));
        kept.Should().HaveCount(2);
    }

    [Fact]
    public void TheSameSeedProducesTheSameDecimatedStream()
    {
        var first = Series(new FleetSimulator(Catalog, 4242, Origin).Advance(TimeSpan.FromSeconds(45), 15));
        var second = Series(new FleetSimulator(Catalog, 4242, Origin).Advance(TimeSpan.FromSeconds(45), 15));
        var other = Series(new FleetSimulator(Catalog, 7, Origin).Advance(TimeSpan.FromSeconds(45), 15));

        second.Select(frame => frame.Value).Should().Equal(first.Select(frame => frame.Value));
        other.Select(frame => frame.Value).Should().NotEqual(first.Select(frame => frame.Value));
    }

    [Fact]
    public void AFaultChangesOnlyTheInjectedSeries()
    {
        var plain = new FleetSimulator(Catalog, 4242, Origin);
        var faulted = new FleetSimulator(Catalog, 4242, Origin);
        plain.Advance(TimeSpan.FromSeconds(30), 15);
        faulted.Advance(TimeSpan.FromSeconds(30), 15);
        var onset = faulted.SimulatedNow;
        faulted.Inject(new FaultSignal("E-02", "E-02-A", "escTemperature", FaultShape.Step, MagnitudeKind.Sigma, 4d, onset));

        var healthyFrames = plain.Advance(TimeSpan.FromSeconds(30), 15);
        var shiftedFrames = faulted.Advance(TimeSpan.FromSeconds(30), 15);
        var healthy = Series(healthyFrames).Where(frame => frame.SimulatedAt >= onset).ToArray();
        var shifted = Series(shiftedFrames).Where(frame => frame.SimulatedAt >= onset).ToArray();
        var pressure = Pressure(healthyFrames);
        var faultedPressure = Pressure(shiftedFrames);

        shifted.Should().NotBeEmpty();
        shifted.Zip(healthy).Should().OnlyContain(pair => pair.First.Value > pair.Second.Value + 1d);
        faultedPressure.Select(frame => frame.Value).Should().Equal(pressure.Select(frame => frame.Value));
    }

    [Fact]
    public void ResetReturnsTheStreamToNominal()
    {
        var plant = new FleetSimulator(Catalog, 4242, Origin);
        var clean = new FleetSimulator(Catalog, 4242, Origin);
        plant.Advance(TimeSpan.FromSeconds(30), 15);
        plant.Inject(new FaultSignal("E-02", "E-02-A", "escTemperature", FaultShape.Step, MagnitudeKind.Sigma, 4d, plant.SimulatedNow));
        var shifted = Series(plant.Advance(TimeSpan.FromSeconds(30), 15)).Select(frame => frame.Value).ToArray();

        plant.Reset();
        var restored = Series(plant.Advance(TimeSpan.FromSeconds(30), 15)).Select(frame => frame.Value).ToArray();
        var nominal = Series(clean.Advance(TimeSpan.FromSeconds(30), 15)).Select(frame => frame.Value).ToArray();

        shifted.Should().NotEqual(nominal);
        restored.Should().Equal(nominal);
        plant.ActiveFaults.Should().BeEmpty();
        plant.SimulatedNow.Should().Be(Origin.AddSeconds(30));
    }

    [Fact]
    public void EveryPlantEmitsTelemetryForItsTools()
    {
        var world = SitePlantGenerator.Load(Path.Combine(RepoPaths.Root(), "data"), 4242);
        var tools = world.Sites.SelectMany(site => site.Tools).Select(tool => tool.Spec).ToArray();
        var simulator = new FleetSimulator(new FleetCatalog(tools), 4242, Origin);
        var frames = simulator.Advance(TimeSpan.FromSeconds(15), 15);

        world.Sites.Select(site => site.Id).Should().Equal("northwater", "ironwood", "lumen");
        frames.Should().Contain(frame => frame.ToolId == "D-03" && frame.ChamberId == "D-03-A" && frame.Parameter == "filmThickness");
        frames.Should().Contain(frame => frame.ToolId == "I-D-01" && frame.ChamberId == "I-D-01-A" && frame.Parameter == "filmThickness");
        frames.Should().Contain(frame => frame.ToolId == "P-D-01" && frame.ChamberId == "P-D-01-A" && frame.Parameter == "filmThickness");
    }

    private static bool Keep(DateTimeOffset at)
    {
        var second = (int)(at - Origin).TotalSeconds;
        return second == 29 || (second + 1) % 15 == 0;
    }

    private static TelemetryFrame[] Series(IEnumerable<TelemetryFrame> frames) =>
        frames.Where(frame => frame.ToolId == "E-02" && frame.ChamberId == "E-02-A" && frame.Parameter == "escTemperature").ToArray();

    private static TelemetryFrame[] Pressure(IEnumerable<TelemetryFrame> frames) =>
        frames.Where(frame => frame.ToolId == "E-02" && frame.ChamberId == "E-02-A" && frame.Parameter == "chamberPressure").ToArray();
}
