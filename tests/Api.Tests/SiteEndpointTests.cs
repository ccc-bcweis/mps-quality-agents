// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Mps.Api.Live;
using Mps.Domain.Identity;
using Mps.Domain.Impact;

namespace Mps.Api.Tests;

public sealed class SiteEndpointTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    private readonly ApiFactory _factory;

    public SiteEndpointTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task SitesListDetailAndCompareMatchTheContract()
    {
        var client = _factory.CreateClient();
        var list = await client.GetFromJsonAsync<JsonElement>("/api/sites");
        list.GetArrayLength().Should().Be(3);
        list.EnumerateArray().Select(site => site.GetProperty("id").GetString()).Should().Equal("northwater", "ironwood", "lumen");
        var northSummary = list.EnumerateArray().First();
        northSummary.GetProperty("role").GetString().Should().Be("Front-end wafer fab");
        northSummary.GetProperty("location").GetString().Should().Be("Cedar Reach, Valecrest");
        northSummary.GetProperty("bays").GetArrayLength().Should().BeInRange(3, 5);
        northSummary.GetProperty("kpis").GetProperty("waferStartsPerHour").GetDouble().Should().BeGreaterThan(0);
        northSummary.GetProperty("kpis").GetProperty("wip").GetInt32().Should().BeGreaterThan(0);

        var detail = await client.GetFromJsonAsync<JsonElement>("/api/sites/northwater");
        detail.GetProperty("flows").GetArrayLength().Should().BeGreaterThan(0);
        var placed = detail.GetProperty("bays").EnumerateArray().SelectMany(bay => bay.GetProperty("tools").EnumerateArray()).ToArray();
        placed.Should().Contain(tool => tool.GetProperty("id").GetString() == "D-01" && tool.GetProperty("siteId").GetString() == "northwater");
        placed.Should().OnlyContain(tool => tool.GetProperty("x").GetDouble() >= 4 && tool.GetProperty("x").GetDouble() <= 96 && tool.GetProperty("y").GetDouble() >= 4 && tool.GetProperty("y").GetDouble() <= 96);
        detail.GetProperty("bays").EnumerateArray().First().GetProperty("kpis").GetProperty("yieldPct").GetDouble().Should().BeInRange(80, 100);

        var compare = await client.GetFromJsonAsync<JsonElement>("/api/sites/compare?family=etch");
        compare.GetProperty("family").GetString().Should().Be("etch");
        compare.GetProperty("referenceSiteId").GetString().Should().Be("northwater");
        var flagged = compare.GetProperty("parameters").EnumerateArray()
            .SelectMany(parameter => parameter.GetProperty("sites").EnumerateArray()
                .Where(site => site.GetProperty("discrepancy").GetBoolean())
                .Select(site => parameter.GetProperty("parameter").GetString() + "@" + site.GetProperty("siteId").GetString()))
            .ToArray();
        flagged.Should().BeEquivalentTo("escTemperature@ironwood", "wallTemperature@ironwood");
        var northEsc = EscRow(compare, "northwater");
        northEsc.GetProperty("discrepancy").GetBoolean().Should().BeFalse();
        northEsc.GetProperty("reason").ValueKind.Should().Be(JsonValueKind.Null);

        (await client.GetAsync("/api/sites/missing")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/sites/compare")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/sites/compare?family=plasma")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var tools = await client.GetFromJsonAsync<JsonElement>("/api/tools");
        tools.GetArrayLength().Should().Be(32);
        tools.EnumerateArray().Should().OnlyContain(tool => !string.IsNullOrEmpty(tool.GetProperty("siteId").GetString()) && tool.GetProperty("model").GetString()!.StartsWith("MPS-", StringComparison.Ordinal));

        if (Environment.GetEnvironmentVariable("MPS_EXPORT_SITES") == "1")
        {
            var directory = Path.Combine(FindRepo(), "contracts", "examples", "sites");
            Directory.CreateDirectory(directory);
            await WriteExample(client, "/api/sites", Path.Combine(directory, "list.json"));
            await WriteExample(client, "/api/sites/northwater", Path.Combine(directory, "northwater.json"));
            await WriteExample(client, "/api/sites/compare?family=etch", Path.Combine(directory, "compare-etch.json"));
        }
    }

    [Fact]
    public async Task ADifferentDemoSeedMovesToolPositions()
    {
        var baseline = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/sites/northwater");
        await using var shifted = _factory.WithWebHostBuilder(builder => builder.UseSetting("Mps:Demo:Seed", "7777"));
        var moved = await shifted.CreateClient().GetFromJsonAsync<JsonElement>("/api/sites/northwater");
        Place(baseline, "D-01").Should().NotBe(Place(moved, "D-01"));
        Place(baseline, "E-01").Should().NotBe(Place(moved, "E-01"));
    }

    [Fact]
    public async Task SharedPrecursorLotIsVisibleFromBothPlants()
    {
        var seedPath = Path.Combine(FindRepo(), "data", "seed", "enterprise.json");
        var enterprise = new SeedEnterpriseTools(seedPath);
        var from = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var until = new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero);
        var north = await enterprise.GetMaterialLotsAsync(new ToolId("D-03"), from, until, CancellationToken.None);
        var iron = await enterprise.GetMaterialLotsAsync(new ToolId("I-D-01"), from, until, CancellationToken.None);
        north.Should().Contain(lot => lot.LotId == "MX4419" && lot.SupplierName == "Marrowgate Process Gases");
        iron.Should().Contain(lot => lot.LotId == "MX4419");
        var runs = await enterprise.GetRunsAsync(new ToolId("I-D-01"), from, until, CancellationToken.None);
        runs.Should().NotBeEmpty();
        runs.Should().OnlyContain(run => run.SiteId == "ironwood");
        var config = await enterprise.GetConfigAsync(new ToolId("D-03"), CancellationToken.None);
        config.Revision.Should().Be("A");
        (await enterprise.GetConfigAsync(new ToolId("D-01"), CancellationToken.None)).Revision.Should().Be("C");

        var signals = new DemoSignalTools(new UnusedScopes(), new CrossSiteMaterialCatalog(seedPath));
        var scope = await signals.GetScopeAsync(new ToolId("D-03"), "filmThickness", from, until, CancellationToken.None);
        scope.Kind.Should().Be(ImpactScopeKind.MaterialLot);
        scope.Key.Should().Be("MX4419");
        scope.AlignmentNote.Should().Contain("Northwater Fab").And.Contain("Ironwood Fab").And.Contain("MX4419");
        var other = await signals.GetScopeAsync(new ToolId("I-D-01"), "filmThickness", from, until, CancellationToken.None);
        other.Key.Should().Be(scope.Key);
    }

    private static JsonElement EscRow(JsonElement compare, string siteId)
    {
        var parameter = compare.GetProperty("parameters").EnumerateArray().Single(row => row.GetProperty("parameter").GetString() == "escTemperature");
        return parameter.GetProperty("sites").EnumerateArray().Single(row => row.GetProperty("siteId").GetString() == siteId);
    }

    private static string Place(JsonElement detail, string toolId)
    {
        var tool = detail.GetProperty("bays").EnumerateArray().SelectMany(bay => bay.GetProperty("tools").EnumerateArray()).Single(item => item.GetProperty("id").GetString() == toolId);
        return tool.GetProperty("x").GetDouble() + "," + tool.GetProperty("y").GetDouble();
    }

    private static async Task WriteExample(HttpClient client, string path, string file)
    {
        var body = await client.GetStringAsync(path);
        using var document = JsonDocument.Parse(body);
        var pretty = JsonSerializer.Serialize(document.RootElement, Pretty);
        await File.WriteAllTextAsync(file, pretty.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
    }

    private static string FindRepo()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MPS.sln")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("MPS.sln was not found above the test output directory.");
    }

    private sealed class UnusedScopes : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new InvalidOperationException("The shared-lot path does not open a database scope.");
    }
}
