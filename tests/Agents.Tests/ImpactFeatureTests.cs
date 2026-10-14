// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Impact;
using Mps.Domain.Identity;
using Mps.Domain.Impact;
using Mps.Domain.Telemetry;

namespace Mps.Agents.Tests;

public sealed class SignalShapeClassifierTests
{
    [Fact]
    public void ClassifiesAStep()
    {
        SignalShapeClassifier.Classify([1d, 1d, 1d, 10d, 10d, 10d]).Should().Be(SignalShape.Step);
    }

    [Fact]
    public void ClassifiesARamp()
    {
        SignalShapeClassifier.Classify([1d, 2d, 3d, 4d, 5d, 6d]).Should().Be(SignalShape.Ramp);
    }

    [Fact]
    public void ClassifiesAnOscillation()
    {
        SignalShapeClassifier.Classify([1d, 5d, 1d, 5d, 1d, 5d]).Should().Be(SignalShape.Oscillation);
    }

    [Fact]
    public void ClassifiesAnIntermittentSpike()
    {
        SignalShapeClassifier.Classify([1d, 1d, 8d, 1d, 1d, 1d]).Should().Be(SignalShape.Intermittent);
    }

    [Fact]
    public void ClassifiesAShortOrFlatTraceAsUnknown()
    {
        SignalShapeClassifier.Classify([1d, 2d, 3d]).Should().Be(SignalShape.Unknown);
        SignalShapeClassifier.Classify([2d, 2d, 2d, 2d]).Should().Be(SignalShape.Unknown);
    }

    [Fact]
    public void ClassifiesTelemetryPointsInTimeOrder()
    {
        var tool = new ToolId("MPS-E7");
        var later = new DateTimeOffset(2026, 10, 14, 16, 0, 0, TimeSpan.Zero);
        var earlier = later.AddMinutes(-10);
        TelemetryPoint[] points =
        [
            new(later, 6d),
            new(earlier, 1d),
            new(earlier.AddMinutes(2), 2d),
            new(earlier.AddMinutes(4), 3d),
            new(earlier.AddMinutes(6), 4d),
            new(earlier.AddMinutes(8), 5d),
        ];

        SignalShapeClassifier.Classify(points).Should().Be(SignalShape.Ramp);
    }
}

public sealed class ImpactFeatureCalculatorTests
{
    private static readonly DateTimeOffset Onset = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SharedLotAcrossToolsIsAMaterialLotScope()
    {
        var features = Calculate(
            [Observe("MPS-E7", "E-02", "R1", "LOT-1"), Observe("MPS-E8", "E-01", "R1", "LOT-1")],
            []);

        features.Kind.Should().Be(ImpactScopeKind.MaterialLot);
        features.Key.Should().Be("LOT-1");
        features.ToScope().Kind.Should().Be(ImpactScopeKind.MaterialLot);
    }

    [Fact]
    public void SharedRecipeAcrossToolsIsARecipeScope()
    {
        var features = Calculate(
            [Observe("MPS-E7", "E-02", "RCPT-9", "LOT-A"), Observe("MPS-E8", "E-01", "RCPT-9", "LOT-B")],
            []);

        features.Kind.Should().Be(ImpactScopeKind.Recipe);
        features.Key.Should().Be("RCPT-9");
    }

    [Fact]
    public void ALotSharedByToolsWinsOverASharedRecipe()
    {
        var features = Calculate(
            [Observe("MPS-E7", null, "R1", "LOT-1"), Observe("MPS-E8", null, "R1", "LOT-1")],
            []);

        features.Kind.Should().Be(ImpactScopeKind.MaterialLot);
        features.Key.Should().Be("LOT-1");
    }

    [Fact]
    public void OneToolAndOneChamberIsAChamberScope()
    {
        var features = Calculate([Observe("MPS-E7", "E-02", "R1", "LOT-1")], []);

        features.Kind.Should().Be(ImpactScopeKind.Chamber);
        features.Key.Should().Be("E-02");
    }

    [Fact]
    public void OneToolWithoutAChamberIsAToolScope()
    {
        var features = Calculate([Observe("MPS-E7", null, "R1", "LOT-1")], []);

        features.Kind.Should().Be(ImpactScopeKind.Tool);
        features.Key.Should().Be("MPS-E7");
    }

    [Fact]
    public void AlignmentPicksTheClosestEventAtOrBeforeOnset()
    {
        var features = Calculate(
            [Observe("MPS-E7", "E-02", null, null)],
            [
                new AlignedEvent("pm", "WO-19", Onset.AddDays(-10)),
                new AlignedEvent("eco", "ECO-4471", Onset.AddDays(-2)),
                new AlignedEvent("eco", "ECO-OLD", Onset.AddDays(-20)),
            ]);

        features.AlignedEventAt.Should().Be(Onset.AddDays(-2));
        features.AlignmentNote.Should().Contain("ECO-4471");
    }

    [Fact]
    public void AlignmentIncludesTheWindowEdgesAndIgnoresTheOutside()
    {
        var features = Calculate(
            [Observe("MPS-E7", "E-02", null, null)],
            [
                new AlignedEvent("eco", "EDGE", Onset.AddDays(-14)),
                new AlignedEvent("eco", "OUT", Onset.AddDays(-14).AddSeconds(-1)),
            ]);

        features.AlignedEventAt.Should().Be(Onset.AddDays(-14));
        features.AlignmentNote.Should().Contain("EDGE");
    }

    [Fact]
    public void AlignmentUsesTheNearestLaterEventWhenNonePrecedeOnset()
    {
        var later = Onset.AddHours(12);
        var features = Calculate(
            [Observe("MPS-E7", "E-02", null, null)],
            [
                new AlignedEvent("notice", "SN-4", later),
                new AlignedEvent("notice", "SN-LATE", Onset.AddDays(2)),
            ]);

        features.AlignedEventAt.Should().Be(later);
        features.AlignmentNote.Should().Contain("SN-4");
    }

    [Fact]
    public void EmptyObservationsAreRejected()
    {
        var act = () => ImpactFeatureCalculator.Calculate(Onset, SignalShape.Step, [], []);

        act.Should().Throw<ArgumentException>();
    }

    private static ImpactFeatures Calculate(IReadOnlyList<ImpactObservation> observations, IReadOnlyList<AlignedEvent> events)
    {
        return ImpactFeatureCalculator.Calculate(Onset, SignalShape.Oscillation, observations, events);
    }

    private static ImpactObservation Observe(string tool, string? chamber, string? recipe, string? lot)
    {
        return new ImpactObservation(tool, chamber, recipe, lot);
    }
}
