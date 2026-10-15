// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Live;

/// <summary>
/// Wall-clock tick for the in-process plant. One wall second advances <see cref="SimSecondsPerWallSecond"/> simulated seconds (demo scale 60).
/// </summary>
public sealed class SimulationOptions
{
    public bool Enabled { get; init; } = true;

    public int TickSeconds { get; init; } = 2;

    public int SimSecondsPerWallSecond { get; init; } = 60;

    public int PrimeSeconds { get; init; } = 600;

    public int SampleStrideSeconds { get; init; } = 15;

    public int MaxPointsPerSeries { get; init; } = 960;

    public static SimulationOptions From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection("Mps:Simulation");
        return new SimulationOptions
        {
            Enabled = section.GetValue("Enabled", true),
            TickSeconds = Positive(section.GetValue("TickSeconds", 2), 2),
            SimSecondsPerWallSecond = Positive(section.GetValue("SimSecondsPerWallSecond", 60), 60),
            PrimeSeconds = NonNegative(section.GetValue("PrimeSeconds", 600)),
            SampleStrideSeconds = Positive(section.GetValue("SampleStrideSeconds", 15), 15),
            MaxPointsPerSeries = Positive(section.GetValue("MaxPointsPerSeries", 960), 960),
        };
    }

    private static int Positive(int value, int fallback) => value > 0 ? value : fallback;

    private static int NonNegative(int value) => value < 0 ? 0 : value;
}
