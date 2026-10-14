// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Simulator.Roi;
using Mps.Simulator.Seed;

namespace Mps.Simulator.Tests;

public sealed class RoiDatasetTests
{
    [Fact]
    public void DemoYearSplitsCopqAndProjectsThePlanLevers()
    {
        var dataset = RoiCalculator.Calculate(new RoiAssumptions());
        var baseline = dataset.Baseline;

        baseline.Assumptions.Seed.Should().Be(EnterpriseSeedGenerator.DemoSeed);
        baseline.CopqUsd.Should().Be(96_000_000m);
        baseline.LaborUsd.Should().Be(2_926_000m);
        baseline.ScrapUsd.Should().Be(46_537_000m);
        baseline.WarrantyUsd.Should().Be(20_476_280m);
        baseline.DowntimeUsd.Should().Be(26_060_720m);
        (baseline.LaborUsd + baseline.ScrapUsd + baseline.WarrantyUsd + baseline.DowntimeUsd).Should().Be(baseline.CopqUsd);
        baseline.RepeatIncidents.Should().Be(252m);
        baseline.RunCostUsd.Should().Be(185_600m);
        baseline.DowntimeHours.Should().Be(baseline.DowntimeUsd / 12_000m);

        dataset.BaselineCase.GrossSavingsUsd.Should().Be(0m);
        dataset.BaselineCase.RunCostUsd.Should().Be(0m);
        dataset.BaselineCase.NetSavingsUsd.Should().Be(0m);
        dataset.BaselineCase.PaybackMonths.Should().BeNull();
        dataset.BaselineCase.MttrDays.Should().Be(9.5m);
        dataset.BaselineCase.CapaCycleDays.Should().Be(62m);

        dataset.Conservative.LaborSavingsUsd.Should().Be(877_800m);
        dataset.Conservative.RepeatScrapSavingsUsd.Should().Be(1_256_499m);
        dataset.Conservative.DowntimeSavingsUsd.Should().Be(4_560_626m);
        dataset.Conservative.GrossSavingsUsd.Should().Be(6_694_925m);
        dataset.Conservative.RunCostUsd.Should().Be(185_600m);
        dataset.Conservative.NetSavingsUsd.Should().Be(6_509_325m);
        dataset.Conservative.MttrDays.Should().Be(7.5m);
        dataset.Conservative.CapaCycleDays.Should().Be(49.6m);
        dataset.Conservative.InvestigationHours.Should().Be(15.4m);
        dataset.Conservative.RepeatRate.Should().Be(0.153m);
        dataset.Conservative.PaybackMonths.Should().Be(baseline.RunCostUsd * 12m / dataset.Conservative.GrossSavingsUsd);

        dataset.Expected.LaborSavingsUsd.Should().Be(1_316_700m);
        dataset.Expected.RepeatScrapSavingsUsd.Should().Be(2_094_165m);
        dataset.Expected.DowntimeSavingsUsd.Should().Be(6_384_876m);
        dataset.Expected.GrossSavingsUsd.Should().Be(9_795_741m);
        dataset.Expected.NetSavingsUsd.Should().Be(9_610_141m);
        dataset.Expected.MttrDays.Should().Be(6.7m);
        dataset.Expected.CapaCycleDays.Should().Be(40.3m);
        dataset.Expected.InvestigationHours.Should().Be(12.1m);
        dataset.Expected.RepeatRate.Should().Be(0.135m);
        dataset.Expected.HypothesisDays.Should().Be(1.2m);
        dataset.Expected.DowntimeHoursAvoided.Should().Be(baseline.DowntimeHours * 0.70m * 0.35m);
        dataset.Expected.PaybackMonths.Should().Be(baseline.RunCostUsd * 12m / dataset.Expected.GrossSavingsUsd);

        dataset.Sensitivity.Should().HaveCount(12);
        dataset.Sensitivity.Should().ContainSingle(row => row.Lever == "investigation" && row.Value == 0.45m)
            .Which.GrossSavingsUsd.Should().Be(dataset.Expected.GrossSavingsUsd);
        dataset.Sensitivity.Should().ContainSingle(row => row.Lever == "hypothesis" && row.Value == 0.70m)
            .Which.GrossSavingsUsd.Should().Be(dataset.Expected.GrossSavingsUsd);
        dataset.Sensitivity.Should().ContainSingle(row => row.Lever == "repeat" && row.Value == 0.25m)
            .Which.GrossSavingsUsd.Should().Be(dataset.Expected.GrossSavingsUsd);
        dataset.Sensitivity.Should().ContainSingle(row => row.Lever == "capture" && row.Value == 0.35m)
            .Which.GrossSavingsUsd.Should().Be(dataset.Expected.GrossSavingsUsd);
    }

