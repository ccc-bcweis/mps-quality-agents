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

        var lumenTools = await client.GetFromJsonAsync<JsonElement>("/api/tools?siteId=lumen");
        var allNamed = await client.GetFromJsonAsync<JsonElement>("/api/tools?siteId=all");
        lumenTools.GetArrayLength().Should().Be(10);
        lumenTools.EnumerateArray().Should().OnlyContain(tool => tool.GetProperty("siteId").GetString() == "lumen");

        // All sites is the union of the three plants, with no tool listed twice.
        allTools.GetArrayLength().Should().Be(32);
        allNamed.GetArrayLength().Should().Be(32);
        allTools.EnumerateArray().Select(tool => tool.GetProperty("id").GetString()).Should().OnlyHaveUniqueItems();
        allTools.EnumerateArray().Select(tool => tool.GetProperty("id").GetString()).Should().BeEquivalentTo(
            northTools.EnumerateArray().Concat(ironTools.EnumerateArray()).Concat(lumenTools.EnumerateArray()).Select(tool => tool.GetProperty("id").GetString()));
        (await client.GetFromJsonAsync<JsonElement>("/api/incidents?siteId=all")).GetArrayLength().Should().Be(1);
        northTools.GetArrayLength().Should().Be(12);
        northTools.EnumerateArray().Should().OnlyContain(tool => tool.GetProperty("siteId").GetString() == "northwater");
        ironTools.GetArrayLength().Should().Be(10);
        ironTools.EnumerateArray().Should().OnlyContain(tool => tool.GetProperty("siteId").GetString() == "ironwood");
        allIncidents.GetArrayLength().Should().Be(1);
        northIncidents.GetArrayLength().Should().Be(1);
        ironIncidents.GetArrayLength().Should().Be(0);
        northTickets.GetArrayLength().Should().Be(1);
        ironTickets.GetArrayLength().Should().Be(0);
    }
}
