// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Mps.Api.Live;
using Xunit;

namespace Mps.Api.Tests;

/// <summary>Nothing may be charted with SPC limits and never checked by the rules engine.</summary>
public sealed class SpcCoverageTests
{
    [Fact]
    public void EveryParameterChartedWithLimitsIsEvaluatedAndCountersAreNot()
    {
        using var factory = new ApiFactory();
        var plant = factory.Services.GetRequiredService<PlantSimulation>();
        var at = plant.SimulatedNow;
        var charted = 0;
        var counters = 0;
        foreach (var tool in plant.Runtime.Fleet.Tools)
        {
            foreach (var sensor in tool.Sensors)
            {
                var parameter = sensor.Parameter ?? sensor.Id;
                if (SpcCoverage.IsCumulative(sensor))
                {
                    counters++;
                    sensor.Kind.Should().Be("cumulative", $"{sensor.Id} counts up, so the metadata must say cumulative");
                    SpcCoverage.IsEvaluated(parameter, sensor.Id).Should().BeFalse();
                    continue;
                }

                sensor.Kind.Should().Be("periodic");
                SpcCoverage.IsEvaluated(parameter, sensor.Id).Should().BeTrue($"{sensor.Id} is charted with limits");
                if (sensor.Parameter is null)
                {
                    continue;
                }

                charted++;
                plant.WindowAt(tool.Id, sensor.Parameter, at).Should().NotBeNull(
                    $"{tool.Id} {sensor.Parameter} is charted with limits, so the rules engine needs the same limits to check it");
            }
        }

        charted.Should().BeGreaterThan(50);
        counters.Should().BeGreaterThan(0);
    }
}
