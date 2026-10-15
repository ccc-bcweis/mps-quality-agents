// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Extensions.DependencyInjection;

using Mps.Api.Live;
using Mps.Simulator.Telemetry;

namespace Mps.Api.Tests;

/// <summary>
/// With no scenario and no injected fault, the whole fleet stays nominal. Only a few lone points per wall-clock hour
/// cross the 2 sigma watch line, none leave the spec window, and the next point is back to normal.
/// </summary>
public sealed class BaselineNoiseTests
{
    [Fact]
    public async Task AQuarterHourOfBaselineStaysInSpecWithOnlyRareLoneWatchPoints()
    {
        await using var factory = new ApiFactory();
        var plant = factory.Services.GetRequiredService<PlantSimulation>();
        var simSeconds = 900 * plant.Options.SimSecondsPerWallSecond;
        var frames = plant.Runtime.Simulator.Advance(TimeSpan.FromSeconds(simSeconds), plant.Options.SampleStrideSeconds);

        var tools = frames.Select(frame => frame.ToolId).Distinct(StringComparer.Ordinal).Count();
        tools.Should().Be(32);
        var watch = new List<(string Series, DateTimeOffset At)>();
        var alarms = 0;
        foreach (var frame in frames)
        {
            if (frame.Parameter.EndsWith("-OUTPUT", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var window = plant.WindowAt(frame.ToolId, frame.Parameter, frame.SimulatedAt);
            if (window is null)
            {
                continue;
            }

            if (frame.Value < window.Minimum || frame.Value > window.Maximum)
            {
                alarms++;
            }
            else if (Math.Abs(frame.Value - window.Nominal) > 2d * window.Sigma)
            {
                watch.Add((frame.ToolId + "|" + frame.ChamberId + "|" + frame.Parameter, frame.SimulatedAt));
            }
        }

        alarms.Should().Be(0, "baseline noise never leaves the spec window");
        watch.Count.Should().BeLessThanOrEqualTo(4, "a quarter hour should show at most a handful of lone points");

        // Expected lone points per wall-clock hour across the fleet: a few, not one every second.
        var chamberFrames = frames.Count(frame => !frame.Parameter.EndsWith("-OUTPUT", StringComparison.OrdinalIgnoreCase));
        var perHour = chamberFrames * 4d * BaselineNoise.ExcursionChance;
        perHour.Should().BeInRange(1d, 12d);
        watch.GroupBy(item => item.Series).Should().OnlyContain(group => group.Count() == 1, "each excursion is a single isolated point");
    }
}
