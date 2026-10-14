// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Metrics;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Application.Tests.Metrics;

public sealed class MetricsCalculatorTests
{
    private static readonly DateTimeOffset Today = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NearestRankUsesCeilOfPercentTimesCount()
    {
        var samples = Enumerable.Range(1, 100).Select(value => (double)value).ToArray();

        MetricsCalculator.NearestRank(samples, 0.95d).Should().Be(95d);
        MetricsCalculator.NearestRank([4d], 0.95d).Should().Be(4d);
        MetricsCalculator.NearestRank([], 0.95d).Should().BeNull();
    }

    [Fact]
    public void AnEmptyLogHasNoRates()
    {
        var rollup = MetricsCalculator.Compute([], Today, 2d, "replay");

        rollup.Mode.Should().Be("replay");
        rollup.CompletionRate.Should().BeNull();
        rollup.GroundingScore.Should().BeNull();
        rollup.CategoryAccuracy.Should().BeNull();
        rollup.ToolFailureCount.Should().Be(0);
        rollup.LatencyP95Ms.Should().BeNull();
        rollup.CostPerInvestigationUsd.Should().BeNull();
        rollup.SpendTodayUsd.Should().Be(0d);
        rollup.SpendCapUsd.Should().Be(2d);
    }

    [Fact]
    public void RollupSeparatesSuccessfulCostFromTodaysSpend()
    {
        var done = Guid.NewGuid();
        var failed = Guid.NewGuid();
        var open = Guid.NewGuid();
        var observations = new InvestigationObservation[]
        {
            Sample(done, Today.AddDays(-1), completed: false, succeeded: false, grounding: 1d, failures: 0, latency: 10d, cost: 4d),
            Sample(done, Today.AddDays(-1), completed: true, succeeded: true, grounding: null, failures: 0, latency: null, cost: 0d),
            Sample(failed, Today, completed: false, succeeded: false, grounding: 0.5d, failures: 3, latency: 30d, cost: 2d),
            Sample(open, Today, completed: false, succeeded: false, grounding: 0d, failures: 0, latency: 20d, cost: 0d),
        };

        var rollup = MetricsCalculator.Compute(observations, Today, 2d, "live");

        rollup.CompletionRate.Should().BeApproximately(1d / 3d, 0.0001d);
        rollup.GroundingScore.Should().BeApproximately((1d + 0.5d + 0d) / 3d, 0.0001d);
        rollup.CategoryAccuracy.Should().BeNull();
        rollup.ToolFailureCount.Should().Be(3);
        rollup.LatencyP95Ms.Should().Be(30d);
        rollup.CostPerInvestigationUsd.Should().Be(4d);
        rollup.SpendTodayUsd.Should().Be(2d);
    }

    [Fact]
    public void AZeroReplayCostIsARealNumberOnceAnInvestigationSucceeds()
    {
        var id = Guid.NewGuid();
        var observations = new[]
        {
            Sample(id, Today, completed: false, succeeded: false, grounding: 1d, failures: 0, latency: 0d, cost: 0d),
            Sample(id, Today, completed: true, succeeded: true, grounding: null, failures: 0, latency: null, cost: 0d),
        };

        var rollup = MetricsCalculator.Compute(observations, Today, 2d, "replay");

        rollup.CompletionRate.Should().Be(1d);
        rollup.CostPerInvestigationUsd.Should().Be(0d);
        rollup.LatencyP95Ms.Should().Be(0d);
        rollup.SpendTodayUsd.Should().Be(0d);
    }

    [Fact]
    public void GroundingUsesCitedEvidenceAndTreatsAnEscalationWithoutAHypothesisAsGrounded()
    {
        var known = EvidenceId.New();
        var unknown = EvidenceId.New();
        var evidence = new[]
        {
            new EvidenceItem(known, EvidenceSource.Spc, "Chart", "Outside the window.", Today, "E-02", null),
        };
        var mixed = new Hypothesis(HypothesisId.New(), RootCauseCategory.ProcessDrift, "Drift.", 0.4d, [known, unknown], [], ["Hold the lot."]);

        GroundingScore.For(evidence, [mixed]).Should().Be(0.5d);
        GroundingScore.For(evidence, []).Should().Be(1d);
    }

    private static InvestigationObservation Sample(
        Guid id,
        DateTimeOffset at,
        bool completed,
        bool succeeded,
        double? grounding,
        int failures,
        double? latency,
        double cost) =>
        new(id, at, completed, succeeded, grounding, 0, failures, latency, cost);
}
