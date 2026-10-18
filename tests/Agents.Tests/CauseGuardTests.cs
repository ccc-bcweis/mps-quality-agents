// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Evidence;
using Mps.Agents.Grounding;
using Mps.Agents.Impact;
using Mps.Domain.Enterprise;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Agents.Tests;

public sealed class CauseGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 21, 0, 0, TimeSpan.Zero);

    private static EvidencePack Pack(SignalShape shape, params SupplierNotice[] notices)
    {
        var ruledOut = new List<RuledOutRecord>();
        foreach (var notice in notices)
        {
            var reason = RuledOut.Reason(notice);
            if (reason is null)
            {
                continue;
            }

            ruledOut.Add(new RuledOutRecord(
                EvidenceIds.Create(EvidenceSource.Erp, notice.NoticeId),
                RootCauseCategory.SupplyChainMaterial,
                reason,
                "noticeMatchesPurchaseSpecification"));
        }

        return new([], [], [], [], [], [], notices, true, shape, null, null, ruledOut);
    }

    private static ResolutionClaim Claim(string cause, string? shape) =>
        new(RootCauseCategory.EngineeringChange, cause, 0.9, [], [], [], [], null, null, shape);

    [Fact]
    public void ContrastingProseWithTheRightShapeFieldPasses()
    {
        CauseGuard.Check(Claim("CF4 flow on E-02-A shows a step rather than a gradual drift, after lot CF4-221 arrived.", "step"), Pack(SignalShape.Step))
            .Should().BeNull("the check compares the structured shape field, not words in the prose");
    }

    [Fact]
    public void AReportedShapeThatIsNotTheMeasuredShapeEscalates()
    {
        CauseGuard.Check(Claim("CF4 flow on E-02-A changed after lot CF4-221 arrived.", "ramp"), Pack(SignalShape.Step))
            .Should().Contain("gradual drift").And.Contain("a step");
    }

    [Fact]
    public void AMissingOrUnknownShapeIdEscalates()
    {
        CauseGuard.Check(Claim("ECO-4471 changed the edge ring before onset.", null), Pack(SignalShape.Oscillation))
            .Should().Contain("did not report the signal shape");
        CauseGuard.Check(Claim("ECO-4471 changed the edge ring before onset.", "wobble"), Pack(SignalShape.Oscillation))
            .Should().Contain("not a known shape");
    }

    [Fact]
    public void AShapeTheReadingsDoNotShowYetEscalatesWithAWayForward()
    {
        CauseGuard.Check(Claim("The chamber pressure moved after ECO-4471.", "step"), Pack(SignalShape.Unknown))
            .Should().Contain("Request more evidence");
        CauseGuard.Check(Claim("ECO-4471 changed the edge ring before onset.", "unknown"), Pack(SignalShape.Unknown)).Should().BeNull();
    }

    private static readonly SupplierNotice InSpec =
        new("SN-SIH4-088", "Cinderwell", "GAS-SIH4", "SIH4-088", "Silane lot SIH4-088 matches the purchase specification.", Now);

    private static readonly EvidenceItem NoticeItem =
        new(EvidenceId.New(), EvidenceSource.Erp, "Silane lot SIH4-088 matches the purchase specification.", "Supplier notice SN-SIH4-088.", Now, null, "SN-SIH4-088");

    private static readonly EvidenceItem RecipeEdit =
        new(EvidenceId.New(), EvidenceSource.Cmms, "Recipe edit", "An operator edited the RF power recipe setpoint on E-03. There is no engineering change order for that edit.", Now, "E-03", "WO-RCP-E03-RF");

    private static EvidencePack PackWithItems(SignalShape shape)
    {
        var reason = RuledOut.Reason(InSpec)!;
        var ruledOut = new RuledOutRecord(
            EvidenceIds.Create(EvidenceSource.Erp, InSpec.NoticeId),
            RootCauseCategory.SupplyChainMaterial,
            reason,
            "noticeMatchesPurchaseSpecification");
        return new([NoticeItem, RecipeEdit], [], [], [], [], [], [InSpec], true, shape, null, null, [ruledOut]);
    }

    private static ResolutionClaim Claim(RootCauseCategory category, string cause, string shape, params EvidenceId[] cited) =>
        new(category, cause, 0.9, cited, [], [], [], null, null, shape);

    [Fact]
    public void ARuledOutRecordNamedAsTheCauseOfItsOwnClassEscalates()
    {
        CauseGuard.Check(Claim(RootCauseCategory.SupplyChainMaterial, "Silane lot SIH4-088 is the cause.", "oscillation"), Pack(SignalShape.Oscillation, InSpec))
            .Should().Contain("SN-SIH4-088");
    }

    [Fact]
    public void ARuledOutRecordCitedAsSupportEscalatesWhateverTheClass()
    {
        var claim = Claim(RootCauseCategory.ProcessDrift, "WO-RCP-E03-RF: an operator edited the recipe setpoint on E-03 with no engineering change order; a step followed.", "step", RecipeEdit.Id, NoticeItem.Id);
        CauseGuard.Check(claim, PackWithItems(SignalShape.Step)).Should().Contain("SN-SIH4-088");
    }

    [Fact]
    public void APackRuledOutIdInEvidenceIdsEscalatesEvenWhenTheItemIsNotNamed()
    {
        var noticeId = EvidenceIds.Create(EvidenceSource.Erp, InSpec.NoticeId);
        var claim = Claim(RootCauseCategory.EquipmentCalibrationOrAlignment, "The pump bearing is past its service interval.", "step", noticeId);

        CauseGuard.Check(claim, Pack(SignalShape.Step, InSpec)).Should().Contain("SN-SIH4-088");
    }

    [Fact]
    public void ADecoyOnlyInTheAnswerRuledOutIdsPasses()
    {
        var noticeId = EvidenceIds.Create(EvidenceSource.Erp, InSpec.NoticeId);
        var claim = new ResolutionClaim(
            RootCauseCategory.ProcessDrift,
            "Work order WO-RCP-E03-RF says an operator edited the recipe setpoint on E-03 with no engineering change order, and a step followed.",
            0.9,
            [RecipeEdit.Id],
            [],
            [],
            [],
            null,
            null,
            "step",
            [new RuledOutClaim(noticeId, "The silane lot matches its specification.")]);

        CauseGuard.Check(claim, PackWithItems(SignalShape.Step)).Should().BeNull();
    }

    [Fact]
    public void ANoticeThePackDoesNotListIsNotReDerivedFromItsText()
    {
        var pack = new EvidencePack([], [], [], [], [], [], [InSpec], true, SignalShape.Step);
        var claim = Claim(RootCauseCategory.SupplyChainMaterial, "Silane lot SIH4-088 is the cause.", "step");

        CauseGuard.Check(claim, pack).Should().BeNull();
    }

    /// <summary>Live SC-8 (Oct 18): the answer dismissed the in-spec silane notice by id in its cause and was escalated.</summary>
    [Fact]
    public void NamingARuledOutRecordToDismissARivalClassPasses()
    {
        var claim = Claim(
            RootCauseCategory.ProcessDrift,
            "Work order WO-RCP-E03-RF says an operator edited the RF power recipe setpoint on E-03 with no engineering change order, and a step followed. Supply chain is ruled out: SN-SIH4-088 says lot SIH4-088 matches the purchase specification.",
            "step",
            RecipeEdit.Id);
        CauseGuard.Check(claim, PackWithItems(SignalShape.Step)).Should().BeNull("the record is mentioned only to dismiss a rival class, and it is not cited as support");
    }

    [Fact]
    public void AMatchingShapeAndARealRecordPass()
    {
        CauseGuard.Check(Claim("The oscillation started after ECO-4471 changed the edge ring.", "oscillation"), Pack(SignalShape.Oscillation)).Should().BeNull();
        CauseGuard.Check(Claim("ECO-4471 changed the edge ring; process drift is ruled out.", "OSCILLATION"), Pack(SignalShape.Oscillation)).Should().BeNull();
    }

    [Fact]
    public void TheRetryNoteNamesTheMeasuredShapeIdAndTheWordsToAvoidFromTheCatalog()
    {
        var note = CauseGuard.ShapeFeedback("ramp", Pack(SignalShape.Step));
        note.Should().Contain("Set shape to \"step\"");
        note.Should().Contain("gradual").And.Contain("drift").And.Contain("not even to say the signal is not like that");
        note.Should().NotContain("stepped");
    }

    [Fact]
    public void TheShapeCatalogComesFromMetadataAndCoversEveryMeasuredShape()
    {
        var catalog = SignalShapeCatalog.Load();
        catalog.Ids.Should().BeEquivalentTo(["step", "ramp", "oscillation", "intermittent", "unknown"]);
        foreach (var shape in Enum.GetValues<SignalShape>())
        {
            catalog.Of(shape).Description.Should().NotBeNullOrWhiteSpace();
        }

        var bad = Path.Combine(Path.GetTempPath(), "shapes-" + Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(bad, "shapes:\n  - id: wobble\n    description: a wobble\n");
        try
        {
            FluentActions.Invoking(() => SignalShapeCatalog.Load(bad)).Should().Throw<InvalidOperationException>().WithMessage("*wobble*");
        }
        finally
        {
            File.Delete(bad);
        }
    }
}
