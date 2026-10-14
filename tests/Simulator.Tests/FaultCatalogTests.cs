// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Simulator.Faults;
using Mps.Simulator.Fleet;
using Mps.Simulator.GroundTruth;
using Mps.Simulator.Telemetry;

namespace Mps.Simulator.Tests;

public sealed class FaultCatalogTests
{
    private static readonly FleetCatalog Fleet = FleetCatalogLoader.Load(RepoPaths.FleetDirectory());

    private static readonly FaultCatalog Catalog = FaultCatalogLoader.Load(RepoPaths.FaultDirectory(), Fleet);

    private static readonly string[] HiddenTokens =
    [
        "engineeringChange",
        "equipmentCalibrationOrAlignment",
        "supplyChainMaterial",
        "processDrift",
        "expectedBehavior",
        "GroundTruth",
        "rootCause",
    ];

    [Fact]
    public void CatalogCoversFourCausesPlusContradictionAndMissingEvidence()
    {
        Catalog.Faults.Select(fault => fault.Category).Distinct().Should().BeEquivalentTo(FaultCategories.All);
        Catalog.Faults.Should().Contain(fault => fault.Evidence.Contradictory && fault.ExpectedBehavior == FaultCategories.Escalate);
        Catalog.Faults.Should().Contain(fault => fault.Evidence.MissingEvidence);
        Catalog.GetRequired("FLT-ECO-4471").Scenario.Should().Be("S1");
        Catalog.GetRequired("FLT-CONTRA-D01").Scenario.Should().Be("S2");
    }

    [Fact]
    public void EngineeringChangeWritesTheEcoAndThePmConfounder()
    {
        var (draft, truth) = Apply("FLT-ECO-4471");
        var eco = draft.Ecos.Should().ContainSingle(record => record.EcoId == "ECO-4471").Subject;
        eco.ToolId.Should().Be("E-02");
        eco.EffectiveAt.Should().BeOnOrBefore(truth.Signals[0].Onset);
        draft.WorkOrders.Should().ContainSingle(order => order.WorkOrderId == "WO-PM-8821");
        draft.Configs.Should().Contain(config => config.ConfigId == "CFG-E-02" && config.Revision == "C");
        draft.Runs.Where(run => run.ToolId.Value == "E-02").Should().OnlyContain(run => run.RecipeRevision == "C");
        truth.EvidenceRecordIds.Should().Contain("ECO-4471");
        truth.ConfounderRecordIds.Should().Contain("WO-PM-8821");
        truth.AssertCategory.Should().BeTrue();
        truth.Category.Should().Be(FaultCategories.EngineeringChange);
    }

    [Fact]
    public void CalibrationWritesAnOverdueRecordAndASupplierConfounder()
    {
        var (draft, truth) = Apply("FLT-CAL-S02");
        var calibration = draft.Calibrations.Should().ContainSingle().Subject;
        calibration.CalibrationId.Should().Be("CAL-S02-O2-MFC");
        calibration.IsOverdue.Should().BeTrue();
        calibration.CompletedAt.Should().BeNull();
        calibration.DueAt.Should().BeBefore(truth.Signals[0].Onset);
        draft.Notices.Should().ContainSingle(notice => notice.NoticeId == "SN-N2H2-190");
        draft.MaterialUses.Should().Contain(use => use.LotId == "N2H2-190" && use.ToolId == "S-02");
        truth.EvidenceRecordIds.Should().Contain("CAL-S02-O2-MFC");
        truth.ConfounderRecordIds.Should().Contain("SN-N2H2-190");
    }

