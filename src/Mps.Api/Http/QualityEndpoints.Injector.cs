// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Bus;
using Mps.Api.Demo;
using Mps.Application.Bus;
using Mps.Application.Errors;
using Mps.Application.Faults;
using Mps.Domain.Events;
using Mps.Simulator.Scenarios;

namespace Mps.Api.Http;

public static partial class QualityEndpoints
{
    /// <summary>Scenario injector, stop and fault injection.</summary>
    private static void MapInjector(WebApplication app)
    {
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
            commands.Ensure(EventTypes.ScenarioStart, Caller(http, configuration));
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
            commands.Ensure(EventTypes.ScenarioStart, Caller(http, configuration));
            var removed = plant.ClearAll();
            await events.PublishAsync(
                new LifecycleEvent(
                    EventTypes.ScenarioStopped,
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

            commands.Ensure(EventTypes.FaultInject, Caller(http, configuration));

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
                    DurationMinutes: body.DurationMinutes is > 0 ? body.DurationMinutes : null,
                    EvidenceFaultId: string.IsNullOrWhiteSpace(body.EvidenceFaultId) ? null : body.EvidenceFaultId.Trim(),
                    Evidence: body.Evidence?.ToAttachment()),
                cancellationToken).ConfigureAwait(false);
            return Results.Json(new FaultAcceptedResponse(accepted.FaultId, accepted.CorrelationId), statusCode: StatusCodes.Status202Accepted);
        });
    }
}
