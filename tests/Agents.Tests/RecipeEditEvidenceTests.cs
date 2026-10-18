// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Explain;
using Mps.Agents.Grounding;
using Mps.Agents.Impact;
using Mps.Agents.Ranking;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Agents.Tests;

/// <summary>Live SC-8 (Oct 18): how the recipe edit work order and the E-03 signal were described to the agents and the console.</summary>
public sealed class RecipeEditEvidenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 6, 9, 17, TimeSpan.Zero);

    private static readonly EvidenceItem RecipeEdit = new(
        EvidenceId.New(),
        EvidenceSource.Cmms,
        "An operator edited the RF power recipe setpoint on E-03 in the MES recipe. There is no engineering change order for that edit.",
        "Work order WO-RCP-E03-RF (process) on E-03 E-03-A: An operator edited the RF power recipe setpoint on E-03 in the MES recipe. There is no engineering change order for that edit.",
        Now.AddDays(-3),
        "E-03",
        "WO-RCP-E03-RF");

    private static EvidenceItem Signal(string tool, string chamber) => new(
        EvidenceId.New(),
        EvidenceSource.Spc,
        "Western Electric rule 1 on Bias RF power",
        "rule broke",
        Now,
        tool,
        "spc:" + tool + ":rfBiasPower:2026-10-18T06:09:17Z:western-electric-1",
        EvidenceReading.Place(tool, chamber, 452.8));

    [Theory]
    [InlineData("E-03", "E-03-A", "E-03-A-RF-BIAS-FWD")]
    [InlineData("E-01", "E-01-B", "E-01-B-RF-BIAS-FWD")]
    [InlineData("E-01", "E-01-A", "E-01-A-RF-BIAS-FWD")]
    public void SignalEvidenceNamesTheSensorOnItsOwnToolAndChamber(string tool, string chamber, string expected)
    {
        var item = Signal(tool, chamber);
        WhyItMattersCatalog.Shared.SignalIdFor(item).Should().Be(expected);
        WhyItMattersCatalog.Shared.ExplainItem(item).SignalId.Should().Be(expected);
    }

    [Fact]
    public void ARecipeEditWorkOrderIsDescribedAsARecipeChangeNotMaintenance()
    {
        var why = WhyItMattersCatalog.Shared.ExplainItem(RecipeEdit).WhyItMatters;
        why.Should().Contain("recipe setpoint").And.NotContain("Maintenance");
    }

    [Fact]
    public void ARecipeSetpointEditWithNoEngineeringChangeOrderImpliesProcessDrift()
    {
        var note = new EvidenceNote { Source = "cmms", Title = RecipeEdit.Title, Detail = RecipeEdit.Excerpt };
        CategoryEvidenceRules.Shared.Rules.Where(rule => rule.Matches(note)).Select(rule => rule.Implies)
            .Should().Equal(RootCauseCategory.ProcessDrift);
    }

    [Fact]
    public void TheEvidenceMatrixKeepsTheRecipeEditWithProcessDriftEvenForAStep()
    {
        var signal = Signal("E-03", "E-03-A");
        var matrix = EvidenceMatrixBuilder.FromItems([signal, RecipeEdit], SignalShape.Step, null, WhyItMattersCatalog.Shared);

        matrix.Single(entry => entry.Category == RootCauseCategory.ProcessDrift).Should().Match<EvidenceMatrixEntry>(entry =>
            entry.EvidenceId == RecipeEdit.Id && entry.Stance != EvidenceStance.RulesOut);
        matrix.Single(entry => entry.Category == RootCauseCategory.EquipmentCalibrationOrAlignment).EvidenceId.Should().NotBe(RecipeEdit.Id);
    }
}
