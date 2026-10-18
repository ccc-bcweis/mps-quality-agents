// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Bus;
using Mps.Api.Persistence;
using Mps.Application.Metrics;
using Mps.Application.Printouts;
using Mps.Simulator.Scenarios;
using Mps.Simulator.Sites;

namespace Mps.Api.Http;

public static partial class QualityEndpoints
{
    /// <summary>Causal trace, audit log and metrics.</summary>
    private static void MapTraceAuditAndMetrics(WebApplication app)
    {
        app.MapGet("/api/trace/{eventId}", (string eventId, CausalTraceService traces) =>
        {
            var trace = traces.Build(eventId);
            return trace is null ? Results.NotFound() : Results.Ok(trace);
        });
        app.MapGet("/api/audit", async (QualityDbContext db, IManualMeasurementLog measurements, CancellationToken cancellationToken) =>
        {
            return Results.Ok(await AuditLog.ReadAsync(db, measurements, cancellationToken).ConfigureAwait(false));
        });
        app.MapGet("/api/metrics", async (
            IMetricsLog metrics,
            MetricsPolicy policy,
            IConfiguration configuration,
            TimeProvider time,
            IServiceProvider services,
            CancellationToken cancellationToken) =>
        {
            var rollup = await metrics.RollupAsync(
                time.GetUtcNow(),
                policy.SpendCapUsd,
                ContractMapper.ModeFrom(configuration["LLM_MODE"]),
                cancellationToken).ConfigureAwait(false);
            var spendToday = await LiveSpendAsync(services, time, rollup.SpendTodayUsd, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new MetricsResponse(
                rollup.Mode,
                rollup.CompletionRate,
                rollup.GroundingScore,
                rollup.CategoryAccuracy ?? EvalAccuracy.Value,
                rollup.ToolFailureCount,
                rollup.LatencyP95Ms,
                MetricsRollup.CostPerSuccess(spendToday, rollup.SuccessfulToday),
                spendToday,
                rollup.SpendCapUsd));
        });
    }
}
