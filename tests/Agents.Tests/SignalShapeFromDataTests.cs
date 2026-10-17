// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Evidence;
using Mps.Agents.Impact;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Telemetry;

namespace Mps.Agents.Tests;

public sealed class SignalShapeFromDataTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 16, 20, 0, 0, TimeSpan.Zero);

    private static readonly double[] Noises = NoiseSeries(new Random(4242), 600);

    private static double Noise(int i) => Noises[i % Noises.Length];

    private static double[] NoiseSeries(Random random, int count) =>
        Enumerable.Range(0, count).Select(_ => (random.NextDouble() - 0.5d) / 5d).ToArray();

    [Fact]
    public void PlainNoiseIsAlmostNeverCalledAnOscillation()
    {
        var random = new Random(7);
        var hits = Enumerable.Range(0, 500).Count(_ => SignalShapeClassifier.Classify(NoiseSeries(random, 40)) == SignalShape.Oscillation);

        hits.Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public void ANoisyOscillationIsAnOscillationNotARamp()
    {
        // Chamber pressure swinging ±0.4 around 3.5 with a 2-minute period, sampled every 15 seconds, plus noise.
        var values = Enumerable.Range(0, 40).Select(i => 3.5d + (0.4d * Math.Sin(2 * Math.PI * i / 8d)) + Noise(i)).ToArray();

        SignalShapeClassifier.Classify(values).Should().Be(SignalShape.Oscillation);
    }

    [Fact]
    public void ANoisyRampAndANoisyStepKeepTheirShapes()
    {
        SignalShapeClassifier.Classify(Enumerable.Range(0, 40).Select(i => (i * 0.1d) + (Noise(i) / 4)).ToArray()).Should().Be(SignalShape.Ramp);
        SignalShapeClassifier.Classify(Enumerable.Range(0, 40).Select(i => (i < 20 ? 0d : 5d) + (Noise(i) / 4)).ToArray()).Should().Be(SignalShape.Step);
    }

    [Fact]
    public void TheEvidenceReadsTheShapeSinceTheFirstRuleBreakAndSaysItInWords()
    {
        // Two hours of normal readings, then ten minutes of oscillation: the whole window looks flat, the recent part swings.
        var points = new List<TelemetryPoint>();
        for (var i = 0; i < 480; i++)
        {
            points.Add(new TelemetryPoint(Start.AddSeconds(15 * i), 3.5d + (Noise(i) / 10)));
        }

        for (var i = 0; i < 40; i++)
        {
            points.Add(new TelemetryPoint(Start.AddSeconds(15 * (480 + i)), 3.5d + (0.6d * Math.Sin(2 * Math.PI * i / 8d)) + (Noise(i) / 10)));
        }

        var window = new TelemetryWindow(new ToolId("D-01"), "chamberPressure", Start, points[^1].At, points);
        SpcViolation[] violations = [new SpcViolation("western-electric-1", new ToolId("D-01"), "D-01-A", "chamberPressure", Start.AddSeconds(15 * 482), 4.1d, 0.1d, Severity.High)];

        var shape = EvidencePackFactory.ShapeOf(window, violations);

        shape.Should().Be(SignalShape.Oscillation);
        EvidencePackFactory.DescribeWindow(window, shape).Should().Contain("oscillation").And.NotContain("gradual");
    }

    // Live readings from the owner's run (D-01 chamber pressure, 2026-10-16 16:35-16:50 ET): a recipe step down on
    // both chambers, then an injected oscillation on chamber A only.
    private static readonly double[] LiveA = [3.467d, 3.532d, 3.471d, 3.531d, 3.422d, 3.457d, 3.487d, 3.51d, 3.541d, 3.592d, 3.592d, 3.573d, 3.635d, 3.666d, 3.645d, 3.576d, 3.603d, 3.461d, 3.553d, 3.498d, 3.456d, 3.535d, 3.453d, 3.499d, 3.039d, 3.12d, 3.044d, 2.99d, 2.965d, 2.984d, 2.982d, 2.995d, 2.922d, 2.968d, 2.959d, 3.022d, 2.978d, 3.081d, 3.004d, 2.991d, 3.021d, 2.993d, 3.051d, 2.945d, 3.001d, 2.958d, 3.388d, 3.552d, 4.458d, 4.435d, 4.128d, 3.807d, 3.435d, 2.953d, 2.575d, 2.516d, 2.715d, 2.955d, 3.472d, 3.889d, 4.307d, 4.48d, 4.565d, 4.357d, 3.883d, 3.54d, 3.194d, 2.738d];

    private static readonly double[] LiveB = [3.487d, 3.443d, 3.58d, 3.467d, 3.486d, 3.439d, 3.471d, 3.547d, 3.635d, 3.54d, 3.626d, 3.583d, 3.609d, 3.537d, 3.61d, 3.618d, 3.476d, 3.511d, 3.497d, 3.427d, 3.56d, 3.483d, 3.531d, 3.438d, 3.025d, 3.006d, 3.033d, 2.987d, 2.984d, 2.986d, 3.079d, 3.018d, 2.961d, 3.031d, 3.036d, 3.097d, 3.012d, 2.928d, 3.039d, 2.979d, 2.968d, 2.963d, 3.06d, 3.018d, 3.097d, 3.005d, 3.081d, 3.047d, 3.482d, 3.436d, 3.376d, 3.56d, 3.482d, 3.473d, 3.478d, 3.489d, 3.58d, 3.581d, 3.551d, 3.53d, 3.596d, 3.58d, 3.649d, 3.622d, 3.468d, 3.506d, 3.498d, 3.479d];

    [Fact]
    public void TheLiveOscillationIsSeenOnceItHasSwungTwice()
    {
        var early = LiveA.Take(60).ToArray();
        SignalShapeClassifier.IsSwinging(early.Skip(early.Length - 24).ToArray(), early).Should().BeTrue();
        SignalShapeClassifier.IsSwinging(LiveA.Skip(LiveA.Length - 24).ToArray(), LiveA).Should().BeTrue();
    }

    [Fact]
    public void ARecipeStepOrOneSidedSpikesAreNotSwings()
    {
        SignalShapeClassifier.IsSwinging(LiveB.Skip(LiveB.Length - 24).ToArray(), LiveB).Should().BeFalse();
        var spikes = LiveB.Select((value, index) => index is 60 or 64 ? value + 1.5d : value).ToArray();
        SignalShapeClassifier.IsSwinging(spikes.Skip(spikes.Length - 24).ToArray(), spikes).Should().BeFalse();
    }

    [Fact]
    public void TheShapeIsReadAgainstTheRecipeNominalSoAStepChangeIsNotTheShape()
    {
        // D-01 chamber A: recipe nominal steps 3.5 -> 3.0 at reading 24, then the injected oscillation starts at 46.
        var points = LiveA.Select((value, i) => new TelemetryPoint(Start.AddSeconds(15 * i), value)).ToArray();
        var nominals = LiveA.Select((_, i) => i < 24 ? 3.5d : 3.0d).ToArray();
        var window = new TelemetryWindow(new ToolId("D-01"), "chamberPressure", Start, points[^1].At, points) { Nominals = nominals };
        SpcViolation[] violations = [new SpcViolation("western-electric-1", new ToolId("D-01"), "D-01-A", "chamberPressure", Start.AddSeconds(15 * 48), 4.458d, 0.25d, Severity.High)];

        EvidencePackFactory.ShapeOf(window, violations).Should().Be(SignalShape.Oscillation);
    }

    [Fact]
    public void ShapeWaitIsLoadedFromMetadata()
    {
        var wait = Mps.Agents.Investigation.ShapeWait.Load(Path.Combine(RepoRoot(), "data", "metadata", "investigation.yaml"));

        wait.Attempts.Should().BeGreaterThan(0);
        wait.Delay.Should().BePositive();
    }

    [Fact]
    public void TheInvestigationWaitsForEnoughReadingsSinceOnsetBeforeJudgingTheShape()
    {
        // Live D-01-A at the moment the first investigation ran: half a cycle after the first rule break.
        var wait = new Mps.Agents.Investigation.ShapeWait(30, TimeSpan.FromSeconds(20), 20);
        var points = LiveA.Select((value, i) => new TelemetryPoint(Start.AddSeconds(15 * i), value)).ToArray();
        var nominals = LiveA.Select((_, i) => i < 24 ? 3.5d : 3.0d).ToArray();
        SpcViolation[] violations = [new SpcViolation("western-electric-1", new ToolId("D-01"), "D-01-A", "chamberPressure", Start.AddSeconds(15 * 48), 4.458d, 0.25d, Severity.High)];

        var early = new TelemetryWindow(new ToolId("D-01"), "chamberPressure", Start, points[55].At, points.Take(56).ToArray()) { Nominals = nominals.Take(56).ToArray() };
        var full = new TelemetryWindow(new ToolId("D-01"), "chamberPressure", Start, points[^1].At, points) { Nominals = nominals };

        wait.NeedsMoreReadings(early, violations).Should().BeTrue();
        wait.NeedsMoreReadings(full, violations).Should().BeFalse();
        EvidencePackFactory.ShapeOf(full, violations).Should().Be(SignalShape.Oscillation);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MPS.sln")))
        {
            dir = dir.Parent;
        }

        return dir!.FullName;
    }
}
