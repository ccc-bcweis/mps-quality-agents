// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Mps.Api.Bus;
using Mps.Api.Demo;
using Mps.Application.Errors;
using Mps.Domain.Events;
using Mps.Simulator.Scenarios;

namespace Mps.Api.Http;

public static partial class QualityEndpoints
{
    /// <summary>Presenter controls: session, speed, reset, data mode and replay capture.</summary>
    private static void MapDemoControls(WebApplication app)
    {
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

            commands.Ensure(hard ? EventTypes.ResetHard : EventTypes.ResetSoft, caller);
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
    }
}
