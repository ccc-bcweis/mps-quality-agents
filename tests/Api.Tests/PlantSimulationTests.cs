// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using Mps.Api.Bus;
using Mps.Api.Live;
using Mps.Domain.Identity;
using Mps.Simulator.Telemetry;

namespace Mps.Api.Tests;

public sealed class PlantSimulationTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task AStepFaultOnTheStreamRaisesAnSpcAlertAndStartsAnInvestigation()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var plant = factory.Services.GetRequiredService<PlantSimulation>();
        var telemetry = factory.Services.GetRequiredService<TelemetryPublisher>();
        var publisher = factory.Services.GetRequiredService<SpcRuleSubscriber>();
        plant.Advance(TimeSpan.FromSeconds(30));
        var onset = plant.SimulatedNow;
        plant.Inject(new FaultSignal("E-02", "E-02-A", "escTemperature", FaultShape.Step, MagnitudeKind.Sigma, 6d, onset));
        plant.Advance(TimeSpan.FromSeconds(30));

        await telemetry.PublishPendingAsync(CancellationToken.None);
        await publisher.DrainAsync();

        var alerts = await client.GetFromJsonAsync<JsonElement>("/api/alerts?toolId=E-02", Json);
        var tickets = await client.GetFromJsonAsync<JsonElement>("/api/tickets", Json);
        var incidents = await client.GetFromJsonAsync<JsonElement>("/api/incidents", Json);
        alerts.EnumerateArray().Should().Contain(alert => alert.GetProperty("parameter").GetString() == "escTemperature");
        tickets.EnumerateArray().Should().Contain(ticket => ticket.GetProperty("title").GetString()!.Contains("ESC temperature", StringComparison.Ordinal));
        incidents.EnumerateArray().Should().Contain(incident => incident.GetProperty("parameter").GetString() == "escTemperature");

        var tools = new DemoSignalTools(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            factory.Services.GetRequiredService<CrossSiteMaterialCatalog>(),
            plant);
        var window = await tools.GetWindowAsync(
            new ToolId("E-02"),
            null,
            "escTemperature",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddYears(80),
            CancellationToken.None);
        window.Points.Should().NotBeEmpty();
        window.Points[^1].Value.Should().BeGreaterThan(window.Points[0].Value + 1d);

        plant.Reset();
        publisher.Clear();
        var nominal = plant.Log.Window("E-02", "E-02-A", "escTemperature");
        nominal.Should().BeEmpty();
        plant.Runtime.Simulator.ActiveFaults.Should().BeEmpty();
    }

    [Fact]
    public async Task ResetReturnsTheLiveSeriesToNominal()
    {
        await using var factory = new ApiFactory();
        _ = factory.CreateClient();
        var plant = factory.Services.GetRequiredService<PlantSimulation>();
        plant.Advance(TimeSpan.FromSeconds(30));
        var baseline = plant.Log.Window("D-03", "D-03-A", "filmThickness").Select(point => point.Value).ToArray();
        plant.Inject(new FaultSignal("D-03", "D-03-A", "filmThickness", FaultShape.Step, MagnitudeKind.Percent, -8d, plant.SimulatedNow));
        plant.Advance(TimeSpan.FromSeconds(30));
        var shifted = plant.Log.Window("D-03", "D-03-A", "filmThickness").Select(point => point.Value).ToArray();

        plant.Reset();
        plant.Advance(TimeSpan.FromSeconds(30));
        var restored = plant.Log.Window("D-03", "D-03-A", "filmThickness").Select(point => point.Value).ToArray();

        baseline.Should().NotBeEmpty();
        shifted.Should().NotEqual(baseline);
        restored.Should().Equal(baseline);
        plant.Log.Window("I-D-01", "I-D-01-A", "filmThickness").Should().NotBeEmpty();
    }
}
