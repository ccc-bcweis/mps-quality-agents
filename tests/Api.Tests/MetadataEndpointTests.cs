// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Mps.Api.Tests;

public sealed class MetadataEndpointTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task CompanySitesToolRulesAndMapsMatchTheCatalog()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var company = await client.GetFromJsonAsync<JsonElement>("/api/metadata/company", Json);
        company.GetProperty("name").GetString().Should().Be("Meridian Precision Systems");
        company.GetProperty("loadMilliseconds").GetDouble().Should().BeGreaterThanOrEqualTo(0);

        var sites = await client.GetFromJsonAsync<JsonElement>("/api/metadata/sites", Json);
        sites!.GetArrayLength().Should().Be(3);
        var north = sites.EnumerateArray().Single(site => site.GetProperty("id").GetString() == "northwater");
        var manufacturing = north.GetProperty("capabilities").EnumerateArray()
            .Single(capability => capability.GetProperty("name").GetString() == "Manufacturing");
        manufacturing.GetProperty("simulated").GetBoolean().Should().BeTrue();
        manufacturing.GetProperty("lines").EnumerateArray().Select(line => line.GetProperty("name").GetString())
            .Should().Contain("Northwater wafer flow");
        var warehousing = north.GetProperty("capabilities").EnumerateArray()
            .Single(capability => capability.GetProperty("name").GetString() == "Warehousing");
        warehousing.GetProperty("simulated").GetBoolean().Should().BeFalse();

        var bench = manufacturing.GetProperty("directTools").EnumerateArray().Single();
        bench.GetProperty("name").GetString().Should().Be("Film check bench");
        bench.GetProperty("simulated").GetBoolean().Should().BeFalse();

        var tool = await client.GetFromJsonAsync<JsonElement>("/api/metadata/tools/E-01", Json);
        tool.GetProperty("siteName").GetString().Should().Be("Northwater Fab");
        tool.GetProperty("lineName").GetString().Should().Be("Northwater wafer etch");
        var temperature = tool.GetProperty("parameters").EnumerateArray()
            .Single(parameter => parameter.GetProperty("id").GetString() == "escTemperature");
        temperature.GetProperty("lcl").GetDouble().Should().Be(40);
        temperature.GetProperty("ucl").GetDouble().Should().Be(60);

        var direct = await client.GetFromJsonAsync<JsonElement>("/api/metadata/tools/nw-film-check", Json);
        direct.GetProperty("lineId").ValueKind.Should().Be(JsonValueKind.Null);

        var missing = await client.GetAsync("/api/metadata/tools/Z-99");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var rules = await client.GetFromJsonAsync<JsonElement>("/api/metadata/rules", Json);
        rules!.EnumerateArray().First().GetProperty("id").GetString().Should().Be("rule-critical-human");
        rules.EnumerateArray().Should().Contain(rule => rule.GetProperty("whenPlain").GetString()!.Contains("critical", StringComparison.Ordinal));

        var critical = rules.EnumerateArray().First().GetProperty("conditions").EnumerateArray().Single();
        critical.GetProperty("field").GetString().Should().Be("severity");
        critical.GetProperty("value").GetString().Should().Be("critical");
        rules.EnumerateArray().Single(rule => rule.GetProperty("id").GetString() == "rule-low-confidence")
            .GetProperty("conditions").EnumerateArray().Single().GetProperty("value").GetString().Should().Be("0.5");

        var settings = await client.GetFromJsonAsync<JsonElement>("/api/metadata/settings", Json);
        string Setting(string id) => settings.EnumerateArray().Single(row => row.GetProperty("id").GetString() == id).GetProperty("value").GetString()!;
        Setting("spc.controlLimitSigma").Should().Be("3", "spc-rules.yaml sets control limits at 3 sigma");
        Setting("spc.we4.count").Should().Be("8");
        Setting("clear.readings").Should().Be("8");
        Setting("decision.overrideMin").Should().Be("20");

        var maps = await client.GetFromJsonAsync<JsonElement>("/api/metadata/process-maps", Json);
        maps!.EnumerateArray().Select(map => map.GetProperty("state").GetString()).Should().Equal("current", "future");
    }

    [Fact]
    public async Task ResetDoesNotDropMetadata()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var before = await client.GetFromJsonAsync<JsonElement>("/api/metadata/company", Json);
        var reset = await client.PostAsync("/api/admin/reset", null);
        reset.EnsureSuccessStatusCode();
        var after = await client.GetFromJsonAsync<JsonElement>("/api/metadata/company", Json);
        after.GetProperty("name").GetString().Should().Be(before.GetProperty("name").GetString());
        after.GetProperty("industry").GetString().Should().Be("Semiconductor manufacturing");
    }
}
