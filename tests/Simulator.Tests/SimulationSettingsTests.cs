// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using FluentAssertions;

using Mps.Simulator;
using Mps.Simulator.Sites;
using Mps.Simulator.Telemetry;

namespace Mps.Simulator.Tests;

/// <summary>The simulator's shape comes from data/metadata/simulation.yaml; the shipped values match what was hard-coded before.</summary>
public sealed class SimulationSettingsTests
{
    [Fact]
    public void ShippedValuesAreServedThroughTheOldNames()
    {
        RecipeSchedule.StepSeconds.Should().Be(120);
        RecipeSchedule.WaferSeconds.Should().Be(300);
        RecipeSchedule.WafersPerLot.Should().Be(25);
        BaselineNoise.ExcursionSigma.Should().Be(2.4d);
        BaselineNoise.ExcursionChance.Should().Be(4e-7d);
        TelemetryMath.Phi.Should().Be(0.82d);
        TelemetryMath.NoiseScale.Should().Be(0.12d);
        SiteComparison.SpreadRatio.Should().Be(1.5d);
        SimulationSettings.Shared.RealTimeFaultPace.Should().Be(6d);
    }

    [Fact]
    public void MissingSettingFailsAtLoad()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "recipeSchedule: { stepSeconds: 120, waferSeconds: 300 }\n");
            var act = () => SimulationSettings.Load(path);
            act.Should().Throw<InvalidOperationException>().WithMessage("*missing*");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
