// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Mps.Api.Live;

namespace Mps.Api.Tests;

/// <summary>
/// Live (Oct 17): charts redrew into a different-looking graph and back. Every sensor on every tool must stream as
/// exactly one series, keyed by its parameter (or sensor id) and a real chamber (or the tool), the same key the full
/// load and the streamed updates use, so a chart can never pick up another sensor's data.
/// </summary>
public sealed class LiveSeriesKeyTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task EverySensorOnEveryToolStreamsExactlyOneSeriesUnderItsOwnKey()
    {
        await using var factory = new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Mps:Simulation:RealTime", "false");
            builder.UseSetting("Mps:Simulation:PrimeSeconds", "0");
        });
        var client = factory.CreateClient();
        var plant = factory.Services.GetRequiredService<PlantSimulation>();
        plant.Advance(TimeSpan.FromSeconds(120));

        var tools = await client.GetFromJsonAsync<JsonElement>("/api/tools", Json);
        var checkedSensors = 0;
        foreach (var tool in tools.EnumerateArray())
        {
            var toolId = tool.GetProperty("id").GetString()!;
            var live = await client.GetFromJsonAsync<JsonElement>($"/api/tools/{toolId}/live", Json);
            var keys = live.GetProperty("series").EnumerateArray()
                .Select(series => series.GetProperty("parameter").GetString() + "|" + series.GetProperty("chamberId").GetString())
                .ToList();
            keys.Should().OnlyHaveUniqueItems(toolId);

            foreach (var reading in live.GetProperty("readings").EnumerateArray())
            {
                var sensorId = reading.GetProperty("sensorId").GetString();
                var parameter = reading.GetProperty("parameter").GetString();
                var chamberId = reading.TryGetProperty("chamberId", out var chamber) ? chamber.GetString() : null;
                chamberId.Should().NotBeNullOrWhiteSpace($"{sensorId} needs a chamber (or the tool) so streamed points land in its own series");
                keys.Count(key => key == parameter + "|" + chamberId).Should().Be(1, $"{sensorId} must map to exactly one series");
                checkedSensors++;
            }

            // The streamed updates key points by the reading's chamber; a missing one would fall back to the first chamber.
            foreach (var reading in plant.Log.SnapshotForTool(toolId))
            {
                reading.ChamberId.Should().NotBeNullOrWhiteSpace($"{reading.SensorId} on {toolId}");
            }
        }

        checkedSensors.Should().BeGreaterThan(200, "every sensor on every tool is checked");
    }
}
