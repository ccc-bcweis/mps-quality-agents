// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Live;

/// <summary>
/// Wall-clock tick for the in-process plant. One wall second advances <see cref="SimSecondsPerWallSecond"/> simulated seconds (demo scale 60).
/// </summary>
public sealed class SimulationOptions
{
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// When true the plant clock follows the wall clock: it starts at now() on load and on every reset, and each tick
    /// catches up to now(). Speed then scales how often a reading is kept, not how fast time passes.
    /// </summary>
    public bool RealTime { get; init; }

    public int TickSeconds { get; init; } = 2;

    public int SimSecondsPerWallSecond { get; init; } = Mps.Simulator.Demo.DemoDefaults.ClockScale;

    public int PrimeSeconds { get; init; } = 600;

    public int SampleStrideSeconds { get; init; } = 15;

    public int MaxPointsPerSeries { get; init; } = Mps.Simulator.SimulationSettings.Shared.TelemetryWindow.ReadingsPerSensor;

    /// <summary>Shortest wall time between two live hub messages for the same page (toolLive, fleetLive).</summary>
    public int PushIntervalMilliseconds { get; init; } = 1000;

    /// <summary>Shortest wall time between two clock messages to every page.</summary>
    public int ClockIntervalSeconds { get; init; } = 5;

    /// <summary>More new points than this on one series in one message sends a resync instead (a page reloads the tool).</summary>
    public int MaxPushPointsPerSeries { get; init; } = 120;

    public static SimulationOptions From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection("Mps:Simulation");
        return new SimulationOptions
        {
            Enabled = section.GetValue("Enabled", true),
            RealTime = section.GetValue("RealTime", false),
            TickSeconds = Positive(section.GetValue("TickSeconds", 2), 2),
            SimSecondsPerWallSecond = Positive(section.GetValue("SimSecondsPerWallSecond", Mps.Simulator.Demo.DemoDefaults.ClockScale), Mps.Simulator.Demo.DemoDefaults.ClockScale),
            PrimeSeconds = NonNegative(section.GetValue("PrimeSeconds", 600)),
            SampleStrideSeconds = Positive(section.GetValue("SampleStrideSeconds", 15), 15),
            // The window lives in data/metadata/simulation.yaml (telemetryWindow.readingsPerSensor), not in app settings.
            MaxPointsPerSeries = Mps.Simulator.SimulationSettings.Shared.TelemetryWindow.ReadingsPerSensor,
            PushIntervalMilliseconds = Positive(section.GetValue("PushIntervalMilliseconds", 1000), 1000),
            ClockIntervalSeconds = Positive(section.GetValue("ClockIntervalSeconds", 5), 5),
            MaxPushPointsPerSeries = Positive(section.GetValue("MaxPushPointsPerSeries", 120), 120),
        };
    }

    private static int Positive(int value, int fallback) => value > 0 ? value : fallback;

    private static int NonNegative(int value) => value < 0 ? 0 : value;
}
