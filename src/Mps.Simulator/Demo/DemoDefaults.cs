// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

using Microsoft.Extensions.Configuration;

namespace Mps.Simulator.Demo;

/// <summary>
/// The one place for the demo's reproducibility defaults. Configuration (<c>Mps:Demo:Seed</c>, <c>Mps:Model:Seed</c>,
/// <c>Mps:Simulation:SimSecondsPerWallSecond</c>) overrides them; these values apply only when a setting is absent.
/// </summary>
public static class DemoDefaults
{
    /// <summary>Plant layout, telemetry and model replay seed (<c>Mps:Demo:Seed</c>).</summary>
    public const int PlantSeed = 4242;

    /// <summary>Enterprise records, fleet KPIs and the ROI dataset. Fixed so the seeded records match the docs.</summary>
    public const int EnterpriseSeed = 20261014;

    /// <summary>Simulated seconds per wall-clock second.</summary>
    public const int ClockScale = 60;

    public const string PlantSeedSetting = "Mps:Demo:Seed";

    public static int PlantSeedFrom(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var configured = configuration[PlantSeedSetting];
        return string.IsNullOrWhiteSpace(configured)
            ? PlantSeed
            : int.Parse(configured, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }
}
