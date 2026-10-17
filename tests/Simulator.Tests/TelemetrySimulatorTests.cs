// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Simulator.Fleet;
using Mps.Simulator.Telemetry;

namespace Mps.Simulator.Tests;

public sealed class TelemetrySimulatorTests
{
    private static readonly DateTimeOffset Origin = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly FleetCatalog Catalog = FleetCatalogLoader.Load(RepoPaths.FleetDirectory());

    [Fact]
    public void SameSeedProducesTheSameSeries()
    {
        var tool = Catalog.GetRequired("E-02");
        var first = FleetTelemetry.Generate(tool, "E-02-A", "escTemperature", 20261014, Origin, Origin, Origin.AddSeconds(40));
        var second = FleetTelemetry.Generate(tool, "E-02-A", "escTemperature", 20261014, Origin, Origin, Origin.AddSeconds(40));
        var otherSeed = FleetTelemetry.Generate(tool, "E-02-A", "escTemperature", 7, Origin, Origin, Origin.AddSeconds(40));

        second.Select(point => point.Value).Should().Equal(first.Select(point => point.Value));
        otherSeed.Select(point => point.Value).Should().NotEqual(first.Select(point => point.Value));
    }

    [Fact]
    public void LiveAdvanceMatchesTheOnDemandSeries()
    {
        const int seed = 20261014;
        var simulator = new FleetSimulator(Catalog, seed, Origin);
        var frames = simulator.Advance(TimeSpan.FromSeconds(25));
        var tool = Catalog.GetRequired("D-01");
        var expected = FleetTelemetry.Generate(tool, "D-01-A", "chamberPressure", seed, Origin, Origin, Origin.AddSeconds(24));
        var actual = frames.Where(frame => frame.ToolId == "D-01" && frame.ChamberId == "D-01-A" && frame.Parameter == "chamberPressure").ToArray();

        actual.Select(frame => frame.Value).Should().Equal(expected.Select(point => point.Value));
        actual.Select(frame => frame.SimulatedAt).Should().Equal(expected.Select(point => point.At));
    }

    [Fact]
    public void SplittingAdvanceDoesNotChangeTheStream()
    {
        var whole = new FleetSimulator(Catalog, 99, Origin).Advance(TimeSpan.FromSeconds(6));
        var splitSimulator = new FleetSimulator(Catalog, 99, Origin);
        var split = splitSimulator.Advance(TimeSpan.FromSeconds(2)).Concat(splitSimulator.Advance(TimeSpan.FromSeconds(4))).ToArray();

        split.Select(frame => (frame.ToolId, frame.ChamberId, frame.Parameter, frame.SimulatedAt, frame.Value))
            .Should()
            .Equal(whole.Select(frame => (frame.ToolId, frame.ChamberId, frame.Parameter, frame.SimulatedAt, frame.Value)));
    }

    [Fact]
    public void EightToolsStreamOnTheChannel()
    {
        var simulator = new FleetSimulator(Catalog, 20261014, Origin);
        var frames = simulator.Advance(TimeSpan.FromSeconds(1));

        frames.Select(frame => frame.ToolId).Distinct().Should().BeEquivalentTo(Catalog.Tools.Select(tool => tool.Id));
        frames.Should().AllSatisfy(frame =>
        {
            frame.RunId.Should().StartWith("RUN-");
            frame.LotId.Should().StartWith("LOT-");
            double.IsFinite(frame.Value).Should().BeTrue();
        });

        var read = new List<string>();
        while (simulator.Frames.TryRead(out var frame))
        {
            read.Add(frame.ToolId);
        }

        read.Distinct().Should().BeEquivalentTo(Catalog.Tools.Select(tool => tool.Id));
    }

