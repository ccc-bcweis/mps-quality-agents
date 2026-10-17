// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Explain;
using Mps.Agents.Grounding;
using Mps.Agents.Ranking;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Agents.Tests;

public sealed class CategoryGuardTests
{
    private static readonly Guid BearingId = Guid.Parse("00000000-0000-0000-0000-000000000900");
    private static readonly Guid SignalId = Guid.Parse("00000000-0000-0000-0000-000000000910");

    private static EvidenceNote Note(Guid id, string source, string reference, string detail) =>
        new() { Id = id.ToString(), Source = source, Title = "Record " + reference, Detail = detail, ExternalRef = reference };

    private static readonly EvidenceNote Bearing = Note(BearingId, "cmms", "WO-PMP-D02-BRG", "Foreline pump bearing hours are past the service interval on D-02. Vibration has been rising.");
    private static readonly EvidenceNote Signal = Note(SignalId, "telemetry", "TEL-D-02", "Shape from the data: a gradual drift in one direction.");

    private static ResolutionClaim Claim(RootCauseCategory category, Guid[] cited, string[]? counter = null, EvidenceMatrixEntry[]? matrix = null) =>
        new(category, "WO-PMP-D02-BRG says the bearing is past its service interval.", 0.8, cited.Select(id => new EvidenceId(id)).ToArray(), counter ?? [], [], [], null, matrix, "ramp");

    [Fact]
    public void ABearingWorkOrderFiledAsProcessDriftIsAMismatch()
    {
        var reason = CategoryGuard.Check(Claim(RootCauseCategory.ProcessDrift, [BearingId, SignalId]), [Bearing, Signal]);
        reason.Should().Contain("processDrift").And.Contain("WO-PMP-D02-BRG").And.Contain("equipmentCalibrationOrAlignment");
        CategoryGuard.Feedback(Claim(RootCauseCategory.ProcessDrift, [BearingId]), [Bearing]).Should().Contain("\"equipmentCalibrationOrAlignment\"");
    }

    [Fact]
    public void TheEquipmentClassForTheSameRecordPasses()
    {
        CategoryGuard.Check(Claim(RootCauseCategory.EquipmentCalibrationOrAlignment, [BearingId, SignalId]), [Bearing, Signal]).Should().BeNull();
    }

    [Fact]
    public void ARecordTheReplyRulesOutOrDoesNotCiteDoesNotCount()
    {
        CategoryGuard.Check(Claim(RootCauseCategory.ProcessDrift, [SignalId]), [Bearing, Signal]).Should().BeNull("the work order is not cited");
        CategoryGuard.Check(Claim(RootCauseCategory.ProcessDrift, [BearingId], counter: ["WO-PMP-D02-BRG is a confounder."]), [Bearing]).Should().BeNull();
        var rulesOut = new EvidenceMatrixEntry(RootCauseCategory.EquipmentCalibrationOrAlignment, new EvidenceId(BearingId), EvidenceStance.RulesOut, null);
        CategoryGuard.Check(Claim(RootCauseCategory.ProcessDrift, [BearingId], matrix: [rulesOut]), [Bearing]).Should().BeNull();
    }

    [Fact]
    public void CalibrationRulesReadTheRecordTextFromMetadata()
    {
        var overdue = Note(BearingId, "cmms", "CAL-E01-ESC-TC", "Calibration CAL-E01-ESC-TC for ESC thermocouple on E-01: due 2026-10-01, completed never, OVERDUE.");
        var inDate = Note(BearingId, "cmms", "CAL-E01-ESC-TC", "Calibration CAL-E01-ESC-TC for ESC thermocouple on E-01: due 2026-11-01, completed 2026-09-01, in date.");
        var deferred = Note(BearingId, "cmms", "WO-CAL-E01-ESC-DEFER", "ESC thermocouple calibration on chamber A deferred for parts.");
        CategoryGuard.Check(Claim(RootCauseCategory.SupplyChainMaterial, [BearingId]), [overdue]).Should().NotBeNull();
        CategoryGuard.Check(Claim(RootCauseCategory.SupplyChainMaterial, [BearingId]), [inDate]).Should().BeNull();
        CategoryGuard.Check(Claim(RootCauseCategory.ProcessDrift, [BearingId]), [deferred]).Should().NotBeNull();
    }

    [Fact]
    public void AnOperatorRecipeEditInAMaintenanceNoteStaysProcessDrift()
    {
        var edit = Note(BearingId, "cmms", "WO-RCP-E03-RF", "Operator changed the RF power setpoint in the recipe. No engineering change order.");
        CategoryGuard.Check(Claim(RootCauseCategory.ProcessDrift, [BearingId]), [edit]).Should().BeNull();
    }

    [Fact]
    public void TheMapComesFromMetadataAndRejectsAnUnknownClass()
    {
        var rules = CategoryEvidenceRules.Load();
        rules.Rules.Should().NotBeEmpty();
        rules.Rules.Should().OnlyContain(rule => rule.Implies == RootCauseCategory.EquipmentCalibrationOrAlignment);

        var bad = Path.Combine(Path.GetTempPath(), "category-" + Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(bad, "rules:\n  - id: x\n    sources: [cmms]\n    anyOf: [bearing]\n    implies: luck\n");
        try
        {
            FluentActions.Invoking(() => CategoryEvidenceRules.Load(bad)).Should().Throw<InvalidOperationException>().WithMessage("*luck*");
        }
        finally
        {
            File.Delete(bad);
        }
    }
}
