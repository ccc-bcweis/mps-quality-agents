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
}
