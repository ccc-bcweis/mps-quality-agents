// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Diagnostics.Metrics;

using Microsoft.EntityFrameworkCore;

using Mps.Application.Metrics;

namespace Mps.Api.Persistence;

public sealed class EfMetricsLog : IMetricsLog
{
    public const string MeterName = "Mps.Quality";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> ToolSuccess = Meter.CreateCounter<long>("mps.tools.success");

    private static readonly Counter<long> ToolFailure = Meter.CreateCounter<long>("mps.tools.failure");

    private static readonly Counter<long> InvestigationsCompleted = Meter.CreateCounter<long>("mps.investigations.completed");

    private static readonly Histogram<double> Latency = Meter.CreateHistogram<double>("mps.investigation.latency", unit: "ms");

    private static readonly Histogram<double> Cost = Meter.CreateHistogram<double>("mps.investigation.cost", unit: "usd");

    private readonly QualityDbContext _db;

    public EfMetricsLog(QualityDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task RecordAsync(InvestigationObservation observation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observation);
        _db.MetricSamples.Add(new MetricSampleRow
        {
            Id = Guid.NewGuid(),
            InvestigationId = observation.InvestigationId,
            RecordedAt = observation.RecordedAt.UtcDateTime,
            Completed = observation.Completed,
            Succeeded = observation.Succeeded,
            GroundingScore = observation.GroundingScore,
            ToolSuccesses = observation.ToolSuccesses,
            ToolFailures = observation.ToolFailures,
            LatencyMs = observation.LatencyMs,
            CostUsd = observation.CostUsd,
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Publish(observation);
    }

    public async Task<MetricsRollup> RollupAsync(DateTimeOffset now, double spendCapUsd, string mode, CancellationToken cancellationToken)
    {
        var rows = await _db.MetricSamples.AsNoTracking().OrderBy(row => row.RecordedAt).ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var observations = rows.Select(row => new InvestigationObservation(
            row.InvestigationId,
            new DateTimeOffset(DateTime.SpecifyKind(row.RecordedAt, DateTimeKind.Utc)),
            row.Completed,
            row.Succeeded,
            row.GroundingScore,
            row.ToolSuccesses,
            row.ToolFailures,
            row.LatencyMs,
            row.CostUsd)).ToArray();
        return MetricsCalculator.Compute(observations, now, spendCapUsd, mode);
    }

    private static void Publish(InvestigationObservation observation)
    {
        if (observation.ToolSuccesses > 0)
        {
            ToolSuccess.Add(observation.ToolSuccesses);
        }

        if (observation.ToolFailures > 0)
        {
            ToolFailure.Add(observation.ToolFailures);
        }

        if (observation.Completed)
        {
            InvestigationsCompleted.Add(1);
        }

        if (observation.LatencyMs is double latency)
        {
            Latency.Record(latency);
            Cost.Record(observation.CostUsd);
        }
    }
}
