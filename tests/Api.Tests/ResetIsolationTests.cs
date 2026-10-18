// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using Mps.Api.Bus;
using Mps.Api.Live;

namespace Mps.Api.Tests;

/// <summary>A hard reset between two scenarios on the same tool leaves nothing from the first one behind.</summary>
public sealed class ResetIsolationTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    [Trait("Category", "Slow")]
    public async Task HardResetBetweenSc2AndSc3LeavesNoAlertFromSc2()
    {
        await using var factory = new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Mps:Simulation:SampleStrideSeconds", "120");
            builder.UseSetting("Mps:Simulation:RealTime", "false");
            builder.UseSetting("Mps:Simulation:PrimeSeconds", "0");
        });
        var client = factory.CreateClient();
        var plant = factory.Services.GetRequiredService<PlantSimulation>();
        var telemetry = factory.Services.GetRequiredService<TelemetryPublisher>();
        var spc = factory.Services.GetRequiredService<SpcRuleSubscriber>();

        async Task RunAsync(string scenarioId)
        {
            (await client.PostAsync("/api/injector/scenarios/" + scenarioId, null)).StatusCode.Should().Be(HttpStatusCode.Accepted);
            for (var step = 0; step < 6; step++)
            {
                plant.Advance(TimeSpan.FromMinutes(20));
                await telemetry.PublishPendingAsync(CancellationToken.None);
            }

            await spc.DrainAsync();
        }

        (await client.PostAsync("/api/admin/reset", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        await RunAsync("SC-2");
        var first = await client.GetFromJsonAsync<JsonElement>("/api/alerts", Json);
        first.EnumerateArray().Should().Contain(alert => alert.GetProperty("parameter").GetString() == "escTemperature");

        (await client.PostAsync("/api/admin/reset", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        plant.Runtime.Simulator.ActiveFaults.Should().BeEmpty();
        (await client.GetFromJsonAsync<JsonElement>("/api/alerts", Json)).GetArrayLength().Should().Be(0);
        (await client.GetFromJsonAsync<JsonElement>("/api/incidents", Json)).GetArrayLength().Should().Be(0);

        await RunAsync("SC-3");
        var alerts = await client.GetFromJsonAsync<JsonElement>("/api/alerts", Json);
        var incidents = await client.GetFromJsonAsync<JsonElement>("/api/incidents", Json);
        alerts.GetArrayLength().Should().BeGreaterThan(0);
        alerts.EnumerateArray().Should().OnlyContain(alert => alert.GetProperty("parameter").GetString() == "cf4Flow");
        incidents.EnumerateArray().Should().OnlyContain(incident => incident.GetProperty("parameter").GetString() == "cf4Flow");
    }
}
