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
    public void AStepInNormalSensorNoiseIsAStep()
    {
        // Regression: a 4.5-sigma step in ordinary noise never had one clean jump, so triage waited for a shape that never came.
        var random = new Random(11);
        var gaussian = Enumerable.Range(0, 120).Select(_ => Math.Sqrt(-2d * Math.Log(1d - random.NextDouble())) * Math.Cos(2d * Math.PI * random.NextDouble())).ToArray();
        var values = gaussian.Select((noise, i) => (i < 90 ? 50d : 45.5d) + noise).ToArray();

        SignalShapeClassifier.Classify(values).Should().Be(SignalShape.Step);
    }

    [Fact]
    public void ARampThatLevelsOffIsNotCalledAStep()
    {
        var values = Enumerable.Range(0, 200).Select(i => Math.Min(i < 60 ? 0d : (i - 60) * 0.05d, 3d) + Noise(i)).ToArray();

        SignalShapeClassifier.Classify(values).Should().NotBe(SignalShape.Step);
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

    [Fact]
    public void AnEarlyLowSeverityTrendBreakDoesNotCutTheWaitShort()
    {
        // Live D-01-A: a Low Nelson trend break 9 minutes before the fault, then the oscillation's rule 1 break.
        var wait = new Mps.Agents.Investigation.ShapeWait(30, TimeSpan.FromSeconds(20), 20);
        var points = LiveA.Select((value, i) => new TelemetryPoint(Start.AddSeconds(15 * i), value)).ToArray();
        var nominals = LiveA.Select((_, i) => i < 24 ? 3.5d : 3.0d).ToArray();
        var early = new TelemetryWindow(new ToolId("D-01"), "chamberPressure", Start, points[55].At, points.Take(56).ToArray()) { Nominals = nominals.Take(56).ToArray() };
        SpcViolation[] violations =
        [
            new SpcViolation("nelson-6", new ToolId("D-01"), "D-01-A", "chamberPressure", Start.AddSeconds(15 * 10), 3.59d, 0.25d, Severity.Low),
            new SpcViolation("western-electric-1", new ToolId("D-01"), "D-01-A", "chamberPressure", Start.AddSeconds(15 * 48), 4.458d, 0.25d, Severity.High),
        ];

        wait.NeedsMoreReadings(early, violations).Should().BeTrue();
    }

    // Live E-02-A ESC temperature at 10x plant speed (2026-10-16 23:38-23:54 ET): 42 readings before the first rule
    // break, then one reading a second through a 20-second oscillation.
    private static readonly double[] TenX = [48.99d, 48.73d, 48.66d, 49.45d, 48.57d, 49.3d, 48.71d, 49.14d, 48.88d, 49.04d, 48.96d, 48.11d, 49.1d, 49.09d, 48.91d, 49.53d, 49.14d, 48.5d, 50.0d, 49.98d, 50.39d, 49.89d, 49.94d, 49.9d, 50.64d, 49.95d, 52.14d, 52.44d, 52.65d, 52.56d, 52.35d, 52.11d, 51.89d, 51.93d, 51.86d, 52.09d, 51.86d, 51.66d, 51.91d, 53.28d, 54.34d, 55.29d, 55.96d, 56.45d, 56.23d, 55.33d, 54.49d, 53.31d, 51.92d, 50.72d, 49.25d, 48.19d, 47.23d, 47.03d, 47.12d, 48.02d, 49.03d, 50.13d, 51.79d, 53.22d, 54.57d, 55.53d, 56.58d, 56.74d, 56.5d, 56.12d, 54.94d, 53.55d, 51.99d, 50.28d, 48.96d, 48.26d, 47.73d, 47.57d, 47.73d, 48.22d, 49.27d, 50.52d, 51.92d, 53.32d, 54.62d, 55.76d, 56.35d, 56.53d, 56.0d, 55.36d, 54.68d, 53.44d, 51.85d, 50.42d, 49.19d, 48.13d, 47.6d, 47.63d, 47.87d, 48.3d, 49.43d, 50.39d, 51.88d, 53.59d, 55.06d, 56.07d, 56.65d, 56.76d, 56.54d, 55.9d, 54.82d, 53.52d, 52.24d, 50.77d, 49.35d, 48.54d, 47.97d, 47.31d, 47.49d, 48.17d, 49.14d, 50.22d, 51.64d, 52.98d, 54.27d, 55.34d, 55.93d, 56.17d, 56.27d, 55.84d, 54.91d, 53.43d, 52.1d, 50.42d, 49.12d, 48.33d, 47.91d, 47.53d, 47.72d, 48.55d, 49.53d, 51.05d, 52.41d, 51.81d, 52.93d, 53.8d, 54.6d, 54.69d, 54.56d, 53.83d, 52.86d, 51.6d, 50.18d, 48.61d, 47.4d, 46.51d, 45.66d, 45.46d, 45.79d, 46.5d, 47.49d, 48.74d, 50.02d, 51.46d, 52.52d, 53.4d, 54.4d, 54.73d, 54.74d, 53.88d, 52.78d, 50.99d, 49.94d, 48.78d, 47.45d, 46.38d, 45.58d, 45.4d, 45.62d, 46.27d, 47.2d, 48.4d, 49.98d, 51.25d, 52.56d, 53.49d, 54.21d, 54.55d, 54.48d, 53.73d, 52.7d, 51.2d, 50.11d, 48.93d, 47.35d, 46.43d, 45.74d, 45.42d, 45.7d, 46.37d, 47.34d, 48.59d, 50.11d, 51.51d, 52.85d, 53.87d, 54.56d, 54.88d, 54.53d, 54.05d, 52.7d, 51.72d, 50.34d, 48.64d, 46.99d, 45.72d, 45.3d, 44.99d, 45.38d, 46.28d, 47.46d, 48.66d, 50.58d, 51.95d, 53.16d, 53.96d, 54.64d, 54.97d, 54.9d, 54.15d, 53.02d, 51.68d, 50.02d, 48.68d, 47.31d, 46.45d, 45.54d, 45.38d, 45.72d, 46.38d, 47.29d, 48.43d, 49.76d, 51.29d, 52.5d, 53.66d, 54.3d, 54.32d, 53.97d, 53.52d, 52.57d, 51.12d, 49.93d, 48.54d, 47.01d, 46.15d, 45.49d, 45.01d, 45.14d, 45.81d, 47.15d, 48.41d, 49.48d, 50.19d, 51.96d, 53.35d, 53.92d, 54.41d, 54.38d, 53.43d, 52.1d, 50.7d, 49.01d, 47.33d, 45.58d, 44.61d, 43.6d, 43.15d, 43.42d, 44.05d, 45.13d, 46.81d, 48.63d, 50.71d, 51.98d, 53.14d, 54.19d, 54.37d, 54.1d, 53.53d, 52.27d, 50.58d, 48.67d, 46.84d, 45.22d, 43.83d, 42.91d, 42.91d, 43.23d, 44.04d, 45.52d, 47.19d, 49.09d, 50.87d, 52.45d, 53.47d, 54.47d, 54.47d, 54.2d, 53.5d, 52.01d, 50.83d, 49.3d, 47.64d, 45.85d, 44.44d, 43.58d, 43.22d, 43.6d, 44.29d, 45.48d, 47.06d, 48.89d, 50.79d, 52.32d, 53.7d, 54.28d, 54.19d, 54.13d, 53.12d, 51.74d, 50.45d, 48.89d, 47.25d, 45.46d, 44.02d, 43.29d, 42.98d, 43.62d, 44.04d, 45.51d, 47.03d, 48.68d, 50.28d, 52.2d, 53.31d, 53.98d, 54.41d, 54.1d, 53.37d, 51.89d, 50.87d, 49.18d, 47.4d, 45.35d, 44.07d, 43.18d, 42.98d, 43.23d, 43.99d, 45.44d, 46.97d, 48.74d, 50.66d, 52.36d, 53.7d, 54.86d, 54.75d, 54.32d, 53.71d, 52.24d, 50.26d, 48.42d, 46.66d, 45.15d, 43.94d, 43.18d, 42.73d, 43.4d, 44.23d, 45.4d, 46.91d, 48.81d, 50.56d, 52.49d, 54.11d, 54.94d, 55.39d, 54.84d, 54.03d, 52.64d, 51.15d, 49.55d, 47.66d, 45.91d, 44.51d, 43.91d, 43.6d, 43.98d, 44.64d, 45.85d, 47.56d, 49.28d, 51.43d, 52.83d, 53.95d, 54.89d, 54.9d, 54.73d, 54.32d, 52.62d, 50.9d, 48.81d, 47.09d, 45.58d, 44.05d, 43.2d, 42.95d, 43.48d, 44.11d, 45.41d, 47.48d, 49.3d, 50.9d, 52.44d, 53.86d, 54.57d, 54.48d, 53.91d, 53.25d, 52.07d, 50.43d, 48.51d, 46.64d, 45.43d, 44.13d, 43.39d, 43.37d, 43.52d, 44.54d, 45.83d, 47.24d, 49.13d, 51.08d, 52.53d, 53.47d, 54.18d, 54.58d, 54.12d, 53.35d, 52.22d, 50.36d, 48.57d, 46.82d, 45.5d, 44.4d, 43.35d, 43.12d, 43.12d, 43.9d, 45.37d, 47.3d, 49.16d, 50.89d, 52.76d, 54.19d, 55.01d, 55.32d, 55.21d, 54.39d, 52.95d, 51.13d, 49.37d, 47.33d, 45.53d, 44.3d, 43.35d, 43.08d, 43.52d, 44.19d, 45.5d, 47.1d, 49.11d, 51.07d, 52.67d, 53.64d, 54.36d, 54.59d, 54.33d, 53.42d, 52.11d, 50.34d, 48.6d, 46.74d, 45.24d, 43.35d, 42.57d, 42.36d, 42.83d, 43.72d, 45.27d, 46.99d, 48.76d, 50.44d, 52.02d, 53.3d, 54.74d, 55.08d, 54.56d, 53.58d, 52.16d, 50.62d, 48.81d, 46.92d, 45.43d, 44.54d, 43.53d, 43.34d, 43.61d, 44.33d, 45.98d, 47.7d, 49.12d, 50.77d, 52.71d, 54.08d, 54.63d, 55.08d, 54.65d, 53.78d, 52.18d, 50.26d, 48.4d, 46.96d, 45.1d, 43.89d, 43.58d, 43.12d, 43.06d, 43.82d, 45.21d, 46.96d, 49.04d, 50.79d, 52.46d, 53.54d, 54.57d, 54.61d, 54.59d, 54.18d, 52.96d, 51.48d, 49.68d, 47.75d, 46.02d, 44.67d, 44.02d, 43.32d, 43.78d, 44.64d, 45.72d, 46.99d, 48.91d, 50.64d, 52.25d, 53.37d, 54.19d, 54.55d, 54.15d, 53.2d, 52.07d, 50.82d, 48.56d, 46.89d, 45.39d, 44.12d, 43.45d, 43.1d, 43.51d, 44.08d, 44.94d, 46.82d, 48.58d, 50.57d, 52.38d, 53.3d, 54.31d, 54.75d, 54.28d, 53.38d, 52.11d, 50.44d, 48.72d, 46.98d, 45.32d, 44.0d, 43.17d, 42.99d, 43.03d, 43.92d, 45.42d, 47.17d, 48.92d, 50.68d];

    [Fact]
    public void AtTenTimesSpeedTheOscillationIsJudgedAgainstTheReadingsBeforeTheBreak()
    {
        var points = TenX.Select((value, i) => new TelemetryPoint(i < 42 ? Start.AddSeconds(10 * i) : Start.AddSeconds(420 + i - 42), value)).ToArray();
        var window = new TelemetryWindow(new ToolId("E-02"), "escTemperature", Start, points[^1].At, points);
        SpcViolation[] violations = [new SpcViolation("western-electric-2", new ToolId("E-02"), "E-02-A", "escTemperature", points[42].At, 56.0d, 1.667d, Severity.High)];

        EvidencePackFactory.ShapeOf(window, violations).Should().Be(SignalShape.Oscillation);
        new Mps.Agents.Investigation.ShapeWait(30, TimeSpan.FromSeconds(20), 20).NeedsMoreReadings(window, violations).Should().BeFalse();
    }

    [Fact]
    public void AtTenTimesSpeedTheWaitCountsFromTheIncidentNotFromTheLatestRuleBreak()
    {
        // Live 10x: the harvested rule breaks are each rule's latest, stamped seconds ago, so counting from them never
        // reaches 20 readings and the agent waited its full 10 minutes. Counting from the incident time ends the wait.
        var points = TenX.Select((value, i) => new TelemetryPoint(i < 42 ? Start.AddSeconds(10 * i) : Start.AddSeconds(420 + i - 42), value)).ToArray();
        var window = new TelemetryWindow(new ToolId("E-02"), "escTemperature", Start, points[^1].At, points);
        SpcViolation[] latest = [new SpcViolation("western-electric-1", new ToolId("E-02"), "E-02-A", "escTemperature", points[^1].At, 54.45d, 1.333d, Severity.High)];
        var wait = new Mps.Agents.Investigation.ShapeWait(30, TimeSpan.FromSeconds(20), 20);

        wait.NeedsMoreReadings(window, latest).Should().BeTrue("the old count started at the newest break");
        wait.NeedsMoreReadings(window, latest, points[42].At).Should().BeFalse();
        wait.NeedsMoreReadings(window, latest, points[^5].At).Should().BeTrue("only four readings since the incident opened");
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
