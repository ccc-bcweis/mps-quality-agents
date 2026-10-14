// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Options;

namespace Mps.MockEnterprise;

public static class EnterpriseEndpoints
{
    public static void MapEnterpriseEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/mes/runs", (string? toolId, DateTimeOffset? from, DateTimeOffset? until, EnterpriseStore store) =>
        {
            if (MissingTool(toolId, out var id) is { } error)
            {
                return error;
            }

            return Results.Ok(store.RunsFor(id, from, until));
        });
        app.MapGet("/mes/lots", (string? toolId, EnterpriseStore store) =>
        {
            if (MissingTool(toolId, out var id) is { } error)
            {
                return error;
            }

            return Results.Ok(store.LotsFor(id));
        });
        app.MapGet("/qms/ncrs", (string? toolId, EnterpriseStore store) =>
        {
            if (MissingTool(toolId, out var id) is { } error)
            {
                return error;
            }

            return Results.Ok(store.NcrsFor(id));
        });
        app.MapGet("/plm/ecos", (string? toolId, EnterpriseStore store) =>
        {
            if (MissingTool(toolId, out var id) is { } error)
            {
                return error;
            }

            return Results.Ok(store.EcosFor(id));
        });
        app.MapGet("/plm/config", (string? toolId, EnterpriseStore store) =>
        {
            if (MissingTool(toolId, out var id) is { } error)
            {
                return error;
            }

            var config = store.ConfigFor(id);
            return config is null ? Results.NotFound() : Results.Ok(config);
        });
        app.MapGet("/erp/lots", (string? toolId, EnterpriseStore store) =>
        {
            if (MissingTool(toolId, out var id) is { } error)
            {
                return error;
            }

            return Results.Ok(store.MaterialsFor(id));
        });
        app.MapGet("/erp/notices", (string? materialCode, EnterpriseStore store) => Results.Ok(store.NoticesFor(materialCode)));
        app.MapGet("/cmms/work-orders", (string? toolId, EnterpriseStore store) =>
        {
            if (MissingTool(toolId, out var id) is { } error)
            {
                return error;
            }

            return Results.Ok(store.WorkOrdersFor(id));
        });
        app.MapGet("/cmms/calibrations", (string? toolId, EnterpriseStore store) =>
        {
            if (MissingTool(toolId, out var id) is { } error)
            {
                return error;
            }

            return Results.Ok(store.CalibrationsFor(id));
        });
        app.MapPost("/qms/capas", (HttpRequest request, EnterpriseStore store, IOptions<MockOptions> options, CancellationToken cancellationToken) =>
            WriteCapaAsync(request, store, options.Value, cancellationToken));
        app.MapPost("/ticketing/tickets", (HttpRequest request, EnterpriseStore store, IOptions<MockOptions> options, CancellationToken cancellationToken) =>
            WriteTicketAsync(request, store, options.Value, cancellationToken));
    }

    private static async Task<IResult> WriteCapaAsync(HttpRequest request, EnterpriseStore store, MockOptions options, CancellationToken cancellationToken)
    {
        if (await InjectedFaultAsync(request, options, cancellationToken).ConfigureAwait(false) is { } fault)
        {
            return fault;
        }

        var body = await ReadAsync<CapaWrite>(request, cancellationToken).ConfigureAwait(false);
        if (body is null || body.Extra is { Count: > 0 } || !GuidText(body.CapaId, out var capaId) || !GuidText(body.IncidentId, out var incidentId) || !GuidText(body.ApprovalTokenId, out _) || string.IsNullOrWhiteSpace(body.Summary))
        {
            return Results.BadRequest();
        }

        var outcome = store.AddCapa(capaId, incidentId);
        return outcome.Id is null ? Results.StatusCode(outcome.StatusCode) : Results.Json(new { externalId = outcome.Id }, statusCode: outcome.StatusCode);
    }

    private static async Task<IResult> WriteTicketAsync(HttpRequest request, EnterpriseStore store, MockOptions options, CancellationToken cancellationToken)
    {
        if (await InjectedFaultAsync(request, options, cancellationToken).ConfigureAwait(false) is { } fault)
        {
            return fault;
        }

        var body = await ReadAsync<TicketWrite>(request, cancellationToken).ConfigureAwait(false);
        if (body is null || body.Extra is { Count: > 0 } || !GuidText(body.IncidentId, out var incidentId) || string.IsNullOrWhiteSpace(body.ToolId) || string.IsNullOrWhiteSpace(body.Title))
        {
            return Results.BadRequest();
        }

        var outcome = store.AddTicket(incidentId);
        return outcome.Id is null ? Results.StatusCode(outcome.StatusCode) : Results.Json(new { number = outcome.Id }, statusCode: outcome.StatusCode);
    }

    private static async Task<IResult?> InjectedFaultAsync(HttpRequest request, MockOptions options, CancellationToken cancellationToken)
    {
        if (!request.Headers.TryGetValue("X-Mock-Fault", out var header))
        {
            return null;
        }

        switch (header.ToString())
        {
            case "500":
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            case "409":
                return Results.StatusCode(StatusCodes.Status409Conflict);
            case "timeout":
                await Task.Delay(TimeSpan.FromSeconds(options.TimeoutSeconds), cancellationToken).ConfigureAwait(false);
                return Results.StatusCode(StatusCodes.Status504GatewayTimeout);
            default:
                return Results.BadRequest();
        }
    }

    private static async Task<T?> ReadAsync<T>(HttpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await request.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static IResult? MissingTool(string? toolId, out string id)
    {
        if (string.IsNullOrWhiteSpace(toolId))
        {
            id = string.Empty;
            return Results.BadRequest();
        }

        id = toolId.Trim();
        return null;
    }

    private static bool GuidText(string? value, out string normalized)
    {
        if (Guid.TryParse(value, out var parsed) && parsed != Guid.Empty)
        {
            normalized = parsed.ToString("D");
            return true;
        }

        normalized = string.Empty;
        return false;
    }

    private sealed class CapaWrite
    {
        public string? CapaId { get; set; }

        public string? IncidentId { get; set; }

        public string? ApprovalTokenId { get; set; }

        public string? Summary { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    private sealed class TicketWrite
    {
        public string? IncidentId { get; set; }

        public string? ToolId { get; set; }

        public string? Title { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }
}
