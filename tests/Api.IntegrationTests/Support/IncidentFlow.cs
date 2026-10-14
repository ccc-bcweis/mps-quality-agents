// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Text.Json;

namespace Mps.Api.IntegrationTests.Support;

internal readonly record struct StartedIncident(string IncidentId, string InvestigationId, JsonElement Investigation);

internal static class IncidentFlow
{
    public static async Task<StartedIncident> OpenAndStartAsync(
        HttpClient client,
        string toolId,
        string parameter,
        string summary)
    {
        var opened = (await ApiClient.SendAsync(
            client,
            HttpMethod.Post,
            "/api/incidents",
            new { toolId, parameter, summary })).Require(HttpStatusCode.Created);
        var incidentId = opened.RequiredString("id");
        var started = (await ApiClient.SendAsync(
            client,
            HttpMethod.Post,
            "/api/incidents/" + incidentId + "/investigations")).Require(HttpStatusCode.Created);
        return new StartedIncident(incidentId, started.RequiredString("id"), started);
    }

    public static async Task<ApiCall> DecideAsync(HttpClient client, string investigationId, object body)
    {
        return await ApiClient.SendAsync(
            client,
            HttpMethod.Post,
            "/api/investigations/" + investigationId + "/decisions",
            body);
    }

    public static async Task AssertNoStoredCapaAsync(HttpClient client, string incidentId)
    {
        var list = (await ApiClient.SendAsync(client, HttpMethod.Get, "/api/capas")).Require(HttpStatusCode.OK);
        list.EnumerateArray()
            .Count(item => item.GetProperty("incidentId").GetString() == incidentId)
            .Should()
            .Be(0);
    }

    public static void AssertDecisionHasNoCapa(JsonElement decision)
    {
        if (decision.TryGetProperty("capa", out var capa))
        {
            capa.ValueKind.Should().Be(JsonValueKind.Null);
        }
    }
}
