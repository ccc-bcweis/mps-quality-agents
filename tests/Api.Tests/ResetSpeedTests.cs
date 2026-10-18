// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Mps.Api.Bus;
using Mps.Api.Demo;
using Mps.Api.Live;
using Mps.Simulator;

namespace Mps.Api.Tests;

/// <summary>
/// Regression (live, Oct 17 11:25 PM ET): an idle hard reset took 49.5 s on the 0.25 vCPU app and the plant clock then
/// stayed frozen about 95 s, because the reset reseeded a week of history through the event bus. Telemetry is now a
/// fixed rolling window per sensor (data/metadata/simulation.yaml, telemetryWindow): the reset clears it and the plant
/// primes one window, so the reset is quick and the clock is current right after it reports done.
/// Runs with the other reset test: the reset gate is process-wide.
/// </summary>
[Collection(nameof(ResetCancelsInvestigationsTests))]
public sealed class ResetSpeedTests
{
    [Fact]
    public async Task AHardResetFinishesUnderTenSecondsAndTheClockMovesRightAway()
    {
        await using var factory = new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Mps:Simulation:Enabled", "true");
            builder.UseSetting("Mps:Simulation:RealTime", "true");
        });
        _ = factory.CreateClient();
        var plant = factory.Services.GetRequiredService<PlantSimulation>();
        var store = factory.Services.GetRequiredService<SqliteEventStore>();
        await using var scope = factory.Services.CreateAsyncScope();
        var reset = scope.ServiceProvider.GetRequiredService<DemoResetService>();

        var watch = System.Diagnostics.Stopwatch.StartNew();
        await reset.ResetAsync(hard: true, CancellationToken.None);
        watch.Stop();
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));

        // The plant restarted its clock at the end of the reset, so it is current, not minutes behind and catching up.
        var lag = DateTimeOffset.UtcNow - plant.SimulatedNow;
        lag.Should().BeLessThan(TimeSpan.FromSeconds(5), "the plant clock must be current as soon as the reset reports done");
        var moved = plant.SimulatedNow;
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (plant.SimulatedNow <= moved && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        plant.SimulatedNow.Should().BeAfter(moved, "the clock moves on the first tick after the reset");

        // The primed window, and nothing older, is what the plant and the telemetry table hold.
        var window = SimulationSettings.Shared.TelemetryWindow.ReadingsPerSensor;
        plant.Log.MaxPoints.Should().Be(window);
        store.TelemetryRowsPerSeries.Should().Be(window);
    }
}
