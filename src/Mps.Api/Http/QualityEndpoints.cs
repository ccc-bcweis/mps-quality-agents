// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Decisions;
using Mps.Application.Errors;
using Mps.Application.Faults;
using Mps.Application.Intake;
using Mps.Application.Lifecycle;
using Mps.Application.Metrics;
using Mps.Application.Persistence;
using Mps.Domain.Capa;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;

namespace Mps.Api.Http;

public static class QualityEndpoints
{
    public static void Map(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/health", () => Results.Ok(new HealthResponse("ok", LayerAnchor.Name)));
        app.MapGet("/api/tools", async (IFleetCatalog fleet, CancellationToken cancellationToken) =>
        {
            var tools = await fleet.ListAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(tools.Select(ContractMapper.ToResponse).ToArray());
        });
        app.MapGet("/api/tools/{toolId}", async (string toolId, IFleetCatalog fleet, CancellationToken cancellationToken) =>
        {
            var tool = await fleet.FindAsync(new ToolId(toolId), cancellationToken).ConfigureAwait(false);
            return tool is null
                ? Results.NotFound()
                : Results.Ok(ContractMapper.ToResponse(tool));
        });
        app.MapGet("/api/alerts", async (string? toolId, IAlertLog alerts, CancellationToken cancellationToken) =>
        {
            ToolId? filter = string.IsNullOrWhiteSpace(toolId) ? null : new ToolId(toolId);
            var rows = await alerts.ListAsync(filter, cancellationToken).ConfigureAwait(false);
            return Results.Ok(rows.Select(ContractMapper.ToResponse).ToArray());
        });
        app.MapGet("/api/tickets", async (ITicketLog tickets, CancellationToken cancellationToken) =>
        {
            var rows = await tickets.ListAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(rows.Select(ContractMapper.ToResponse).ToArray());
        });
        app.MapGet("/api/incidents", async (IIncidentLog incidents, CancellationToken cancellationToken) =>
        {
            var rows = await incidents.ListAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(rows.Select(row => ContractMapper.ToResponse(row.Incident)).ToArray());
        });
        app.MapPost("/api/incidents", async (OpenIncidentBody? body, IIntakeService intake, CancellationToken cancellationToken) =>
        {
            if (body is null)
            {
                throw new IntakeValidationException("Incident body is required.");
            }

            var result = await intake.AcceptAsync(
                new IntakeSubmission(
                    IncidentChannel.OperatorReport,
                    body.ToolId ?? string.Empty,
                    body.ChamberId,
                    body.Parameter ?? string.Empty,
                    body.Summary ?? string.Empty,
                    body.Severity,
                    signalSeverity: null,
                    correlationId: null,
                    occurredAt: null),
                cancellationToken).ConfigureAwait(false);
            var response = ContractMapper.ToResponse(result.Incident);
            return Results.Created("/api/incidents/" + response.Id.ToString("D"), response);
        });
        app.MapGet("/api/incidents/{incidentId:guid}", async (Guid incidentId, IIncidentLog incidents, CancellationToken cancellationToken) =>
        {
            var stored = await incidents.GetAsync(new IncidentId(incidentId), cancellationToken).ConfigureAwait(false);
            return stored is null ? Results.NotFound() : Results.Ok(ContractMapper.ToResponse(stored.Incident));
        });
        app.MapPost("/api/incidents/{incidentId:guid}/investigations", async (
            Guid incidentId,
            IInvestigationService investigations,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await investigations.StartAsync(new IncidentId(incidentId), cancellationToken).ConfigureAwait(false);
            var response = ContractMapper.ToResponse(snapshot);
            return Results.Created("/api/investigations/" + response.Id.ToString("D"), response);
        });
        app.MapGet("/api/investigations/{investigationId:guid}", async (
            Guid investigationId,
            IInvestigationService investigations,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await investigations.GetAsync(new InvestigationId(investigationId), cancellationToken).ConfigureAwait(false);
            return Results.Ok(ContractMapper.ToResponse(snapshot));
        });
        app.MapPost("/api/investigations/{investigationId:guid}/decisions", async (
            Guid investigationId,
            DecisionBody? body,
            IHumanDecisionService decisions,
            CancellationToken cancellationToken) =>
        {
            if (body?.Kind is null)
            {
                throw new IntakeValidationException("Decision kind is required.");
            }

            var outcome = await decisions.DecideAsync(
                new InvestigationId(investigationId),
                body.Kind.Value,
                body.Comment,
                body.ApproverId,
                cancellationToken).ConfigureAwait(false);
            return Results.Ok(new DecisionResponse(
                ContractMapper.ToResponse(outcome.Investigation),
                outcome.Capa is null ? null : ContractMapper.ToResponse(outcome.Capa.Capa)));
        });
        app.MapGet("/api/capas", async (ICapaLifecycleService capas, CancellationToken cancellationToken) =>
        {
            var rows = await capas.ListAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(rows.Select(row => ContractMapper.ToResponse(row.Capa)).ToArray());
        });
        app.MapGet("/api/capas/{capaId:guid}", async (Guid capaId, ICapaLifecycleService capas, CancellationToken cancellationToken) =>
        {
            var stored = await capas.GetAsync(new CapaId(capaId), cancellationToken).ConfigureAwait(false);
            return Results.Ok(ContractMapper.ToResponse(stored.Capa));
        });
        app.MapPost("/api/capas/{capaId:guid}/advance", async (
            Guid capaId,
            AdvanceCapaBody? body,
            ICapaLifecycleService capas,
            CancellationToken cancellationToken) =>
        {
            if (body?.Stage is null)
            {
                throw new IntakeValidationException("CAPA stage is required.");
            }

            var stored = await capas.AdvanceAsync(new CapaId(capaId), body.Stage.Value, cancellationToken).ConfigureAwait(false);
            return Results.Ok(ContractMapper.ToResponse(stored.Capa));
        });
        app.MapPost("/api/faults", async (FaultBody? body, IFaultInjectionService faults, CancellationToken cancellationToken) =>
        {
            if (body is null)
            {
                throw new IntakeValidationException("Fault body is required.");
            }

            if (body.RootCauseCategory is null)
            {
                throw new IntakeValidationException("Root-cause category is required.");
            }

            if (body.Magnitude is null)
            {
                throw new IntakeValidationException("Fault magnitude is required.");
            }

            var accepted = await faults.InjectAsync(
                new FaultInjectionRequest(
                    body.ToolId ?? string.Empty,
                    body.ChamberId,
                    body.Parameter ?? string.Empty,
                    body.Shape ?? string.Empty,
                    body.Magnitude.Value,
                    body.RootCauseCategory.Value,
                    body.Confounders),
                cancellationToken).ConfigureAwait(false);
            return Results.Json(new FaultAcceptedResponse(accepted.FaultId, accepted.CorrelationId), statusCode: StatusCodes.Status202Accepted);
        });
        app.MapGet("/api/metrics", async (
            IMetricsLog metrics,
            MetricsPolicy policy,
            IConfiguration configuration,
            TimeProvider time,
            CancellationToken cancellationToken) =>
        {
            var rollup = await metrics.RollupAsync(
                time.GetUtcNow(),
                policy.SpendCapUsd,
                ContractMapper.ModeFrom(configuration["LLM_MODE"]),
                cancellationToken).ConfigureAwait(false);
            return Results.Ok(new MetricsResponse(
                rollup.Mode,
                rollup.CompletionRate,
                rollup.GroundingScore,
                rollup.CategoryAccuracy,
                rollup.ToolFailureCount,
                rollup.LatencyP95Ms,
                rollup.CostPerInvestigationUsd,
                rollup.SpendTodayUsd,
                rollup.SpendCapUsd));
        });
    }
}
