// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using Mps.Api.Bus;
using Mps.Api.Live;
using Mps.Simulator.Scenarios;
using Mps.Simulator.Telemetry;

using Xunit;

namespace Mps.Api.Tests;

/// <summary>
/// With no model configured the real agents run offline on the saved answers the replay eval scores: every scripted
/// scenario reaches its expected root-cause category through the evidence gate and the guards, with no network.
/// </summary>
[Trait("Category", "Slow")]
public sealed class OfflineScenarioTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static TheoryData<string> Scenarios => new() { "SC-1", "SC-2", "SC-3", "SC-4", "SC-5", "SC-6", "SC-7", "SC-8" };

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task EachScenarioGetsItsExpectedCauseOffline(string scenarioId)
    {
        await using var factory = Offline();
        var client = factory.CreateClient();
        var card = await CardAsync(client, scenarioId);

        (await InjectAsync(factory, client, scenarioId)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var done = await RunUntilFinishedAsync(factory, client, scenarioId);

        done.GetProperty("stage").GetString().Should().Be("recommendationReady", "scenario {0} should recommend: {1}", scenarioId, done.GetRawText());
        var top = done.GetProperty("hypotheses")[0];
        top.GetProperty("category").GetString().Should().Be(card.GetProperty("rootCauseCategory").GetString());
        top.GetProperty("cause").GetString().Should().NotContain("Placeholder");
        done.GetProperty("evidence").GetArrayLength().Should().BeGreaterThan(1);
        var status = await client.GetFromJsonAsync<JsonElement>("/api/model/status", Json);
        status.GetProperty("label").GetString().Should().Be("Offline (saved answers)");
    }

    [Fact]
    public async Task ApprovingAnOfflineRecommendationCreatesACapaWithTheDevelopmentKey()
    {
        await using var factory = Offline();
        var client = factory.CreateClient();
        await InjectAsync(factory, client, "SC-1");
        var done = await RunUntilFinishedAsync(factory, client, "SC-1");

        var approved = await client.PostAsJsonAsync("/api/investigations/" + done.GetProperty("id").GetGuid().ToString("D") + "/decisions", new
        {
            kind = "approve",
            approverId = "eng-17",
            comment = "Hold the lot and review the edge ring change.",
        });
        var body = await approved.Content.ReadFromJsonAsync<JsonElement>(Json);

        approved.StatusCode.Should().Be(HttpStatusCode.OK, body.GetRawText());
        body.GetProperty("capa").GetProperty("approvedBy").GetString().Should().Be("eng-17");
    }

    [Fact]
    public async Task AddedEvidenceRunsAnotherOfflineRound()
    {
        await using var factory = Offline();
        var client = factory.CreateClient();
        await InjectAsync(factory, client, "SC-7");
        var first = await RunUntilFinishedAsync(factory, client, "SC-7");
        var incidentId = first.GetProperty("incidentId").GetGuid();

        var added = await client.PostAsJsonAsync("/api/investigations/" + first.GetProperty("id").GetGuid().ToString("D") + "/decisions", new
        {
            kind = "requestInfo",
            approverId = "eng-17",
            comment = "The foreline pump bearing on D-02 is past its service interval.",
        });
        var second = await EvidenceRounds.WaitForRoundAsync(client, incidentId, 2);

        added.StatusCode.Should().Be(HttpStatusCode.OK);
        second.GetProperty("stage").GetString().Should().Be("recommendationReady");
        second.GetProperty("evidence").EnumerateArray().Should().Contain(item => item.GetProperty("source").GetString() == "userProvided");
    }

    private static WebApplicationFactory<Program> Offline() =>
        new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Mps:Model:Mode", "offline");

            // The test steps the plant itself (no wall-clock loop), so the agents never wait for more readings.
            builder.UseSetting("Mps:Simulation:RealTime", "false");
            builder.UseSetting("Mps:Simulation:PrimeSeconds", "0");
        });

    /// <summary>Runs ten minutes of healthy plant first (the shape lead-in), so the trace has a baseline before the fault (as the primed live plant does).</summary>
    private static async Task<HttpResponseMessage> InjectAsync(WebApplicationFactory<Program> factory, HttpClient client, string scenarioId)
    {
        var plant = factory.Services.GetRequiredService<PlantSimulation>();
        var telemetry = factory.Services.GetRequiredService<TelemetryPublisher>();
        var spc = factory.Services.GetRequiredService<StreamSpcPublisher>();
        plant.Advance(TimeSpan.FromMinutes(10));
        await telemetry.PublishPendingAsync(CancellationToken.None);
        await spc.PublishAsync(CancellationToken.None);
        await spc.DrainAsync();
        return await client.PostAsync("/api/injector/scenarios/" + scenarioId, null);
    }

    /// <summary>Steps the plant a few simulated minutes at a time through telemetry and SPC until an investigation finishes.</summary>
    private static async Task<JsonElement> RunUntilFinishedAsync(WebApplicationFactory<Program> factory, HttpClient client, string scenarioId)
    {
        var plant = factory.Services.GetRequiredService<PlantSimulation>();
        var telemetry = factory.Services.GetRequiredService<TelemetryPublisher>();
        var spc = factory.Services.GetRequiredService<StreamSpcPublisher>();
        // The live app holds the investigation until 20 or more readings since onset arrive (ShapeWait in
        // data/metadata/investigation.yaml). Stepped time has no such wait, so the first step carries about that many.
        // Off the wall clock a ramp develops at its natural pace (hours), so a ramp's first step carries the whole ramp.
        var ramp = ScriptedScenarioCatalog.All.Single(scenario => scenario.Id == scenarioId).Shape == FaultShape.Ramp;
        plant.Advance(ramp ? TimeSpan.FromHours(2) : TimeSpan.FromMinutes(6));
        for (var step = 0; step < 240; step++)
        {
            if (step > 0)
            {
                plant.Advance(TimeSpan.FromMinutes(2));
            }

            await telemetry.PublishPendingAsync(CancellationToken.None);
            await spc.PublishAsync(CancellationToken.None);
            await spc.DrainAsync();
            var finished = await FinishedOrNullAsync(client);
            if (finished is { } found)
            {
                return found;
            }

            if (await AnyInvestigationAsync(client))
            {
                return await FinishedAsync(client);
            }
        }

        throw new TimeoutException("The scenario never opened an investigation.");
    }

    private static async Task<bool> AnyInvestigationAsync(HttpClient client) => (await ItemsAsync(client)).GetArrayLength() > 0;

    private static async Task<JsonElement> ItemsAsync(HttpClient client)
    {
        var list = await client.GetFromJsonAsync<JsonElement>("/api/investigations", Json);
        return list.ValueKind == JsonValueKind.Array ? list : list.GetProperty("items");
    }

    private static async Task<JsonElement?> FinishedOrNullAsync(HttpClient client)
    {
        var done = (await ItemsAsync(client)).EnumerateArray().FirstOrDefault(item => item.GetProperty("stage").GetString() is "recommendationReady" or "escalated");
        return done.ValueKind == JsonValueKind.Object ? done : null;
    }

    private static async Task<JsonElement> CardAsync(HttpClient client, string scenarioId)
    {
        var cards = await client.GetFromJsonAsync<JsonElement>("/api/injector/scenarios", Json);
        return cards.EnumerateArray().Single(card => card.GetProperty("id").GetString() == scenarioId);
    }

    private static async Task<JsonElement> FinishedAsync(HttpClient client)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        while (true)
        {
            if (await FinishedOrNullAsync(client) is { } done)
            {
                return done;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("No investigation finished: " + (await ItemsAsync(client)).GetRawText());
            }

            await Task.Delay(250);
        }
    }
}
