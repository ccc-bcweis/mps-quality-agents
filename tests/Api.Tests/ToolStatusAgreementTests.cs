// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Xunit;

namespace Mps.Api.Tests;

/// <summary>Sites, Production line tiles and the tool page read one tool status.</summary>
public sealed class ToolStatusAgreementTests
{
    [Fact]
    public async Task SiteFloorStatusMatchesFleetLiveAndToolLiveForEveryTool()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var fleet = await client.GetFromJsonAsync<JsonElement>("/api/fleet/live");
        var liveById = fleet.GetProperty("tools").EnumerateArray()
            .ToDictionary(tool => tool.GetProperty("toolId").GetString()!, tool => tool.GetProperty("status").GetString()!);
        var sites = await client.GetFromJsonAsync<JsonElement>("/api/sites");
        var checkedTools = 0;
        foreach (var summary in sites.EnumerateArray())
        {
            var detail = await client.GetFromJsonAsync<JsonElement>($"/api/sites/{summary.GetProperty("id").GetString()}");
            foreach (var tool in detail.GetProperty("bays").EnumerateArray().SelectMany(bay => bay.GetProperty("tools").EnumerateArray()))
            {
                var id = tool.GetProperty("id").GetString()!;
                var expected = liveById.TryGetValue(id, out var live) ? live : "nominal";
                var onSite = tool.GetProperty("status").GetString();
                onSite.Should().Be(expected == "watch" ? "warning" : expected, $"{id} must read the same status on Sites as on the Production line");
                if (checkedTools < 12)
                {
                    var page = await client.GetFromJsonAsync<JsonElement>($"/api/tools/{id}/live");
                    page.GetProperty("status").GetString().Should().Be(expected, $"{id} tool page");
                }

                checkedTools++;
            }
        }

        checkedTools.Should().BeGreaterThan(10);
    }
}
