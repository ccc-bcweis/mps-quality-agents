// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Mps.Api;
using Mps.Api.Live;

namespace Mps.Contract.Tests;

public sealed class MpsApiFactory : WebApplicationFactory<ApiHostMarker>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.UseSetting("Mps:Simulation:RealTime", "false");
    }
}

/// <summary>
/// Checks the running console host against the parts of the contract that are already required
/// of every build: liveness, no anonymous CAPA create, and no ground-truth route.
/// </summary>
public sealed class LiveConsoleContractTests : IClassFixture<MpsApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly MpsApiFactory _factory;

    public LiveConsoleContractTests(MpsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthReturnsTheContractShape()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "response body was {0}", body);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        root.EnumerateObject().Select(property => property.Name).Should().Equal("status", "service");
        root.GetProperty("status").GetString().Should().Be("ok");
        root.GetProperty("service").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CapaCollectionDoesNotAcceptAClientCreate()
    {
        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/capas", UriKind.Relative),
            new
            {
                incidentId = "8d0a1c4e-1b7a-4e5a-9c1d-0a6b2e3f4a5b",
                summary = "Create a CAPA without a human approval.",
            });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed },
            "response body was {0}",
            body);
        response.StatusCode.Should().NotBe(HttpStatusCode.Created);
        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/groundtruth")]
    [InlineData("/ground-truth")]
    [InlineData("/api/groundtruth")]
    [InlineData("/api/ground-truth")]
    public async Task ConsoleApiDoesNotExposeGroundTruth(string path)
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Live (Oct 17): the chamber pressure chart drew one recipe step's limits over a window that spans several recipe
    /// steps, so the limits flipped and healthy points from an earlier step sat outside them. The live series now carry
    /// limits per recipe step, matching the contract and the limits the rules engine scores each point against.
    /// </summary>
    [Fact]
    public async Task ToolLiveSeriesCarryPerStepLimitsThatMatchTheContractAndTheRulesEngine()
    {
        using var client = _factory.CreateClient();
        var plant = _factory.Services.GetRequiredService<PlantSimulation>();
        plant.Advance(TimeSpan.FromSeconds(Mps.Simulator.Telemetry.RecipeSchedule.StepSeconds * 5));

        var live = await client.GetFromJsonAsync<JsonElement>("/api/tools/E-01/live", Json);
        var seriesSchema = OpenApiDocs.Console.Schema("LiveSeries");
        var segmentSchema = OpenApiDocs.Console.Schema("LiveLimitSegment");
        segmentSchema.AdditionalPropertiesAllowed.Should().BeFalse();
        segmentSchema.Required.Should().BeEquivalentTo("from", "center", "sigma");

        var multiStep = 0;
        foreach (var series in live.GetProperty("series").EnumerateArray())
        {
            var parameter = series.GetProperty("parameter").GetString()!;
            series.EnumerateObject().Select(property => property.Name).Should().BeSubsetOf(seriesSchema.Properties.Keys, parameter);
            if (!series.TryGetProperty("segments", out var segments) || segments.ValueKind == JsonValueKind.Null)
            {
                SpcCoverage.IsCumulative(parameter, null).Should().BeTrue($"{parameter} is charted with limits, so it needs segments");
                continue;
            }

            var rows = segments.EnumerateArray().Select(segment =>
            {
                segment.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(segmentSchema.Properties.Keys);
                return (From: segment.GetProperty("from").GetDateTimeOffset(), Center: segment.GetProperty("center").GetDouble(), Sigma: segment.GetProperty("sigma").GetDouble());
            }).ToArray();
            rows.Should().NotBeEmpty(parameter);
            rows.Select(row => row.From).Should().BeInAscendingOrder(parameter);
            if (rows.Select(row => row.Center).Distinct().Count() > 1)
            {
                multiStep++;
            }

            // Each point's segment is exactly the limits the rules engine scores that point against.
            foreach (var point in series.GetProperty("points").EnumerateArray())
            {
                var at = point.GetProperty("at").GetDateTimeOffset();
                var segment = rows.Last(row => row.From <= at);
                var window = plant.WindowAt("E-01", parameter, at)!;
                segment.Center.Should().Be(window.Nominal, $"{parameter} at {at:O}");
                segment.Sigma.Should().Be(Math.Max(window.Sigma, 1e-6), $"{parameter} at {at:O}");
            }
        }

        multiStep.Should().BePositive("a window that spans several recipe steps has stepped limits");
    }
}
