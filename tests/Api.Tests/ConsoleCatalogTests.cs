// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mps.Api.Tests;

/// <summary>
/// The console reads every label, limit and rule sentence from /api/metadata/catalog. The web tests use a checked-in
/// copy (web/src/test/catalog.fixture.json, settings.fixture.json); this test keeps those copies equal to what the server serves.
/// Set UPDATE_CATALOG_FIXTURE=1 to rewrite it after a data change.
/// </summary>
public sealed class ConsoleCatalogTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task TheCatalogCarriesTheConfiguredNumbersAndSentences()
    {
        await using var factory = new ApiFactory();
        var catalog = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/metadata/catalog", Json);

        var limits = catalog.GetProperty("limits");
        var spc = Mps.Application.Spc.SpcRuleSettings.Shared;
        limits.GetProperty("controlLimitSigma").GetDouble().Should().Be(spc.ControlLimitSigma);
        limits.GetProperty("clearReadings").GetInt32().Should().Be(spc.Clearing.Readings);
        limits.GetProperty("clearSigma").GetDouble().Should().Be(spc.Clearing.Sigma);
        limits.GetProperty("limitCheckReadings").GetInt32().Should().Be(spc.LimitCheck.RecentReadings);
        limits.GetProperty("overrideMinChars").GetInt32().Should().Be(Mps.Application.Decisions.DecisionSettings.Shared.OverrideMinChars);
        limits.GetProperty("maxEvidenceRounds").GetInt32().Should().Be(Mps.Application.Decisions.EvidenceRoundSettings.Shared.MaxRounds);
        limits.GetProperty("maxEvidenceChars").GetInt32().Should().Be(Mps.Domain.Investigations.InvestigationRounds.MaxEvidenceLength);

        string Rule(string id, string field) => catalog.GetProperty("rules").EnumerateArray()
            .Single(rule => rule.GetProperty("id").GetString() == id).GetProperty(field).GetString()!;
        Rule("western-electric-1", "label").Should().Be($"Western Electric rules, rule 1: one point is more than {spc.WesternElectric.Rule1.Sigma}σ from the average");
        Rule("western-electric-2", "brief").Should().Be($"{spc.WesternElectric.Rule2.Count} of {spc.WesternElectric.Rule2.Window} beyond {spc.WesternElectric.Rule2.Sigma} sigma, one side");
        Rule("nelson-6-rising", "plain").Should().Be($"Nelson rule 6 ({spc.NelsonRising.Count} readings in a row, each higher than the last)");

        catalog.GetProperty("parameters").EnumerateArray()
            .Single(parameter => parameter.GetProperty("id").GetString() == "cf4Flow").GetProperty("label").GetString().Should().Be("CF4 flow");
        var sites = catalog.GetProperty("sites").EnumerateArray().ToArray();
        sites.Select(site => site.GetProperty("id").GetString()).Should().Equal("northwater", "ironwood", "lumen");
        sites.Count(site => site.GetProperty("toolPrefix").GetString()!.Length == 0).Should().Be(1, "one plant keeps unprefixed tool ids");
    }

    [Theory]
    [InlineData("/api/metadata/catalog", "catalog.fixture.json")]
    [InlineData("/api/metadata/settings", "settings.fixture.json")]
    public async Task TheWebFixturesMatchWhatTheServerServes(string route, string fixture)
    {
        await using var factory = new ApiFactory();
        var served = JsonNode.Parse(await factory.CreateClient().GetStringAsync(route))!;
        var path = Path.Combine(Root(), "web", "src", "test", fixture);
        var pretty = served.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";
        if (Environment.GetEnvironmentVariable("UPDATE_CATALOG_FIXTURE") == "1")
        {
            await File.WriteAllTextAsync(path, pretty);
        }

        File.Exists(path).Should().BeTrue("the web tests and offline mocks load this file");
        JsonNode.DeepEquals(JsonNode.Parse(await File.ReadAllTextAsync(path)), served).Should()
            .BeTrue($"web/src/test/{fixture} must equal {route}; rerun with UPDATE_CATALOG_FIXTURE=1");
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MPS.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
