// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Simulator.Faults;
using Mps.Simulator.Fleet;
using Mps.Simulator.Seed;

namespace Mps.Simulator.Tests;

public sealed class EnterpriseSeedTests
{
    private static readonly string[] Forbidden =
    [
        "rootCauseCategory",
        "expectedBehavior",
        "assertCategory",
        "GroundTruth",
        "engineeringChange",
        "equipmentCalibrationOrAlignment",
        "supplyChainMaterial",
        "processDrift",
    ];

    private static readonly FleetCatalog Fleet = FleetCatalogLoader.Load(RepoPaths.FleetDirectory());

    private static readonly FaultCatalog Catalog = FaultCatalogLoader.Load(RepoPaths.FaultDirectory(), Fleet);

    [Fact]
    public void DemoSeedCoversTheFleetTheFaultsAndNinetyDays()
    {
        var seed = EnterpriseSeedGenerator.Generate(Fleet, Catalog, EnterpriseSeedGenerator.DemoSeed);
        seed.WindowEnd.Should().Be(new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero));
        seed.WindowStart.Should().Be(seed.WindowEnd.AddDays(-EnterpriseSeedGenerator.HistoryDays));
        seed.Configs.Select(config => config.ToolId.Value).Should().BeEquivalentTo("C-01", "C-02", "D-01", "D-02", "E-01", "E-02", "S-01", "S-02");
        seed.Runs.Count(run => run.RunId.StartsWith("RUN-BG-", StringComparison.Ordinal)).Should().Be(8 * EnterpriseSeedGenerator.Weeks * EnterpriseSeedGenerator.WafersPerWeek);
        var faultRuns = Catalog.Faults.Sum(fault => (fault.Evidence.Runs?.Count ?? 0) * fault.ToolIds.Count);
        seed.Runs.Count(run => run.RunId.StartsWith("RUN-FLT-", StringComparison.Ordinal)).Should().Be(faultRuns);
        seed.Runs.Should().OnlyContain(run => run.StartedAt >= seed.WindowStart && run.StartedAt <= seed.WindowEnd);

        seed.Ecos.Should().Contain(eco => eco.EcoId == "ECO-4471" && eco.ToolId == "E-02");
        seed.Configs.Should().Contain(config => config.ToolId.Value == "D-01" && config.Revision == "C" && config.PartName == "Showerhead");
        seed.Runs.Where(run => run.ToolId.Value == "D-01").Should().OnlyContain(run => run.RecipeRevision == "B");
        seed.Runs.Where(run => run.ToolId.Value == "E-02" && run.RunId.StartsWith("RUN-BG-", StringComparison.Ordinal)).Select(run => run.RecipeRevision).Should().Contain("B").And.Contain("C");
        seed.Runs.Where(run => run.ToolId.Value == "E-01").Should().OnlyContain(run => run.RecipeRevision == "B");

        var overdue = seed.Calibrations.Should().ContainSingle(record => record.CalibrationId == "CAL-S02-O2-MFC").Subject;
        overdue.IsOverdue.Should().BeTrue();
        overdue.CompletedAt.Should().BeNull();
        seed.Calibrations.Select(record => record.CalibrationId).Should().NotContain("CAL-D01-MANOMETER");

        seed.MaterialUses.Where(use => use.LotId == "NF-3317").Select(use => use.ToolId).Should().BeEquivalentTo("E-01", "E-02");
        seed.Notices.Should().Contain(notice => notice.NoticeId == "SN-NF-3317");
        seed.Notices.Should().Contain(notice => notice.NoticeId == "SN-SIH4-088" && notice.SupplierName == "Cinderwell Specialty Gases");
        seed.MaterialLots.Should().Contain(lot => lot.LotId == "QR-TANK-003" && lot.SupplierName == "Bramble Quartz Components");
        seed.Ncrs.Should().Contain(ncr => ncr.NcrId == "NCR-C-01-104");
        seed.WorkOrders.Should().Contain(order => order.WorkOrderId == "WO-PM-8821");
    }

    [Fact]
    public void ADifferentSeedChangesBackgroundRunIdsAndKeepsFaultEvidence()
    {
        var demo = EnterpriseSeedGenerator.Generate(Fleet, Catalog, EnterpriseSeedGenerator.DemoSeed);
        var other = EnterpriseSeedGenerator.Generate(Fleet, Catalog, EnterpriseSeedGenerator.DemoSeed + 1);
        demo.Runs.Where(run => run.RunId.StartsWith("RUN-BG-", StringComparison.Ordinal)).Select(run => run.RunId)
            .Should().NotEqual(other.Runs.Where(run => run.RunId.StartsWith("RUN-BG-", StringComparison.Ordinal)).Select(run => run.RunId));
        demo.Ecos.Select(eco => eco.EcoId).Should().Equal(other.Ecos.Select(eco => eco.EcoId));
    }

    [Fact]
    public void CommittedSeedMatchesTheGeneratorAndHidesTheAnswer()
    {
        var generated = EnterpriseSeedGenerator.Generate(Fleet, Catalog, EnterpriseSeedGenerator.DemoSeed);
        var committed = RepoPaths.SeedFile();
        if (Environment.GetEnvironmentVariable("MPS_EXPORT_SEED") == "1")
        {
            SeedJson.Write(generated, committed);
        }

        var temp = Directory.CreateTempSubdirectory("mps-seed");
        try
        {
            var actualPath = Path.Combine(temp.FullName, "enterprise.json");
            SeedJson.Write(generated, actualPath);
            var again = Path.Combine(temp.FullName, "again.json");
            SeedJson.Write(EnterpriseSeedGenerator.Generate(Fleet, Catalog, EnterpriseSeedGenerator.DemoSeed), again);
            var actual = File.ReadAllText(actualPath);
            actual.Should().Be(File.ReadAllText(again));
            actual.Should().Be(File.ReadAllText(committed));
            foreach (var token in Forbidden)
            {
                actual.Should().NotContain(token);
            }

            var roundTrip = SeedJson.Read(actualPath);
            roundTrip.Seed.Should().Be(EnterpriseSeedGenerator.DemoSeed);
            roundTrip.Runs.Should().HaveCount(generated.Runs.Length);
            roundTrip.Configs.Should().Contain(config => config.ToolId.Value == "E-02" && config.Revision == "C");
        }
        finally
        {
            temp.Delete(true);
        }
    }
}
