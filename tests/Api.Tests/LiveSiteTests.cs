// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using Mps.Api.Live;

namespace Mps.Api.Tests;

public sealed class LiveSiteTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task LiveSnapshotsCoverEveryPlantAndSiteFiltersStayOptional()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var before = await client.GetFromJsonAsync<JsonElement>("/api/fleet/live", Json);
        before.GetProperty("tools").GetArrayLength().Should().Be(0);

        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<PlantSimulation>().Advance(TimeSpan.FromSeconds(120));
        }

        var idle = await client.GetFromJsonAsync<JsonElement>("/api/fleet/live", Json);
        idle.GetProperty("tools").GetArrayLength().Should().BeGreaterThan(0);
        var ids = idle.GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("toolId").GetString()).ToArray();
        ids.Should().Contain("D-03");
        ids.Should().Contain("I-D-01");
        ids.Should().Contain("P-D-01");
        idle.GetProperty("tools").EnumerateArray().Should().OnlyContain(tool => tool.GetProperty("siteId").GetString()!.Length > 0);
        idle.GetProperty("tools").EnumerateArray().Single(tool => tool.GetProperty("toolId").GetString() == "I-D-01").GetProperty("siteId").GetString().Should().Be("ironwood");

        var detail = await client.GetFromJsonAsync<JsonElement>("/api/tools/D-03/live", Json);
        detail.GetProperty("siteId").GetString().Should().Be("northwater");
        detail.GetProperty("readings").GetArrayLength().Should().BeGreaterThan(0);
        detail.GetProperty("series").EnumerateArray().Should().Contain(series => series.GetProperty("points").GetArrayLength() > 0);

        (await client.GetAsync("/api/tools/Z-99/live")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var everyTool = await client.GetFromJsonAsync<JsonElement>("/api/tools", Json);
        everyTool.GetArrayLength().Should().Be(32);

        var ironwood = await client.GetFromJsonAsync<JsonElement>("/api/tools?siteId=ironwood", Json);
        ironwood.EnumerateArray().Should().Contain(tool => tool.GetProperty("id").GetString() == "I-D-01" && tool.GetProperty("siteId").GetString() == "ironwood");
        ironwood.EnumerateArray().Should().NotContain(tool => tool.GetProperty("id").GetString() == "D-01");
        var clone = await client.GetFromJsonAsync<JsonElement>("/api/tools/I-D-01", Json);
        clone.GetProperty("siteId").GetString().Should().Be("ironwood");

        var opened = await client.PostAsJsonAsync("/api/incidents", new
        {
            toolId = "I-D-01",
            parameter = "filmThickness",
            summary = "Ironwood film thickness",
            severity = "high",
        });
        opened.EnsureSuccessStatusCode();
        var incident = await opened.Content.ReadFromJsonAsync<JsonElement>(Json);
        incident.GetProperty("siteId").GetString().Should().Be("ironwood");
        var filtered = await client.GetFromJsonAsync<JsonElement>("/api/incidents?siteId=ironwood", Json);
        filtered.EnumerateArray().Should().Contain(row => row.GetProperty("toolId").GetString() == "I-D-01" && row.GetProperty("siteId").GetString() == "ironwood");
        var north = await client.GetFromJsonAsync<JsonElement>("/api/incidents?siteId=northwater", Json);
        north.EnumerateArray().Should().NotContain(row => row.GetProperty("toolId").GetString() == "I-D-01");

        var tickets = await client.GetFromJsonAsync<JsonElement>("/api/tickets?siteId=Ironwood", Json);
        tickets.EnumerateArray().Should().Contain(row => row.GetProperty("toolId").GetString() == "I-D-01" && row.GetProperty("siteId").GetString() == "ironwood");

        var started = await client.PostAsync("/api/incidents/" + incident.GetProperty("id").GetGuid() + "/investigations", null);
        started.EnsureSuccessStatusCode();
        var investigation = await started.Content.ReadFromJsonAsync<JsonElement>(Json);
        investigation.GetProperty("siteId").GetString().Should().Be("ironwood");
        var listed = await client.GetFromJsonAsync<JsonElement>("/api/investigations?siteId=ironwood", Json);
        listed.EnumerateArray().Should().Contain(row => row.GetProperty("id").GetGuid() == investigation.GetProperty("id").GetGuid());
    }
}
