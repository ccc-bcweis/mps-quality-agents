// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Simulator.Fleet;
using Mps.Simulator.Kpi;

namespace Mps.Simulator.Tests;

public sealed class FleetKpiTests
{
    private static readonly string[] ToolIds = FleetCatalogLoader.Load(RepoPaths.FleetDirectory()).Tools.Select(tool => tool.Id).ToArray();

    [Fact]
    public void DemoSeedIsReproducibleAndStaysInsideTheFormula()
    {
        var snapshot = FleetKpiCalculator.Calculate(ToolIds);
        var again = FleetKpiCalculator.Calculate(ToolIds);

        snapshot.Seed.Should().Be(FleetKpiCalculator.DemoSeed);
        snapshot.PlannedHours.Should().Be(2160);
        snapshot.IdealWafersPerHour.Should().Be(12m);
        snapshot.Tools.Select(tool => tool.ToolId).Should().Equal("C-01", "C-02", "D-01", "D-02", "E-01", "E-02", "S-01", "S-02");
        FleetKpiYaml.Render(again).Should().Be(FleetKpiYaml.Render(snapshot));
        FleetKpiCalculator.Calculate(ToolIds, seed: 1).Tools.Select(tool => tool.RunRateWafersPerHour)
            .Should().NotEqual(snapshot.Tools.Select(tool => tool.RunRateWafersPerHour));

        foreach (var tool in snapshot.Tools)
        {
            tool.RunRateWafersPerHour.Should().BeInRange(10.80m, 11.76m);
            tool.UptimePercent.Should().BeInRange(92m, 98m);
            tool.FirstPassYieldPercent.Should().BeInRange(94m, 99m);
            tool.FailureCount.Should().BeInRange(3, 6);
            tool.MttrHours.Should().BeInRange(1.50m, 6.00m);
            tool.OeePercent.Should().Be(Oee(tool.UptimePercent, tool.RunRateWafersPerHour, tool.FirstPassYieldPercent));
            tool.OperatingHours.Should().Be(FleetKpiCalculator.Round2(2160m * tool.UptimePercent / 100m));
            tool.MtbfHours.Should().Be(FleetKpiCalculator.Round2(tool.OperatingHours / tool.FailureCount));
            tool.RepairHours.Should().Be(FleetKpiCalculator.Round2(tool.FailureCount * tool.MttrHours));
        }

        var fleet = snapshot.Fleet;
        var runRate = snapshot.Tools.Sum(tool => tool.RunRateWafersPerHour);
        var operating = snapshot.Tools.Sum(tool => tool.OperatingHours);
        fleet.ToolCount.Should().Be(8);
        fleet.RunRateWafersPerHour.Should().Be(runRate);
        fleet.OperatingHours.Should().Be(operating);
        fleet.FailureCount.Should().Be(snapshot.Tools.Sum(tool => tool.FailureCount));
        fleet.RepairHours.Should().Be(snapshot.Tools.Sum(tool => tool.RepairHours));
        fleet.UptimePercent.Should().Be(FleetKpiCalculator.Round2(operating / (2160m * 8m) * 100m));
        fleet.FirstPassYieldPercent.Should().Be(FleetKpiCalculator.Round2(snapshot.Tools.Sum(tool => tool.FirstPassYieldPercent * tool.RunRateWafersPerHour) / runRate));
        fleet.OeePercent.Should().Be(Oee(fleet.UptimePercent, runRate / 8m, fleet.FirstPassYieldPercent));
        fleet.MtbfHours.Should().Be(FleetKpiCalculator.Round2(operating / fleet.FailureCount));
        fleet.MttrHours.Should().Be(FleetKpiCalculator.Round2(fleet.RepairHours / fleet.FailureCount));
    }

    [Fact]
    public void AnEmptyToolListIsRejected()
    {
        var act = () => FleetKpiCalculator.Calculate([" ", ""]);
        act.Should().Throw<ArgumentException>().WithMessage("*tool id*");
    }

    [Fact]
    public void CommittedSnapshotMatchesTheGenerator()
    {
        var snapshot = FleetKpiCalculator.Calculate(ToolIds);
        var committed = Path.Combine(RepoPaths.KpiDirectory(), FleetKpiYaml.FileName);
        if (Environment.GetEnvironmentVariable("MPS_EXPORT_KPI") == "1")
        {
            FleetKpiYaml.Write(snapshot, RepoPaths.KpiDirectory());
        }

        var generated = FleetKpiYaml.Render(snapshot);
        generated.Should().Be(File.ReadAllText(committed));
        generated.Should().NotContain("\r");
        generated.Should().EndWith("\n");
        generated.Should().Contain("seed: 20261014");
        File.ReadAllText(Path.Combine(RepoPaths.KpiDirectory(), "README.md")).Should().Contain("20261014").And.Contain("OEE");
    }

    private static decimal Oee(decimal uptimePercent, decimal runRate, decimal firstPassYieldPercent)
    {
        return FleetKpiCalculator.Round2((uptimePercent / 100m) * (runRate / 12m) * (firstPassYieldPercent / 100m) * 100m);
    }
}