    [Fact]
    public void MaterialLotIsVisibleOnEveryEtchToolAndTheEcoIsOnlyAConfounder()
    {
        var (draft, truth) = Apply("FLT-GAS-NF3317");
        draft.MaterialLots.Should().ContainSingle(lot => lot.LotId == "NF-3317" && lot.NoticeId == "SN-NF-3317");
        draft.Notices.Should().ContainSingle(notice => notice.LotId == "NF-3317");
        draft.Lots.Where(lot => lot.LotId == "NF-3317").Select(lot => lot.ToolId.Value).Should().BeEquivalentTo("E-01", "E-02");
        draft.Ecos.Should().ContainSingle(eco => eco.EcoId == "ECO-4502" && eco.ToolId == "E-01");
        truth.EvidenceRecordIds.Should().Contain("NF-3317").And.Contain("SN-NF-3317");
        truth.ConfounderRecordIds.Should().Contain("ECO-4502");
        truth.EvidenceRecordIds.Should().NotContain("ECO-4502");
        truth.Signals.Select(signal => signal.ToolId).Should().BeEquivalentTo("E-01", "E-02");
    }

    [Fact]
    public void ProcessDriftHasNoDiscreteChangeAtOnset()
    {
        var (draft, truth) = Apply("FLT-DRIFT-E01");
        draft.Ecos.Should().BeEmpty();
        draft.Calibrations.Should().BeEmpty();
        draft.Notices.Should().BeEmpty();
        draft.MaterialLots.Should().BeEmpty();
        var clean = draft.WorkOrders.Should().ContainSingle().Subject;
        clean.WorkOrderId.Should().Be("WO-CLN-E01-220");
        clean.PerformedAt.Should().BeBefore(truth.Signals[0].Onset);
        draft.Runs.Should().OnlyContain(run => run.RecipeRevision == "B");
        draft.Configs.Should().OnlyContain(config => config.Revision == "B");
        truth.ConfounderRecordIds.Should().BeEmpty();
        truth.Category.Should().Be(FaultCategories.Drift);
    }

    [Fact]
    public void ContradictionAndMissingEvidenceDoNotAssertASingleRevision()
    {
        var (draft, truth) = Apply("FLT-CONTRA-D01");
        draft.Configs.Should().ContainSingle(config => config.Revision == "C" && config.PartName == "Showerhead");
        draft.Runs.Should().OnlyContain(run => run.RecipeRevision == "B");
        draft.Calibrations.Select(record => record.CalibrationId).Should().NotContain("CAL-D01-MANOMETER");
        draft.RecordIds().Should().NotContain("CAL-D01-MANOMETER");
        truth.ExpectedBehavior.Should().Be(FaultCategories.Escalate);
        truth.AssertCategory.Should().BeFalse();
        truth.MissingRecordIds.Should().Contain("CAL-D01-MANOMETER");
        truth.Contradiction.Should().Contain("revision C").And.Contain("revision B");
    }

    [Fact]
    public void EnterpriseDraftDoesNotCarryTheHiddenLabel()
    {
        foreach (var fault in Catalog.Faults)
        {
            var draft = new EnterpriseDraft();
            _ = GroundTruthWriter.Apply(fault, draft);
            var visible = string.Join('\n', draft.VisibleText());
            foreach (var token in HiddenTokens)
            {
                visible.Should().NotContain(token);
            }
        }
    }

    [Fact]
    public void CatalogSignalChangesTelemetryWithoutStampingACause()
    {
        var fault = Catalog.GetRequired("FLT-ECO-4471");
        var tool = Fleet.GetRequired("E-02");
        var onset = fault.Onset;
        var plain = FleetTelemetry.Generate(tool, "E-02-A", "escTemperature", 5, onset, onset, onset.AddSeconds(20));
        var moved = FleetTelemetry.Generate(tool, "E-02-A", "escTemperature", 5, onset, onset, onset.AddSeconds(20), fault.ToSignals());

        moved.Select(point => point.Value).Should().NotEqual(plain.Select(point => point.Value));
    }

