// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Diagnostics;

using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Application.Metrics;

public static class QualityDiagnostics
{
    public const string ActivitySourceName = "Mps.Quality";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}

public sealed record InvestigationObservation(
    Guid InvestigationId,
    DateTimeOffset RecordedAt,
    bool Completed,
    bool Succeeded,
    double? GroundingScore,
    int ToolSuccesses,
    int ToolFailures,
    double? LatencyMs,
    double CostUsd);

public sealed record MetricsRollup(
    string Mode,
    double? CompletionRate,
    double? GroundingScore,
    double? CategoryAccuracy,
    int ToolFailureCount,
    double? LatencyP95Ms,
    double? CostPerInvestigationUsd,
    double SpendTodayUsd,
    double SpendCapUsd);

public sealed class MetricsPolicy
{
    public MetricsPolicy(double costPerInvestigationUsd, double spendCapUsd)
    {
        if (costPerInvestigationUsd < 0d || double.IsNaN(costPerInvestigationUsd))
        {
            throw new ArgumentOutOfRangeException(nameof(costPerInvestigationUsd), "Investigation cost cannot be negative.");
        }

        if (spendCapUsd < 0d || double.IsNaN(spendCapUsd))
        {
            throw new ArgumentOutOfRangeException(nameof(spendCapUsd), "Spend cap cannot be negative.");
        }

        CostPerInvestigationUsd = costPerInvestigationUsd;
        SpendCapUsd = spendCapUsd;
    }

    public double CostPerInvestigationUsd { get; }

    public double SpendCapUsd { get; }
}

public interface IMetricsLog
{
    Task RecordAsync(InvestigationObservation observation, CancellationToken cancellationToken);

    Task<MetricsRollup> RollupAsync(DateTimeOffset now, double spendCapUsd, string mode, CancellationToken cancellationToken);
}

public static class GroundingScore
{
    public static double For(IReadOnlyList<EvidenceItem> evidence, IReadOnlyList<Hypothesis> hypotheses)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(hypotheses);
        var cited = hypotheses.SelectMany(hypothesis => hypothesis.EvidenceIds).ToArray();
        if (cited.Length == 0)
        {
            return hypotheses.Count == 0 ? 1d : 0d;
        }

        var known = evidence.Select(item => item.Id).ToHashSet();
        return (double)cited.Count(id => known.Contains(id)) / cited.Length;
    }
}

public static class MetricsCalculator
{
    public static double? NearestRank(IReadOnlyList<double> samples, double percentile)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0)
        {
            return null;
        }

        var ordered = samples.OrderBy(sample => sample).ToArray();
        var rank = (int)Math.Ceiling(percentile * ordered.Length);
        var index = Math.Clamp(rank, 1, ordered.Length) - 1;
        return ordered[index];
    }

    public static MetricsRollup Compute(
        IReadOnlyList<InvestigationObservation> observations,
        DateTimeOffset now,
        double spendCapUsd,
        string mode)
    {
        ArgumentNullException.ThrowIfNull(observations);
        var groups = observations.GroupBy(item => item.InvestigationId).ToArray();
        double? completion = groups.Length == 0
            ? null
            : groups.Count(group => group.Any(item => item.Completed)) / (double)groups.Length;
        var grounding = groups
            .Select(group => group.Select(item => item.GroundingScore).LastOrDefault(score => score is not null))
            .Where(score => score is not null)
            .Select(score => score!.Value)
            .ToArray();
        double? groundingScore = grounding.Length == 0 ? null : grounding.Average();
        var latencies = observations.Where(item => item.LatencyMs is not null).Select(item => item.LatencyMs!.Value).ToArray();
        var successful = groups.Where(group => group.Any(item => item.Succeeded)).ToArray();
        double? costPer = successful.Length == 0
            ? null
            : successful.Sum(group => group.Sum(item => item.CostUsd)) / successful.Length;
        var today = now.UtcDateTime.Date;
        var spend = observations.Where(item => item.RecordedAt.UtcDateTime.Date == today).Sum(item => item.CostUsd);
        return new MetricsRollup(
            mode,
            completion,
            groundingScore,
            null,
            observations.Sum(item => item.ToolFailures),
            NearestRank(latencies, 0.95d),
            costPer,
            spend,
            spendCapUsd);
    }
}

public sealed class NullMetricsLog : IMetricsLog
{
    public static readonly NullMetricsLog Instance = new();

    private NullMetricsLog()
    {
    }

    public Task RecordAsync(InvestigationObservation observation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observation);
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    public Task<MetricsRollup> RollupAsync(DateTimeOffset now, double spendCapUsd, string mode, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(MetricsCalculator.Compute([], now, spendCapUsd, mode));
    }
}
