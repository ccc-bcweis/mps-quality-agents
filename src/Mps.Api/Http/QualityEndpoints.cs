// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Auth;
using Mps.Api.Demo;
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
using Mps.Simulator.Demo;
using Mps.Simulator.Fleet;
using Mps.Simulator.Scenarios;
using Mps.Simulator.Sites;

namespace Mps.Api.Http;

public static class QualityEndpoints
{
    public static void Map(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/health", () => Results.Ok(new HealthResponse("ok", LayerAnchor.Name)));
        app.MapGet("/api/sites", (SiteWorld world) => Results.Ok(world.Sites.Select(SiteResponses.ToSummary).ToArray()));
        app.MapGet("/api/sites/compare", (string? family, SiteWorld world) =>
        {
            if (!FamilyParameterCatalog.TryParseFamily(family, out var parsed))
            {
                throw new IntakeValidationException("Query family must be deposition, etch, strip, or clean.");
            }

            return Results.Ok(SiteResponses.ToComparison(SiteComparison.Compare(world, parsed)));
        });
        app.MapGet("/api/sites/{siteId}", (string siteId, SiteWorld world) =>
        {
            var site = world.Find(siteId);
            return site is null ? Results.NotFound() : Results.Ok(SiteResponses.ToDetail(site));
        });
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
        app.MapGet("/api/incidents/{incidentId:guid}/investigation", async (
            Guid incidentId,
            IInvestigationService investigations,
            IInvestigationNarrativeLog narratives,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await investigations.GetLatestByIncidentAsync(new IncidentId(incidentId), cancellationToken).ConfigureAwait(false);
            var narrative = await narratives.GetAsync(snapshot.Id, cancellationToken).ConfigureAwait(false);
            return Results.Ok(ContractMapper.ToResponse(snapshot, narrative));
        });
        app.MapPost("/api/incidents/{incidentId:guid}/investigations", async (
            Guid incidentId,
            IInvestigationService investigations,
            IInvestigationNarrativeLog narratives,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await investigations.StartAsync(new IncidentId(incidentId), cancellationToken).ConfigureAwait(false);
            var narrative = await narratives.GetAsync(snapshot.Id, cancellationToken).ConfigureAwait(false);
            var response = ContractMapper.ToResponse(snapshot, narrative);
            return Results.Created("/api/investigations/" + response.Id.ToString("D"), response);
        });
        app.MapGet("/api/investigations/{investigationId:guid}", async (
            Guid investigationId,
            IInvestigationService investigations,
            IInvestigationNarrativeLog narratives,
            CancellationToken cancellationToken) =>
        {
            var id = new InvestigationId(investigationId);
            var snapshot = await investigations.GetAsync(id, cancellationToken).ConfigureAwait(false);
            var narrative = await narratives.GetAsync(id, cancellationToken).ConfigureAwait(false);
            return Results.Ok(ContractMapper.ToResponse(snapshot, narrative));
        });
        app.MapPost("/api/investigations/{investigationId:guid}/decisions", async (
            Guid investigationId,
            DecisionBody? body,
            IHumanDecisionService decisions,
            IInvestigationService investigations,
            IInvestigationNarrativeLog narratives,
            CancellationToken cancellationToken) =>
        {
            if (body?.Kind is null)
            {
                throw new IntakeValidationException("Decision kind is required.");
            }

            var id = new InvestigationId(investigationId);
            var outcome = await decisions.DecideAsync(
                id,
                body.Kind.Value,
                body.Comment,
                body.ApproverId,
                cancellationToken).ConfigureAwait(false);
            if (outcome.Superseded)
            {
                // Request more evidence: re-run the agents on the same incident with whatever data is now available.
                var rerun = await investigations.StartAsync(outcome.Investigation.IncidentId, cancellationToken).ConfigureAwait(false);
                var rerunNarrative = await narratives.GetAsync(rerun.Id, cancellationToken).ConfigureAwait(false);
                return Results.Ok(new DecisionResponse(ContractMapper.ToResponse(rerun, rerunNarrative), null));
            }

            var narrative = await narratives.GetAsync(id, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new DecisionResponse(
                ContractMapper.ToResponse(outcome.Investigation, narrative),
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
        app.MapGet("/api/admin/session", (HttpContext http, IConfiguration configuration) =>
        {
            var caller = Caller(http, configuration);
            return Results.Ok(new AdminSessionResponse(caller.Mode, caller.IsAdmin, caller.DisplayName));
        });
        app.MapPost("/api/admin/reset", async (HttpContext http, IConfiguration configuration, DemoResetService reset, ScenarioEvidenceBoard board, CancellationToken cancellationToken) =>
        {
            if (!Caller(http, configuration).IsAdmin)
            {
                return Results.Problem(title: "Admin role is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            board.Clear();
            var baseline = await reset.ResetAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(baseline);
        });
        app.MapGet("/api/injector/scenarios", () =>
        {
            var rows = ScriptedScenarioCatalog.All.Select(scenario => new ScenarioDescription(
                scenario.Id,
                scenario.Title,
                scenario.Category,
                scenario.ToolIds,
                ScriptedScenario.ChamberIdFor(scenario.ToolIds[0]),
                scenario.Parameter,
                scenario.ShapeName,
                scenario.Summary)).ToArray();
            return Results.Ok(rows);
        });
        app.MapPost("/api/injector/scenarios/{scenarioId}", async (string scenarioId, ScenarioTrigger scenarios, CancellationToken cancellationToken) =>
        {
            var run = await scenarios.RunAsync(scenarioId, cancellationToken).ConfigureAwait(false);
            if (run is null)
            {
                throw new ResourceNotFoundException("Scenario was not found.");
            }

            return Results.Json(run, statusCode: StatusCodes.Status202Accepted);
        });
        app.MapPost("/api/faults", async (FaultBody? body, IFaultInjectionService faults, DemoRuntime runtime, CancellationToken cancellationToken) =>
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
            runtime.TryApplyFreeForm(body.ToolId ?? string.Empty, body.ChamberId, body.Parameter ?? string.Empty, body.Shape ?? string.Empty, body.Magnitude.Value);
            return Results.Json(new FaultAcceptedResponse(accepted.FaultId, accepted.CorrelationId), statusCode: StatusCodes.Status202Accepted);
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
            return Results.Ok(new MetricsResponse(
                rollup.Mode,
                rollup.CompletionRate,
                rollup.GroundingScore,
                rollup.CategoryAccuracy,
                rollup.ToolFailureCount,
                rollup.LatencyP95Ms,
                rollup.CostPerInvestigationUsd,
                await LiveSpendAsync(services, time, rollup.SpendTodayUsd, cancellationToken).ConfigureAwait(false),
                rollup.SpendCapUsd));
        });
    }

    private static DemoCaller Caller(HttpContext http, IConfiguration configuration)
    {
        var header = http.Request.Headers[EasyAuthPrincipal.HeaderName].ToString();
        return EasyAuthPrincipal.Read(string.IsNullOrWhiteSpace(header) ? null : header, configuration["Mps:Auth:Mode"]);
    }

    private static async Task<double> LiveSpendAsync(IServiceProvider services, TimeProvider time, double fallback, CancellationToken cancellationToken)
    {
        var ledger = services.GetService<Mps.ModelGateway.Cost.ICostLedger>();
        if (ledger is null)
        {
            return fallback;
        }

        var day = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        return (double)await ledger.GetSpendAsync(day, cancellationToken).ConfigureAwait(false);
    }
}

public sealed record AdminSessionResponse(string Mode, bool IsAdmin, string DisplayName);
