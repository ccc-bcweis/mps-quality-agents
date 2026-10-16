// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using Mps.Api.Bus;
using Mps.Api.Live;
using Mps.Api.Printouts;
using Mps.Application.Printouts;
using Mps.Simulator.Telemetry;

namespace Mps.Api.Tests;

/// <summary>
/// A bench reading is a normal sample tagged manual. It goes through the one ingest path and the same rules
/// engine as sensor telemetry: spec window, process-control rules, alert, ticket, investigation, auto-clear.
/// </summary>
public sealed class ManualSampleIngestTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Tool = "E-01";
    private const string Chamber = "E-01-A";
    private const string Parameter = "escTemperature";

    [Fact]
    public async Task OneIngestFunctionHandlesSensorAndManualSamplesIdentically()
    {
        var sensor = await RunAsync(manual: false, outOfSpec: true);
        var manual = await RunAsync(manual: true, outOfSpec: true);

        sensor.Alerts.Should().NotBeEmpty();
        manual.Alerts.Should().BeEquivalentTo(sensor.Alerts, options => options.WithStrictOrdering());
        manual.Tickets.Should().Be(sensor.Tickets);
        manual.Alerts.Should().NotContain(alert => alert.Rule == "manualMeasurement");
    }

    [Fact]
    public async Task AnOutOfSpecManualSampleOpensATicketAndAnInvestigationThenClearsOnNominalReadings()
    {
        await using var factory = Factory();
        var client = factory.CreateClient();
        var (plant, publish) = Parts(factory);
        await WarmAsync(plant, publish);

        var evidence = Guid.NewGuid();
        Ingest(plant, manual: true, outOfSpec: true, evidence);
        await publish();

        var alerts = await client.GetFromJsonAsync<JsonElement>("/api/alerts", Json);
        var tickets = await client.GetFromJsonAsync<JsonElement>("/api/tickets", Json);
        var incidents = await client.GetFromJsonAsync<JsonElement>("/api/incidents", Json);
        alerts.EnumerateArray().Should().Contain(alert => alert.GetProperty("toolId").GetString() == Tool
            && alert.GetProperty("parameter").GetString() == Parameter
            && alert.GetProperty("status").GetString() == "active");
        tickets.GetArrayLength().Should().BeGreaterThan(0);
        incidents.GetArrayLength().Should().BeGreaterThan(0);
        plant.Log.Window(Tool, Chamber, Parameter).Should().Contain(point => point.EvidenceId == evidence && point.Source == "manual");

        for (var round = 0; round < 40; round++)
        {
            plant.AdvanceTick();
            await publish();
        }

        alerts = await client.GetFromJsonAsync<JsonElement>("/api/alerts", Json);
        alerts.EnumerateArray()
            .Where(alert => alert.GetProperty("toolId").GetString() == Tool && alert.GetProperty("parameter").GetString() == Parameter)
            .Should().OnlyContain(alert => alert.GetProperty("status").GetString() != "active", "nominal readings auto-clear the alert");
    }

    [Fact]
    public async Task AnInSpecManualSampleOpensNoTicket()
    {
        var run = await RunAsync(manual: true, outOfSpec: false);

        run.Tickets.Should().Be(0);
        run.Alerts.Should().BeEmpty();
    }

    private sealed record Outcome(IReadOnlyList<(string Rule, string Severity)> Alerts, int Tickets);

    private static async Task<Outcome> RunAsync(bool manual, bool outOfSpec)
    {
        await using var factory = Factory();
        var client = factory.CreateClient();
        var (plant, publish) = Parts(factory);
        await WarmAsync(plant, publish);

        Ingest(plant, manual, outOfSpec, Guid.NewGuid());
        await publish();

        var alerts = await client.GetFromJsonAsync<JsonElement>("/api/alerts", Json);
        var tickets = await client.GetFromJsonAsync<JsonElement>("/api/tickets", Json);
        var rows = alerts.EnumerateArray()
            .Where(alert => alert.GetProperty("toolId").GetString() == Tool && alert.GetProperty("parameter").GetString() == Parameter)
            .Select(alert => (alert.GetProperty("rule").GetString()!, alert.GetProperty("severity").ToString()))
            .ToArray();
        return new Outcome(rows, tickets.GetArrayLength());
    }

    private static void Ingest(PlantSimulation plant, bool manual, bool outOfSpec, Guid evidence)
    {
        var window = plant.WindowAt(Tool, Parameter, plant.SimulatedNow)!;
        var value = outOfSpec ? window.Maximum + (window.Maximum - window.Minimum) : window.Nominal;
        var frame = PlantManualSampleSink.ToFrame(plant, new ManualSample(Tool, Chamber, Parameter, window.Unit, plant.SimulatedNow, value, evidence))!;
        if (!manual)
        {
            frame = frame with { Source = TelemetryFrame.Sources.Sensor, EvidenceId = null };
        }

        plant.Ingest([frame]);
    }

    private static async Task WarmAsync(PlantSimulation plant, Func<Task> publish)
    {
        for (var tick = 0; tick < 30; tick++)
        {
            plant.AdvanceTick();
        }

        await publish();
    }

    private static (PlantSimulation Plant, Func<Task> Publish) Parts(WebApplicationFactory<Program> factory)
    {
        var plant = factory.Services.GetRequiredService<PlantSimulation>();
        var telemetry = factory.Services.GetRequiredService<TelemetryPublisher>();
        var spc = factory.Services.GetRequiredService<StreamSpcPublisher>();
        return (plant, async () =>
        {
            await telemetry.PublishPendingAsync(CancellationToken.None);
            await spc.PublishAsync(CancellationToken.None);
            await spc.DrainAsync();
        }
        );
    }

    private static WebApplicationFactory<Program> Factory() => new ApiFactory().WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Mps:Simulation:RealTime", "false");
        builder.UseSetting("Mps:Simulation:PrimeSeconds", "0");
    });
}