    [Fact]
    public void CommittedGroundTruthMatchesTheWriter()
    {
        var committed = RepoPaths.GroundTruthDirectory();
        if (Environment.GetEnvironmentVariable("MPS_EXPORT_GROUNDTRUTH") == "1")
        {
            GroundTruthWriter.WriteCatalog(RepoPaths.FaultDirectory(), Fleet, committed);
        }

        var temp = Directory.CreateTempSubdirectory("mps-groundtruth");
        try
        {
            GroundTruthWriter.WriteCatalog(RepoPaths.FaultDirectory(), Fleet, temp.FullName);
            var files = Directory.GetFiles(temp.FullName, "*.json").Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
            files.Should().BeEquivalentTo(
                "FLT-CAL-S02.json",
                "FLT-CONTRA-D01.json",
                "FLT-DRIFT-E01.json",
                "FLT-ECO-4471.json",
                "FLT-GAS-NF3317.json");
            foreach (var name in files)
            {
                var expected = File.ReadAllText(Path.Combine(committed, name!));
                var actual = File.ReadAllText(Path.Combine(temp.FullName, name!));
                actual.Should().Be(expected);
                var document = GroundTruthJson.Read(Path.Combine(temp.FullName, name!));
                document.FaultId.Should().Be(Path.GetFileNameWithoutExtension(name));
            }
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Fact]
    public void GroundTruthIsUnreachableFromAgentToolsAndTheApiImage()
    {
        var root = RepoPaths.Root();
        foreach (var relative in new[] { "src/Mps.Agents", "src/Mps.Connectors", "src/Mps.MockEnterprise", "src/Mps.Application", "src/Mps.Api" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, relative), "*.*", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                var text = File.ReadAllText(file);
                text.Should().NotContain("Mps.Simulator.GroundTruth", file);
                text.Should().NotContain("data/groundtruth", file);
                text.Should().NotContain("GroundTruthDocument", file);
            }
        }

        File.ReadAllText(Path.Combine(root, "deploy", "api.Dockerfile")).Should().NotContain("groundtruth");
        File.ReadAllText(Path.Combine(root, "docker-compose.yml")).ToLowerInvariant().Should().NotContain("groundtruth");
        typeof(ITelemetrySource).GetMethods().Select(method => method.Name).Should().NotContain(name => name.Contains("Ground", StringComparison.Ordinal));
    }

    [Fact]
    public void ADriftFaultWithAnEcoIsRejected()
    {
        var temp = Directory.CreateTempSubdirectory("mps-faults");
        try
        {
            foreach (var file in Directory.GetFiles(RepoPaths.FaultDirectory(), "*.yaml"))
            {
                File.Copy(file, Path.Combine(temp.FullName, Path.GetFileName(file)));
            }

            File.WriteAllText(
                Path.Combine(temp.FullName, "flt-bad.yaml"),
                """
                id: FLT-BAD
                title: Bad drift
                category: processDrift
                scenario: bad
                expectedBehavior: recommend
                assertCategory: true
                tools: [E-01]
                parameter: etchRate
                shape: ramp
                magnitude: { kind: sigma, value: -1 }
                onset: "2026-09-01T00:00:00Z"
                cause: This fixture must fail the catalog rules.
                evidence:
                  workOrder:
                    workOrderId: WO-BAD
                    toolId: E-01
                    kind: clean
                    performedAt: "2026-08-01T00:00:00Z"
                    summary: Clean.
                  eco:
                    ecoId: ECO-BAD
                    title: Not allowed on a drift fault
                    toolId: E-01
                    effectiveAt: "2026-08-15T00:00:00Z"
                    changeSummary: A discrete change does not belong on a drift fault.
                """);

            var act = () => FaultCatalogLoader.Load(temp.FullName, Fleet);
            act.Should().Throw<FaultCatalogException>().WithMessage("*process drift*");
        }
        finally
        {
            temp.Delete(true);
        }
    }

    private static (EnterpriseDraft Draft, GroundTruthDocument Truth) Apply(string id)
    {
        var draft = new EnterpriseDraft();
        var truth = GroundTruthWriter.Apply(Catalog.GetRequired(id), draft);
        return (draft, truth);
    }
}