    [Fact]
    public void ScaleSixtyMapsOneRealSecondToOneSimulatedMinute()
    {
        var real = new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
        var clock = new AcceleratedClock(Origin, 60d, new FixedTime(real));
        var moved = new FixedTime(real);
        var started = new AcceleratedClock(Origin, 60d, moved);
        started.SimulatedNow.Should().Be(Origin);
        moved.Now = real.AddSeconds(1);

        started.SimulatedNow.Should().Be(Origin.AddMinutes(1));
        clock.Scale.Should().Be(60d);
    }

    [Fact]
    public void StepFaultShiftsOnlyAfterOnsetAndDoesNotChangeTheNoise()
    {
        var tool = Catalog.GetRequired("E-02");
        var onset = Origin.AddSeconds(15);
        var fault = new FaultSignal("E-02", "E-02-A", "escTemperature", FaultShape.Step, MagnitudeKind.Sigma, 4d, onset);
        var plain = FleetTelemetry.Generate(tool, "E-02-A", "escTemperature", 11, Origin, Origin, Origin.AddSeconds(30));
        var faulted = FleetTelemetry.Generate(tool, "E-02-A", "escTemperature", 11, Origin, Origin, Origin.AddSeconds(30), [fault]);
        var window = RecipeSchedule.Resolve(RecipeSchedule.Locate(tool, 20), "escTemperature");

        for (var index = 0; index < plain.Count; index++)
        {
            var delta = faulted[index].Value - plain[index].Value;
            if (plain[index].At < onset)
            {
                delta.Should().Be(0d);
            }
            else
            {
                delta.Should().BeApproximately(4d * window.Sigma, 1e-9d);
            }
        }
    }

    [Fact]
    public void SimulatorEmitsExactlyTheCatalogSensorIdsAndUnits()
    {
        var simulator = new FleetSimulator(Catalog, 20261014, Origin);
        var cycle = Catalog.Tools.Max(tool => tool.Recipes.Sum(recipe => recipe.Steps.Count)) * RecipeSchedule.StepSeconds;
        var frames = simulator.Advance(TimeSpan.FromSeconds(cycle));

        foreach (var tool in Catalog.Tools)
        {
            var byId = tool.Sensors.ToDictionary(sensor => sensor.Id, StringComparer.Ordinal);
            var emitted = frames.Where(frame => frame.ToolId == tool.Id).ToArray();
            emitted.Select(frame => frame.SensorId).Distinct().Should().BeEquivalentTo(byId.Keys);
            emitted.Should().OnlyContain(frame =>
                byId.ContainsKey(frame.SensorId)
                && frame.Unit == byId[frame.SensorId].Unit
                && frame.Parameter == (byId[frame.SensorId].Parameter ?? frame.SensorId)
                && frame.ChamberId == byId[frame.SensorId].FrameChamberId(tool.Id));
        }
    }

    [Fact]
    public void ToolOutputCountsWafersInsideTheSimulatedHour()
    {
        var simulator = new FleetSimulator(Catalog, 20261014, Origin);
        var frames = simulator.Advance(TimeSpan.FromSeconds(RecipeSchedule.WaferSeconds + 1));
        var output = frames.Where(frame => frame.SensorId == "E-02-OUTPUT").OrderBy(frame => frame.SimulatedAt).ToArray();

        output[0].Value.Should().Be(0d);
        output[0].Unit.Should().Be("wafers");
        output[^1].Value.Should().Be(1d);
    }

    [Fact]
    public void TelemetryFramesDoNotCarryARootCause()
    {
        typeof(TelemetryFrame).GetProperties().Select(property => property.Name)
            .Should()
            .NotContain(name => name.Contains("Cause", StringComparison.Ordinal) || name.Contains("Ground", StringComparison.Ordinal));
        typeof(FaultSignal).GetProperties().Select(property => property.Name)
            .Should()
            .NotContain(name => name.Contains("Cause", StringComparison.Ordinal) || name.Contains("Category", StringComparison.Ordinal));
    }

    private sealed class FixedTime : TimeProvider
    {
        public FixedTime(DateTimeOffset now)
        {
            Now = now;
        }

        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
