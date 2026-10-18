// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using Mps.Api.Bus;
using Mps.Api.Demo;
using Mps.Api.Live;
using Mps.Application.Spc;
using Mps.Domain.Incidents;
using Mps.Simulator.Fleet;
using Mps.Simulator.Telemetry;

namespace Mps.Api.Tests;

public sealed class SpcShadowTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ASimulatedHourOfNominalTelemetryRaisesNoAlertTier()
    {
        using var factory = new ApiFactory();
        _ = factory.CreateClient();
        var plant = factory.Services.GetRequiredService<PlantSimulation>();
        var ledger = Run(plant, strideSeconds: 15, simulated: TimeSpan.FromHours(1));

        ledger.AlertRaises.Should().Be(0, ledger.Format());
        ledger.WatchRaises.Should().Be(0, ledger.Format());
    }

    [Fact]
    [Trait("Category", "Shadow")]
    public void ShadowHourAtOneXAndTenXPrintsTheCounts()
    {
        if (Environment.GetEnvironmentVariable("SPC_SHADOW") != "1")
        {
            return;
        }

        using var factory = new ApiFactory();
        _ = factory.CreateClient();
        var plant = factory.Services.GetRequiredService<PlantSimulation>();
        var one = Run(plant, strideSeconds: 15, simulated: TimeSpan.FromHours(1));
        plant.Reset();
        var ten = Run(plant, strideSeconds: 1, simulated: TimeSpan.FromHours(1));
        var report = "stride 15 (1x)" + Environment.NewLine + one.Format() + Environment.NewLine
            + "stride 1 (10x real-time cadence)" + Environment.NewLine + ten.Format();
        Console.WriteLine(report);
        one.AlertRaises.Should().Be(0, report);
        one.WatchRaises.Should().Be(0, report);
        ten.AlertRaises.Should().Be(0, report);
        ten.WatchRaises.Should().Be(0, report);
    }

    [Fact]
    public async Task AnAlertClearsAfterASoftResetWhenNominalReadingsArrive()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var plant = factory.Services.GetRequiredService<PlantSimulation>();
        var telemetry = factory.Services.GetRequiredService<TelemetryPublisher>();
        var rules = factory.Services.GetRequiredService<SpcRuleSubscriber>();

        plant.Advance(TimeSpan.FromSeconds(30));
        var onset = plant.SimulatedNow;
        plant.Inject(new FaultSignal("E-02", "E-02-A", "escTemperature", FaultShape.Step, MagnitudeKind.Sigma, 6d, onset));
        plant.Advance(TimeSpan.FromSeconds(60));
        await telemetry.PublishPendingAsync(CancellationToken.None);
        await rules.DrainAsync();

        var before = await client.GetFromJsonAsync<JsonElement>("/api/alerts?toolId=E-02", Json);
        before.EnumerateArray().Should().Contain(alert =>
            alert.GetProperty("parameter").GetString() == "escTemperature" && alert.GetProperty("status").GetString() == "active");

        await using var scope = factory.Services.CreateAsyncScope();
        var reset = scope.ServiceProvider.GetRequiredService<DemoResetService>();
        await reset.ResetAsync(hard: false, CancellationToken.None);

        var pulsed = await client.GetFromJsonAsync<JsonElement>("/api/alerts?toolId=E-02", Json);
        pulsed.EnumerateArray().Should().Contain(alert =>
            alert.GetProperty("parameter").GetString() == "escTemperature" && alert.GetProperty("status").GetString() == "active");

        plant.Advance(TimeSpan.FromMinutes(5));
        await telemetry.PublishPendingAsync(CancellationToken.None);

        var after = await client.GetFromJsonAsync<JsonElement>("/api/alerts?toolId=E-02", Json);
        after.EnumerateArray()
            .Where(alert => alert.GetProperty("parameter").GetString() == "escTemperature")
            .Should().NotBeEmpty()
            .And.OnlyContain(alert => alert.GetProperty("status").GetString() == "cleared");
    }

    private static SpcShadowLedger Run(PlantSimulation plant, int strideSeconds, TimeSpan simulated)
    {
        var engine = new SpcRuleEngine(SpcRuleSettings.Shared);
        var cap = new SpcTicketCap();
        var ledger = new SpcShadowLedger();
        var left = simulated;
        while (left > TimeSpan.Zero)
        {
            var step = left > TimeSpan.FromMinutes(10) ? TimeSpan.FromMinutes(10) : left;
            var frames = plant.Runtime.Simulator.Advance(step, strideSeconds);
            foreach (var frame in frames)
            {
                var kind = KindOf(plant, frame);
                if (!engine.Evaluates(frame.Parameter, kind))
                {
                    continue;
                }

                var window = plant.WindowAt(frame.ToolId, frame.Parameter, frame.SimulatedAt);
                if (window is null)
                {
                    continue;
                }

                var sigma = Math.Max(window.Sigma, 1e-6d);
                var observation = engine.Observe(new SpcReading(
                    frame.ToolId,
                    frame.ChamberId,
                    frame.Parameter,
                    kind,
                    frame.SimulatedAt,
                    plant.Score(frame.ToolId, frame.Parameter, frame.Value, window),
                    frame.Value,
                    window.Nominal,
                    sigma,
                    window.Minimum,
                    window.Maximum,
                    "nominal-" + frame.ToolId,
                    window.Unit));
                if (observation is null || observation.Action == SpcAction.None)
                {
                    continue;
                }

                var wouldTicket = false;
                var suppressed = false;
                if (observation.Action is SpcAction.Raise or SpcAction.Escalate
                    && observation.Tier == SpcTier.Alert
                    && observation.Severity >= Severity.High)
                {
                    if (cap.TryAdmit(frame.ToolId, frame.SimulatedAt, engine.Settings.TicketCap.PerToolPerHour))
                    {
                        wouldTicket = true;
                    }
                    else
                    {
                        suppressed = true;
                    }
                }

                ledger.Note(observation, wouldTicket, suppressed);
            }

            left -= step;
        }

        return ledger;
    }

    private static string? KindOf(PlantSimulation plant, TelemetryFrame frame)
    {
        try
        {
            var tool = plant.Runtime.Fleet.GetRequired(frame.ToolId);
            return tool.Sensors.FirstOrDefault(sensor => string.Equals(sensor.Id, frame.SensorId, StringComparison.Ordinal))?.Kind;
        }
        catch (FleetSpecException)
        {
            return null;
        }
    }
}
