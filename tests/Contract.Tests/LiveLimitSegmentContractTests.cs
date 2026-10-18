// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using Mps.Api.Live;

namespace Mps.Contract.Tests;

/// <summary>
/// Live (Oct 17): the chamber pressure chart drew one recipe step's limits over a window that spans several recipe
/// steps, so the limits flipped and healthy points from an earlier step sat outside them. The live series now carry
/// limits per recipe step, matching the contract and the limits the rules engine scores each point against.
/// </summary>
public sealed class LiveLimitSegmentContractTests : IClassFixture<MpsApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly MpsApiFactory _factory;

    public LiveLimitSegmentContractTests(MpsApiFactory factory)
    {
        _factory = factory;
    }

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
