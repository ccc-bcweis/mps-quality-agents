// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Auth;
using Mps.Api.Bus;
using Mps.Api.Demo;
using Mps.Api.Persistence;
using Mps.Application.Decisions;
using Mps.Application.Errors;
using Mps.Application.Intake;
using Mps.Application.Lifecycle;
using Mps.Application.Persistence;
using Mps.Domain.Events;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;

namespace Mps.Api.Http;

public static partial class QualityEndpoints
{
    /// <summary>Alerts, tickets, incidents and investigations, including the human decision.</summary>
    private static void MapRecords(WebApplication app)
    {
        app.MapGet("/api/alerts", async (string? toolId, string? siteId, IAlertLog alerts, ConsoleDataMode mode, CancellationToken cancellationToken) =>
        {
            if (mode.IsReplay)
            {
                var recorded = ReplayProjection.Alerts(mode.Messages)
                    .Where(row => string.IsNullOrWhiteSpace(toolId) || string.Equals(row.ToolId, toolId.Trim(), StringComparison.OrdinalIgnoreCase))
                    .Where(row => MatchesSite(siteId, row.SiteId))
                    .ToArray();
                return Results.Ok(recorded);
            }

            ToolId? filter = string.IsNullOrWhiteSpace(toolId) ? null : new ToolId(toolId);
            var rows = await alerts.ListAsync(filter, cancellationToken).ConfigureAwait(false);
            return Results.Ok(rows.Select(ContractMapper.ToResponse).Where(row => MatchesSite(siteId, row.SiteId)).ToArray());
        });
        app.MapGet("/api/tickets", async (string? siteId, ITicketLog tickets, IIncidentLog incidents, CancellationToken cancellationToken) =>
        {
            var rows = await tickets.ListAsync(cancellationToken).ConfigureAwait(false);
            var stages = (await incidents.ListAsync(cancellationToken).ConfigureAwait(false))
                .ToDictionary(row => row.Incident.Id.Value, row => row.Incident.Stage);
            return Results.Ok(rows
                .Select(row => ContractMapper.ToResponse(row, stages.TryGetValue(row.IncidentId.Value, out var stage) ? stage : null))
                .Where(row => MatchesSite(siteId, row.SiteId))
                .ToArray());
        });
        app.MapGet("/api/incidents", async (string? siteId, IIncidentLog incidents, CancellationToken cancellationToken) =>
        {
            var rows = await incidents.ListAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(rows.Select(row => ContractMapper.ToResponse(row.Incident)).Where(row => MatchesSite(siteId, row.SiteId)).ToArray());
        });
        app.MapPost("/api/incidents", async (
            OpenIncidentBody? body,
            IIntakeService intake,
            CommandGateway commands,
            HttpContext http,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            if (body is null)
            {
                throw new IntakeValidationException("Incident body is required.");
            }

            commands.Ensure(EventTypes.OperatorReport, Caller(http, configuration));
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
            IIncidentLog incidents,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await investigations.GetLatestByIncidentAsync(new IncidentId(incidentId), cancellationToken).ConfigureAwait(false);
            var narrative = await narratives.GetAsync(snapshot.Id, cancellationToken).ConfigureAwait(false);
            var site = await SiteForIncidentAsync(incidents, snapshot.IncidentId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(ContractMapper.ToResponse(snapshot, narrative, site));
        });
        app.MapGet("/api/incidents/{incidentId:guid}/investigations", async (
            Guid incidentId,
            IInvestigationService investigations,
            IInvestigationNarrativeLog narratives,
            IIncidentLog incidents,
            CancellationToken cancellationToken) =>
        {
            // Every round on the ticket, oldest first (the evidence loop). Closed rounds are read-only history.
            var rounds = await investigations.ListRoundsAsync(new IncidentId(incidentId), cancellationToken).ConfigureAwait(false);
            var site = await SiteForIncidentAsync(incidents, new IncidentId(incidentId), cancellationToken).ConfigureAwait(false);
            var mapped = new List<InvestigationResponse>();
            foreach (var round in rounds)
            {
                var narrative = await narratives.GetAsync(round.Id, cancellationToken).ConfigureAwait(false);
                mapped.Add(ContractMapper.ToResponse(round, narrative, site));
            }

            return Results.Ok(mapped);
        });
        app.MapPost("/api/incidents/{incidentId:guid}/investigations", async (
            Guid incidentId,
            IInvestigationService investigations,
            IInvestigationNarrativeLog narratives,
            IIncidentLog incidents,
            CommandGateway commands,
            HttpContext http,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            commands.Ensure(EventTypes.InvestigationStart, Caller(http, configuration));
            var snapshot = await investigations.StartAsync(new IncidentId(incidentId), cancellationToken).ConfigureAwait(false);
            var narrative = await narratives.GetAsync(snapshot.Id, cancellationToken).ConfigureAwait(false);
            var site = await SiteForIncidentAsync(incidents, snapshot.IncidentId, cancellationToken).ConfigureAwait(false);
            var response = ContractMapper.ToResponse(snapshot, narrative, site);
            return Results.Created("/api/investigations/" + response.Id.ToString("D"), response);
        });
        app.MapGet("/api/investigations", async (
            string? siteId,
            IInvestigationService investigations,
            IInvestigationNarrativeLog narratives,
            IIncidentLog incidents,
            CancellationToken cancellationToken) =>
        {
            var rows = await investigations.ListAsync(cancellationToken).ConfigureAwait(false);
            var mapped = new List<InvestigationResponse>();
            foreach (var snapshot in rows)
            {
                var site = await SiteForIncidentAsync(incidents, snapshot.IncidentId, cancellationToken).ConfigureAwait(false);
                if (!MatchesSite(siteId, site))
                {
                    continue;
                }

                var narrative = await narratives.GetAsync(snapshot.Id, cancellationToken).ConfigureAwait(false);
                mapped.Add(ContractMapper.ToResponse(snapshot, narrative, site));
            }

            return Results.Ok(mapped);
        });
        app.MapGet("/api/investigations/{investigationId:guid}", async (
            Guid investigationId,
            IInvestigationService investigations,
            IInvestigationNarrativeLog narratives,
            IIncidentLog incidents,
            CancellationToken cancellationToken) =>
        {
            var id = new InvestigationId(investigationId);
            var snapshot = await investigations.GetAsync(id, cancellationToken).ConfigureAwait(false);
            var narrative = await narratives.GetAsync(id, cancellationToken).ConfigureAwait(false);
            var site = await SiteForIncidentAsync(incidents, snapshot.IncidentId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(ContractMapper.ToResponse(snapshot, narrative, site));
        });
        app.MapPost("/api/investigations/{investigationId:guid}/decisions", async (
            Guid investigationId,
            DecisionBody? body,
            IHumanDecisionService decisions,
            IServiceScopeFactory scopes,
            Mps.Application.Lifecycle.InvestigationsInFlight inFlight,
            IInvestigationNarrativeLog narratives,
            IIncidentLog incidents,
            QualityDbContext db,
            CommandGateway commands,
            HttpContext http,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            if (body?.Kind is null)
            {
                throw new IntakeValidationException("Decision kind is required.");
            }

            var approvalCaller = Caller(http, configuration);
            commands.Ensure(EventTypes.ApprovalGrant, approvalCaller);
            var id = new InvestigationId(investigationId);

            // One role: the approver is always the signed-in user in the hosted demo. Local runs may name one.
            var approver = string.Equals(approvalCaller.Mode, EasyAuthPrincipal.AzureMode, StringComparison.OrdinalIgnoreCase)
                ? approvalCaller.DisplayName
                : body.ApproverId;

            // Added evidence always names who added it; a local run without a name uses the local caller's name.
            if (body.Kind == HumanDecisionKind.RequestInfo && string.IsNullOrWhiteSpace(approver))
            {
                approver = approvalCaller.DisplayName;
            }
            var outcome = await decisions.DecideAsync(
                id,
                body.Kind.Value,
                body.Comment,
                approver,
                cancellationToken).ConfigureAwait(false);
            var site = await SiteForIncidentAsync(incidents, outcome.Investigation.IncidentId, cancellationToken).ConfigureAwait(false);
            if (outcome.Superseded)
            {
                // The evidence loop: the round is closed with the person's evidence; the next round runs in the background
                // (gathering can wait minutes for readings) and the console follows its placeholder until it is saved.
                var next = await NextRoundAsync(scopes, inFlight, outcome, cancellationToken).ConfigureAwait(false);
                var nextNarrative = await narratives.GetAsync(next.Id, cancellationToken).ConfigureAwait(false);
                return Results.Ok(new DecisionResponse(ContractMapper.ToResponse(next, nextNarrative, site), null));
            }

            var narrative = await narratives.GetAsync(id, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new DecisionResponse(
                ContractMapper.ToResponse(outcome.Investigation, narrative, site),
                outcome.Capa is null ? null : await WithApprovalAsync(db, ContractMapper.ToResponse(outcome.Capa.Capa, site, await ToolForIncidentAsync(incidents, outcome.Capa.Capa.IncidentId, cancellationToken).ConfigureAwait(false)), cancellationToken).ConfigureAwait(false)));
        });
    }
}
