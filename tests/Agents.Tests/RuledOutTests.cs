// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Evidence;
using Mps.Application.Faults;
using Mps.Domain.Enterprise;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Agents.Tests;

public sealed class RuledOutTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 15, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(RootCauseCategory.EngineeringChange)]
    [InlineData(RootCauseCategory.EquipmentCalibrationOrAlignment)]
    [InlineData(RootCauseCategory.SupplyChainMaterial)]
    [InlineData(RootCauseCategory.ProcessDrift)]
    public void EveryInjectedMisleadingRecordReadsAsRuledOut(RootCauseCategory category)
    {
        var records = ConfounderFactory.For(category, new ToolId("E-02"), "E-02-A", Guid.NewGuid(), Now);

        var reasons = records.Ecos.Select(RuledOut.Reason)
            .Concat(records.WorkOrders.Select(RuledOut.Reason))
            .Concat(records.Notices.Select(RuledOut.Reason))
            .ToArray();

        reasons.Should().NotBeEmpty().And.OnlyContain(reason => reason != null);
    }

    [Fact]
    public void ARealChangeOrAnOutOfSpecLotIsNotRuledOut()
    {
        RuledOut.Reason(new EcoRecord("ECO-4471", "Edge ring rev C", "E-02", null, Now, "Install edge ring rev C.")).Should().BeNull();
        RuledOut.Reason(new SupplierNotice("SN-1", "S", "GAS-CF4", "PL-2044", "Lot PL-2044 is out of spec.", Now)).Should().BeNull();
        RuledOut.Reason(new SupplierNotice("SN-2", "S", "GAS-CF4", "PL-2044", "Lot PL-2044 matches the purchase specification but is out of spec on moisture.", Now)).Should().BeNull();
        RuledOut.Reason(new WorkOrder("WO-1", new ToolId("E-02"), null, "pm", Now, "Replaced the liner.")).Should().BeNull();
        RuledOut.Reason(new WorkOrder("WO-2", new ToolId("E-02"), null, "pm", Now, "Inspection only: no parts changed.")).Should().BeNull();
    }

    [Fact]
    public void SeedTextsKeepTheThreeOutcomesAndTheRecordsOwnTitle()
    {
        RuledOut.Reason(new EcoRecord(
                "ECO-61234",
                "Operating note update on E-02",
                "E-02",
                null,
                Now,
                "Document-only update to the E-02 operating note. Setpoints were not changed."))
            .Should().Be("ECO-61234 is a document-only change. Setpoints were not changed.");
        RuledOut.Reason(new EcoRecord("ECO-1", "Note", "E-02", null, Now, "This is a document only paperwork edit."))
            .Should().Be("ECO-1 is a document-only change. Setpoints were not changed.");

        RuledOut.Reason(new SupplierNotice(
                "SN-SIH4-088",
                "Cinderwell",
                "GAS-SIH4",
                "SIH4-088",
                "Silane lot SIH4-088 matches the purchase specification.",
                Now))
            .Should().Be("Lot SIH4-088 (SN-SIH4-088) matches its specification.");

        RuledOut.Reason(new WorkOrder(
                "WO-71234",
                new ToolId("E-02"),
                "E-02-A",
                "preventive maintenance",
                Now,
                "Routine preventive maintenance the same week. Inspection only: no parts changed and no settings changed. No findings."))
            .Should().Be("Work order WO-71234 (preventive maintenance) was an inspection with no findings. Nothing was changed.");

        var title = "Silane lot SIH4-088 matches the purchase specification.";
        RuledOut.Title(title, "Lot SIH4-088 (SN-SIH4-088) matches its specification.").Should().Be(title);
        RuledOut.Title(title, null).Should().Be(title);
    }

    [Fact]
    public void ABadRuledOutFileFailsAtLoad()
    {
        RuledOutRules.Shared.Rules.Should().Contain(rule => rule.Id == "documentOnlyEco");
        RuledOutRules.Shared.Rules.Should().Contain(rule => rule.Id == "noticeMatchesPurchaseSpecification");
        RuledOutRules.Shared.Rules.Should().Contain(rule => rule.Id == "inspectionWithNoFindings");

        var bad = Path.Combine(Path.GetTempPath(), "ruled-out-" + Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(bad, "rules:\n  - id: x\n    sources: [eco]\n    anyOf: [document-only]\n    rulesOut: luck\n    reason: \"{ecoId} is out.\"\n");
        try
        {
            FluentActions.Invoking(() => RuledOutRules.Load(bad)).Should().Throw<InvalidOperationException>().WithMessage("*luck*");
        }
        finally
        {
            File.Delete(bad);
        }

        var missing = Path.Combine(Path.GetTempPath(), "ruled-out-missing-" + Guid.NewGuid().ToString("N") + ".yaml");
        FluentActions.Invoking(() => RuledOutRules.Load(missing)).Should().Throw<InvalidOperationException>().WithMessage("*ruled-out.yaml*");
    }
}
