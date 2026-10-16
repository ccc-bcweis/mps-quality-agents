// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Mps.Api.Auth;
using Mps.Api.Bus;
using Mps.Api.Demo;
using Mps.Api.Live;
using Mps.Api.Persistence;
using Mps.Application.Bus;
using Mps.Application.Decisions;
using Mps.Application.Errors;
using Mps.Application.Faults;
using Mps.Application.Intake;
using Mps.Application.Lifecycle;
using Mps.Application.Metrics;
using Mps.Application.Persistence;
using Mps.Application.Printouts;
using Mps.Domain.Capa;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;
using Mps.Simulator.Fleet;
using Mps.Simulator.Scenarios;
using Mps.Simulator.Sites;

namespace Mps.Api.Http;

public static class QualityEndpoints
{
    private const int ReplayCaptureTicks = 90;

    private static readonly JsonSerializerOptions ResetJson = new(JsonSerializerDefaults.Web);

    public static void Map(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/health", () => Results.Ok(new HealthResponse("ok", LayerAnchor.Name)));
        MetadataEndpoints.Map(app);
        app.MapGet("/api/sites", (SiteWorld world) => Results.Ok(world.Sites.Select(SiteResponses.ToSummary).ToArray()));
        app.MapGet("/api/sites/compare", (string? family, SiteWorld world) =>
        {
            if (!FamilyParameterCatalog.TryParseFamily(family, out var parsed))
            {
                throw new IntakeValidationException("Query family must be deposition, etch, strip, or clean.");
            }

            return Results.Ok(SiteResponses.ToComparison(SiteComparison.Compare(world, parsed)));
        });
        app.MapGet("/api/sites/{siteId}", async (string siteId, SiteWorld world, IAlertLog alerts, CancellationToken cancellationToken) =>
        {
            var site = world.Find(siteId);
            if (site is null)
            {
                return Results.NotFound();
            }

            var status = ToolAlertStatus.ByTool(await alerts.ListAsync(null, cancellationToken).ConfigureAwait(false));
            return Results.Ok(SiteResponses.ToDetail(site, toolId => ToolAlertStatus.Of(status, toolId)));
        });
        app.MapGet("/api/tools", (string? siteId, PlantSimulation plant) =>
        {
            // All sites is the union of every plant's floor, the same set the live stream reports.
            var all = string.IsNullOrWhiteSpace(siteId) || string.Equals(siteId.Trim(), "all", StringComparison.OrdinalIgnoreCase);
            var tools = plant.World.Sites
                .Where(item => all || string.Equals(item.Id, siteId!.Trim(), StringComparison.OrdinalIgnoreCase))
                .SelectMany(item => plant.ToolsForSite(item.Id));
            return Results.Ok(tools.Select(ContractMapper.ToResponse).ToArray());
        });
        app.MapGet("/api/tools/{toolId}", async (string toolId, IFleetCatalog fleet, CancellationToken cancellationToken) =>
        {
            var tool = await fleet.FindAsync(new ToolId(toolId), cancellationToken).ConfigureAwait(false);
            return tool is null
                ? Results.NotFound()
                : Results.Ok(ContractMapper.ToResponse(tool));
        });
        app.MapGet("/api/fleet/live", async (PlantSimulation plant, ConsoleDataMode mode, IAlertLog alerts, CancellationToken cancellationToken) =>
        {
            if (mode.IsReplay)
            {
                return Results.Ok(ReplayProjection.Fleet(mode.Messages));
            }

            var status = ToolAlertStatus.ByTool(await alerts.ListAsync(null, cancellationToken).ConfigureAwait(false));
            return Results.Ok(LiveResponses.Fleet(plant, status));
        });
        app.MapGet("/api/tools/{toolId}/live", async (string toolId, PlantSimulation plant, ConsoleDataMode mode, IAlertLog alerts, CancellationToken cancellationToken) =>
        {
            if (!mode.IsReplay)
            {
                var status = ToolAlertStatus.ByTool(await alerts.ListAsync(null, cancellationToken).ConfigureAwait(false));
                var live = LiveResponses.Tool(plant, toolId, status);
                return live is null ? Results.NotFound() : Results.Ok(live);
            }

            var recorded = ReplayProjection.Tool(mode.Messages, toolId);
            if (recorded is not null)
            {
                return Results.Ok(recorded);
            }

            var placed = plant.FindTool(toolId);
            if (placed is null)
            {
                return Results.NotFound();
            }

            var now = mode.Messages.Count == 0 ? plant.SimulatedNow : mode.Messages.Max(message => message.OccurredAt);
            return Results.Ok(new ToolLiveResponse(toolId, ContractMapper.SiteForTool(placed.SiteId ?? toolId), now, "nominal", [], []));
        });
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
        app.MapGet("/api/tickets", async (string? siteId, ITicketLog tickets, CancellationToken cancellationToken) =>
        {
            var rows = await tickets.ListAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(rows.Select(ContractMapper.ToResponse).Where(row => MatchesSite(siteId, row.SiteId)).ToArray());
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

            commands.Ensure(LifecycleTypes.OperatorReport, Caller(http, configuration));
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
            commands.Ensure(LifecycleTypes.InvestigationStart, Caller(http, configuration));
            var snapshot = await investigations.StartAsync(new IncidentId(incidentId), cancellationToken).ConfigureAwait(false);
            var narrative = await narratives.GetAsync(snapshot.Id, cancellationToken).ConfigureAwait(false);
            var site = await SiteForIncidentAsync(incidents, snapshot.IncidentId, cancellationToken).ConfigureAwait(false);
            var response = ContractMapper.ToResponse(snapshot, narrative, site);
            return Results.Created("/api/investigations/" + response.Id.ToString("D"), response);
        });
        app.MapGet("/api/investigations", async (
            string? siteId,
            IInvestigationLog investigations,
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
            IInvestigationService investigations,
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
            commands.Ensure(LifecycleTypes.ApprovalGrant, approvalCaller);
            var id = new InvestigationId(investigationId);

            // One role: the approver is always the signed-in user in the hosted demo. Local runs may name one.
            var approver = string.Equals(approvalCaller.Mode, EasyAuthPrincipal.AzureMode, StringComparison.OrdinalIgnoreCase)
                ? approvalCaller.DisplayName
                : body.ApproverId;
            var outcome = await decisions.DecideAsync(
                id,
                body.Kind.Value,
                body.Comment,
                approver,
                cancellationToken).ConfigureAwait(false);
            var site = await SiteForIncidentAsync(incidents, outcome.Investigation.IncidentId, cancellationToken).ConfigureAwait(false);
            if (outcome.Superseded)
            {
                // Request more evidence: re-run the agents on the same incident with whatever data is now available.
                var rerun = await investigations.StartAsync(outcome.Investigation.IncidentId, cancellationToken).ConfigureAwait(false);
                var rerunNarrative = await narratives.GetAsync(rerun.Id, cancellationToken).ConfigureAwait(false);
                return Results.Ok(new DecisionResponse(ContractMapper.ToResponse(rerun, rerunNarrative, site), null));
            }

            var narrative = await narratives.GetAsync(id, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new DecisionResponse(
                ContractMapper.ToResponse(outcome.Investigation, narrative, site),
                outcome.Capa is null ? null : await WithApprovalAsync(db, ContractMapper.ToResponse(outcome.Capa.Capa, site, await ToolForIncidentAsync(incidents, outcome.Capa.Capa.IncidentId, cancellationToken).ConfigureAwait(false)), cancellationToken).ConfigureAwait(false)));
        });
        app.MapGet("/api/capas", async (string? siteId, ICapaLifecycleService capas, IIncidentLog incidents, QualityDbContext db, CancellationToken cancellationToken) =>
        {
            var rows = await capas.ListAsync(cancellationToken).ConfigureAwait(false);
            var mapped = new List<CapaResponse>();
            foreach (var row in rows)
            {
                var site = await SiteForIncidentAsync(incidents, row.Capa.IncidentId, cancellationToken).ConfigureAwait(false);
                if (MatchesSite(siteId, site))
                {
                    mapped.Add(await WithApprovalAsync(db, ContractMapper.ToResponse(row.Capa, site, await ToolForIncidentAsync(incidents, row.Capa.IncidentId, cancellationToken).ConfigureAwait(false)), cancellationToken).ConfigureAwait(false));
                }
            }

            return Results.Ok(mapped);
        });
        app.MapGet("/api/capas/{capaId:guid}", async (Guid capaId, ICapaLifecycleService capas, IIncidentLog incidents, QualityDbContext db, CancellationToken cancellationToken) =>
        {
            var stored = await capas.GetAsync(new CapaId(capaId), cancellationToken).ConfigureAwait(false);
            var site = await SiteForIncidentAsync(incidents, stored.Capa.IncidentId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(await WithApprovalAsync(db, ContractMapper.ToResponse(stored.Capa, site, await ToolForIncidentAsync(incidents, stored.Capa.IncidentId, cancellationToken).ConfigureAwait(false)), cancellationToken).ConfigureAwait(false));
        });
        app.MapPost("/api/capas/{capaId:guid}/advance", async (
            Guid capaId,
            AdvanceCapaBody? body,
            ICapaLifecycleService capas,
            IIncidentLog incidents,
            QualityDbContext db,
            CommandGateway commands,
            HttpContext http,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            if (body?.Stage is null)
            {
                throw new IntakeValidationException("CAPA stage is required.");
            }

            commands.Ensure(LifecycleTypes.CapaAdvance, Caller(http, configuration));
            var current = await capas.GetAsync(new CapaId(capaId), cancellationToken).ConfigureAwait(false);
            var incident = await incidents.GetAsync(current.Capa.IncidentId, cancellationToken).ConfigureAwait(false);
            var correlation = incident?.Incident.CorrelationId ?? "capa-" + capaId.ToString("N");
            var toolId = incident?.Incident.ToolId.ToString();
            var parent = commands.Cascade.FindStep(correlation, "capa") ?? commands.Cascade.FindStep(correlation, "approval");
            var stored = await commands.RunAsync(
                LifecycleTypes.CapaAdvance,
                correlation,
                "quality",
                toolId,
                incident?.Incident.ChamberId,
                parent,
                new { capaId, stage = body.Stage.Value.ToString() },
                token => capas.AdvanceAsync(new CapaId(capaId), body.Stage.Value, token),
                cancellationToken).ConfigureAwait(false);
            var site = await SiteForIncidentAsync(incidents, stored.Capa.IncidentId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(await WithApprovalAsync(db, ContractMapper.ToResponse(stored.Capa, site, await ToolForIncidentAsync(incidents, stored.Capa.IncidentId, cancellationToken).ConfigureAwait(false)), cancellationToken).ConfigureAwait(false));
        });
        app.MapGet("/api/admin/session", async (HttpContext http, IConfiguration configuration, Mps.Api.Access.JoinLinkStore store, CancellationToken cancellationToken) =>
        {
            var caller = Caller(http, configuration);
            var member = !Mps.Api.Access.AccessGate.Hosted(configuration)
                || await Mps.Api.Access.AccessGate.IsAllowedAsync(http, configuration, store, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new AdminSessionResponse(caller.Mode, caller.IsAdmin, caller.DisplayName, member));
        });
        app.MapGet("/api/demo/speed", (Mps.Api.Live.PlantSimulation plant) =>
            Results.Ok(new { speed = plant.Speed, strideSeconds = plant.StrideSeconds, speeds = Mps.Api.Live.PlantSimulation.Speeds, strides = Mps.Api.Live.PlantSimulation.Strides }));
        app.MapPost("/api/demo/speed", (SpeedBody? body, HttpContext http, IConfiguration configuration, Mps.Api.Live.PlantSimulation plant) =>
        {
            if (!Caller(http, configuration).IsAdmin)
            {
                return Results.Problem(title: "Admin role is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            return body is not null && plant.TrySetSpeed(body.Speed, body.StrideSeconds)
                ? Results.Ok(new { speed = plant.Speed, strideSeconds = plant.StrideSeconds, speeds = Mps.Api.Live.PlantSimulation.Speeds, strides = Mps.Api.Live.PlantSimulation.Strides })
                : Results.Problem(title: "Speed must be 1, 2, 5 or 10 and samples every 5, 15, 30 or 60 seconds.", statusCode: StatusCodes.Status400BadRequest);
        });
        app.MapGet("/api/admin/reset/progress", (ResetProgress progress) => Results.Ok(progress.Current));
        app.MapPost("/api/admin/reset", async (HttpContext http, IConfiguration configuration, DemoResetService reset, CommandGateway commands, CancellationToken cancellationToken) =>
        {
            var caller = Caller(http, configuration);
            if (!caller.IsAdmin)
            {
                return Results.Problem(title: "Admin role is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            var hard = true;
            if (http.Request.ContentLength > 0)
            {
                ResetBody? body;
                try
                {
                    body = await http.Request.ReadFromJsonAsync<ResetBody>(ResetJson, cancellationToken).ConfigureAwait(false);
                }
                catch (JsonException)
                {
                    throw new IntakeValidationException("Reset body is not valid.");
                }

                if (string.Equals(body?.Kind, "soft", StringComparison.OrdinalIgnoreCase))
                {
                    hard = false;
                }
            }

            commands.Ensure(hard ? LifecycleTypes.ResetHard : LifecycleTypes.ResetSoft, caller);
            var baseline = await reset.ResetAsync(hard, cancellationToken).ConfigureAwait(false);
            return Results.Ok(baseline);
        });
        app.MapGet("/api/demo/data-mode", (ConsoleDataMode mode) => Results.Ok(mode.Snapshot()));
        app.MapPost("/api/demo/data-mode", async (HttpContext http, IConfiguration configuration, ConsoleDataMode mode, ReplayStore store, Mps.Api.Live.PlantSimulation plant, CancellationToken cancellationToken) =>
        {
            var caller = Caller(http, configuration);
            if (!caller.IsAdmin)
            {
                return Results.Problem(title: "Admin role is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            DataModeBody? body;
            try
            {
                body = await http.Request.ReadFromJsonAsync<DataModeBody>(ResetJson, cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                throw new IntakeValidationException("Data mode body is not valid.");
            }

            if (string.Equals(body?.Mode, "live", StringComparison.OrdinalIgnoreCase))
            {
                mode.UseLive();
                return Results.Ok(mode.Snapshot());
            }

            if (!string.Equals(body?.Mode, "replay", StringComparison.OrdinalIgnoreCase))
            {
                throw new IntakeValidationException("Data mode must be live or replay.");
            }

            if (!ScriptedScenarioCatalog.TryGet(body?.ScenarioId, out var scenario) || !store.TryLoad(scenario.Id, out var messages))
            {
                return Results.NotFound();
            }

            mode.UseReplay(scenario.Id, ReplayRebase.OntoNow(messages, plant.SimulatedNow));
            return Results.Ok(mode.Snapshot());
        });
        app.MapPost("/api/admin/replay/{scenarioId}", async (string scenarioId, HttpContext http, IConfiguration configuration, ScenarioReplayRecorder recorder, ScenarioTrigger trigger, Mps.Api.Live.PlantSimulation plant, Mps.Api.Bus.TelemetryPublisher publisher, CancellationToken cancellationToken) =>
        {
            var caller = Caller(http, configuration);
            if (!caller.IsAdmin)
            {
                return Results.Problem(title: "Admin role is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            if (!ScriptedScenarioCatalog.TryGet(scenarioId, out var scenario))
            {
                return Results.NotFound();
            }

            var count = await recorder.CaptureAsync(
                scenario.Id,
                async token =>
                {
                    var run = await trigger.RunAsync(scenario.Id, token).ConfigureAwait(false);
                    if (run is null)
                    {
                        throw new InvalidOperationException("Scenario " + scenario.Id + " did not run.");
                    }

                    // The scenario streams through the plant tick. Step the plant so the recording holds the streamed readings.
                    for (var tick = 0; tick < ReplayCaptureTicks; tick++)
                    {
                        plant.AdvanceTick();
                        await publisher.PublishPendingAsync(token).ConfigureAwait(false);
                    }
                },
                cancellationToken).ConfigureAwait(false);
            return Results.Ok(new ReplayCaptureResponse(scenario.Id, count));
        });
        app.MapPost("/api/printouts/inspect", async (HttpRequest request, IDocumentIntake intake, Mps.Api.Printouts.PrintoutFileStore files, CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
            {
                throw new IntakeValidationException("Upload a PDF or an image.");
            }

            var form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
            {
                throw new IntakeValidationException("A printout file is required.");
            }

            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            var bytes = buffer.ToArray();
            var inspection = await intake.InspectAsync(bytes, file.ContentType, file.FileName, cancellationToken).ConfigureAwait(false);
            var reading = inspection.Reading;
            var fileId = files.Add(bytes, file.ContentType, file.FileName);
            return Results.Ok(new PrintoutInspectionResponse(
                reading.ToolId,
                reading.ChamberId,
                reading.ObservedAt,
                reading.Instrument,
                reading.Parameter,
                reading.Value,
                reading.Unit,
                reading.SpecMinimum,
                reading.SpecMaximum,
                reading.Shape,
                reading.OperatorInitials,
                inspection.ParseMethod,
                reading.OutOfSpec,
                fileId));
        });
        app.MapPost("/api/printouts/confirm", async (
            PrintoutConfirmBody? body,
            IDocumentIntake intake,
            Mps.Api.Printouts.PrintoutFileStore files,
            CommandGateway commands,
            HttpContext http,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            if (body is null)
            {
                throw new IntakeValidationException("Printout body is required.");
            }

            commands.Ensure(LifecycleTypes.DocumentUpload, Caller(http, configuration));

            if (body.ObservedAt is null || body.Value is null || body.SpecMinimum is null || body.SpecMaximum is null)
            {
                throw new IntakeValidationException("Observed time, value, and spec limits are required.");
            }

            PrintoutReading reading;
            try
            {
                reading = new PrintoutReading(
                    body.ToolId ?? string.Empty,
                    body.ChamberId,
                    body.ObservedAt.Value,
                    body.Instrument ?? string.Empty,
                    body.Parameter ?? string.Empty,
                    body.Value.Value,
                    body.Unit ?? string.Empty,
                    body.SpecMinimum.Value,
                    body.SpecMaximum.Value,
                    body.Shape,
                    body.OperatorInitials)
                {
                    PrintedObservedAt = body.PrintedObservedAt,
                };
            }
            catch (ArgumentException exception)
            {
                throw new IntakeValidationException(exception.Message);
            }

            var stored = body.FileId is { } id ? files.Find(id) : null;
            var confirmed = await intake.ConfirmAsync(reading, stored is null ? null : body.FileId, stored?.FileName, cancellationToken).ConfigureAwait(false);
            return Results.Ok(confirmed);
        });
        app.MapGet("/api/manual-readings", (string? toolId, string? siteId, IManualMeasurementLog measurements, PlantSimulation plant) =>
        {
            var rows = (string.IsNullOrWhiteSpace(toolId) ? measurements.All() : measurements.ForTool(toolId.Trim()))
                .Where(item => item.Reading is not null)
                .Select(item => ManualReadings.ToResponse(item, plant))
                .Where(row => MatchesSite(siteId, row.SiteId))
                .OrderByDescending(row => row.ObservedAt)
                .ToArray();
            return Results.Ok(rows);
        });
        app.MapGet("/api/manual-readings/{evidenceId:guid}/file", (Guid evidenceId, IManualMeasurementLog measurements, Mps.Api.Printouts.PrintoutFileStore files) =>
        {
            var item = measurements.All().FirstOrDefault(row => row.Evidence.Id.Value == evidenceId);
            var file = item?.FileId is { } id ? files.Find(id) : null;
            return file is null ? Results.NotFound() : Results.File(file.Bytes, file.ContentType, file.FileName);
        });
        app.MapGet("/api/injector/scenarios", (ScenarioCardCatalog cards) =>
        {
            var rows = ScriptedScenarioCatalog.All.Select(scenario =>
            {
                var card = cards.TryGet(scenario.Id, out var found) ? found : null;
                var summary = scenario.Summary;
                return new ScenarioDescription(
                    scenario.Id,
                    scenario.Title,
                    scenario.Category,
                    scenario.ToolIds,
                    ScriptedScenario.ChamberIdFor(scenario.ToolIds[0]),
                    scenario.Parameter,
                    scenario.ShapeName,
                    summary,
                    card?.WhatsHappening ?? summary,
                    card?.WhatWeSimulate ?? summary,
                    card?.WhatAiShouldFind ?? summary,
                    card?.ExpectedCause ?? summary);
            }).ToArray();
            return Results.Ok(rows);
        });
        app.MapPost("/api/injector/scenarios/{scenarioId}", async (
            string scenarioId,
            ScenarioTrigger scenarios,
            CommandGateway commands,
            HttpContext http,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            commands.Ensure(LifecycleTypes.ScenarioStart, Caller(http, configuration));
            var run = await scenarios.RunAsync(scenarioId, cancellationToken).ConfigureAwait(false);
            if (run is null)
            {
                throw new ResourceNotFoundException("Scenario was not found.");
            }

            return Results.Json(run, statusCode: StatusCodes.Status202Accepted);
        });
        app.MapPost("/api/injector/stop", async (
            Mps.Api.Live.PlantSimulation plant,
            CommandGateway commands,
            ILifecycleEventSink events,
            TimeProvider time,
            HttpContext http,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            commands.Ensure(LifecycleTypes.ScenarioStart, Caller(http, configuration));
            var removed = plant.ClearAll();
            await events.PublishAsync(
                new LifecycleEvent(
                    LifecycleTypes.ScenarioStopped,
                    "stop-" + Guid.NewGuid().ToString("N"),
                    null,
                    null,
                    "presenter",
                    null,
                    null,
                    null,
                    time.GetUtcNow(),
                    new { removed, summary = "Scenario stopped. Telemetry returns to nominal; records stay." }),
                cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { removed });
        });
        app.MapPost("/api/faults", async (
            FaultBody? body,
            IFaultInjectionService faults,
            CommandGateway commands,
            HttpContext http,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            if (body is null)
            {
                throw new IntakeValidationException("Fault body is required.");
            }

            commands.Ensure(LifecycleTypes.FaultInject, Caller(http, configuration));

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
                    body.Confounders,
                    DurationMinutes: body.DurationMinutes is > 0 ? body.DurationMinutes : null),
                cancellationToken).ConfigureAwait(false);
            return Results.Json(new FaultAcceptedResponse(accepted.FaultId, accepted.CorrelationId), statusCode: StatusCodes.Status202Accepted);
        });
        app.MapGet("/api/trace/{eventId}", (string eventId, CausalTraceService traces) =>
        {
            var trace = traces.Build(eventId);
            return trace is null ? Results.NotFound() : Results.Ok(trace);
        });
        app.MapGet("/api/audit", async (QualityDbContext db, CancellationToken cancellationToken) =>
        {
            return Results.Ok(await AuditLog.ReadAsync(db, cancellationToken).ConfigureAwait(false));
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

    private static bool MatchesSite(string? requested, string actual) =>
        string.IsNullOrWhiteSpace(requested)
        || string.Equals(requested.Trim(), "all", StringComparison.OrdinalIgnoreCase)
        || string.Equals(requested.Trim(), actual, StringComparison.OrdinalIgnoreCase);

    private static async Task<string?> ToolForIncidentAsync(IIncidentLog incidents, IncidentId incidentId, CancellationToken cancellationToken)
    {
        var stored = await incidents.GetAsync(incidentId, cancellationToken).ConfigureAwait(false);
        return stored?.Incident.ToolId.ToString();
    }

    private static async Task<string> SiteForIncidentAsync(IIncidentLog incidents, IncidentId incidentId, CancellationToken cancellationToken)
    {
        var stored = await incidents.GetAsync(incidentId, cancellationToken).ConfigureAwait(false);
        return ContractMapper.SiteForTool(stored?.Incident.ToolId.ToString());
    }

    private static async Task<CapaResponse> WithApprovalAsync(QualityDbContext db, CapaResponse capa, CancellationToken cancellationToken)
    {
        var approval = await db.Approvals.AsNoTracking()
            .Where(row => row.TokenId == capa.ApprovalTokenId)
            .OrderByDescending(row => row.IssuedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return ContractMapper.WithApproval(capa, approval);
    }

    private static DemoCaller Caller(HttpContext http, IConfiguration configuration)
    {
        var header = http.Request.Headers[EasyAuthPrincipal.HeaderName].ToString();
        var caller = EasyAuthPrincipal.Read(string.IsNullOrWhiteSpace(header) ? null : header, configuration["Mps:Auth:Mode"]);
        return !caller.IsAdmin && Mps.Api.Access.AccessOptions.From(configuration).IsAdminEmail(Mps.Api.Access.CallerIdentity.Read(header).Email)
            ? caller with { IsAdmin = true }
            : caller;
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

public sealed record AdminSessionResponse(string Mode, bool IsAdmin, string DisplayName, bool Member = true);

public sealed record ResetBody(string? Kind);

public sealed record DataModeBody(string? Mode, string? ScenarioId);

public sealed record SpeedBody(int Speed, int StrideSeconds);
