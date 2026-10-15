// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net.Http.Json;
using System.Text.Json;

namespace Mps.Api.Tests;

public sealed class SiteFilterTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ListRoutesKeepEveryPlantUntilASiteIsNamed()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var opened = await client.PostAsJsonAsync("/api/incidents", new
        {
            toolId = "E-02",
            parameter = "escTemperature",
            summary = "Site filter probe",
            severity = "low",
        });
        opened.EnsureSuccessStatusCode();

        var allTools = await client.GetFromJsonAsync<JsonElement>("/api/tools");
        var northTools = await client.GetFromJsonAsync<JsonElement>("/api/tools?siteId=northwater");
        var ironTools = await client.GetFromJsonAsync<JsonElement>("/api/tools?siteId=ironwood");
        var allIncidents = await client.GetFromJsonAsync<JsonElement>("/api/incidents");
        var northIncidents = await client.GetFromJsonAsync<JsonElement>("/api/incidents?siteId=Northwater");
        var ironIncidents = await client.GetFromJsonAsync<JsonElement>("/api/incidents?siteId=ironwood");
        var northTickets = await client.GetFromJsonAsync<JsonElement>("/api/tickets?siteId=northwater");
        var ironTickets = await client.GetFromJsonAsync<JsonElement>("/api/tickets?siteId=lumen");

        allTools.GetArrayLength().Should().Be(8);
        northTools.GetArrayLength().Should().Be(8);
        northTools.EnumerateArray().Should().OnlyContain(tool => tool.GetProperty("siteId").GetString() == "northwater");
        ironTools.GetArrayLength().Should().Be(0);
        allIncidents.GetArrayLength().Should().Be(1);
        northIncidents.GetArrayLength().Should().Be(1);
        ironIncidents.GetArrayLength().Should().Be(0);
        northTickets.GetArrayLength().Should().Be(1);
        ironTickets.GetArrayLength().Should().Be(0);
    }
}