    [Fact]
    public void SeedStampDoesNotScaleDollarsAndIncidentsScaleLabor()
    {
        var demo = RoiCalculator.Calculate(new RoiAssumptions());
        var restamped = RoiCalculator.Calculate(new RoiAssumptions { Seed = 1 });
        restamped.Baseline.LaborUsd.Should().Be(demo.Baseline.LaborUsd);
        restamped.Expected.GrossSavingsUsd.Should().Be(demo.Expected.GrossSavingsUsd);
        restamped.Baseline.Assumptions.Seed.Should().Be(1);

        var moreIncidents = RoiCalculator.Calculate(new RoiAssumptions { IncidentsPerYear = 2800 });
        moreIncidents.Baseline.LaborUsd.Should().Be(5_852_000m);
        moreIncidents.Baseline.CopqUsd.Should().Be(demo.Baseline.CopqUsd);
    }

    [Fact]
    public void SharesThatExceedTheRemainderAreRejected()
    {
        var act = () => RoiCalculator.Calculate(new RoiAssumptions
        {
            ScrapShareOfRemainder = 0.90m,
            WarrantyShareOfRemainder = 0.20m,
        });

        act.Should().Throw<InvalidOperationException>().WithMessage("*exceed the remainder*");
    }

    [Fact]
    public void CommittedTablesMatchTheGenerator()
    {
        var dataset = RoiCalculator.Calculate(new RoiAssumptions());
        var committed = RepoPaths.RoiDirectory();
        if (Environment.GetEnvironmentVariable("MPS_EXPORT_ROI") == "1")
        {
            RoiCsv.Write(dataset, committed);
        }

        var temp = Directory.CreateTempSubdirectory("mps-roi");
        try
        {
            RoiCsv.Write(dataset, temp.FullName);
            foreach (var name in new[] { RoiCsv.BaselineFileName, RoiCsv.ProjectionFileName, RoiCsv.SensitivityFileName })
            {
                var generated = File.ReadAllText(Path.Combine(temp.FullName, name));
                generated.Should().Be(File.ReadAllText(Path.Combine(committed, name)));
                generated.Should().NotContain("\r");
                generated.Should().EndWith("\n");
                generated.Should().Contain("20261014");
            }
        }
        finally
        {
            temp.Delete(true);
        }

        var projection = File.ReadAllLines(Path.Combine(committed, RoiCsv.ProjectionFileName));
        projection.Single(row => row.StartsWith("20261014,baseline,", StringComparison.Ordinal))
            .Should().EndWith(",0,0,0,,0.00");
        var expected = projection.Single(row => row.StartsWith("20261014,expected,", StringComparison.Ordinal));
        expected.Should().Contain(",1316700,2094165,6384876,9795741,185600,9610141,");
        File.ReadAllLines(Path.Combine(committed, RoiCsv.SensitivityFileName)).Should().HaveCount(13);
        File.ReadAllText(Path.Combine(committed, RoiCsv.BaselineFileName)).Should().Contain(",2926000,46537000,20476280,26060720,");
    }
}
