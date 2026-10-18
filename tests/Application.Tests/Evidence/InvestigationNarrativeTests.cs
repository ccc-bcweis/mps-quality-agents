// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Errors;
using Mps.Application.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Application.Tests.Evidence;

public sealed class InvestigationNarrativeTests
{
    private static readonly Guid EvidenceId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void CreateKeepsASentenceAtTheLengthLimitAndTrimsTheSignal()
    {
        var why = new string('a', InvestigationNarrative.MaxWhyItMattersLength);
        var ids = Ids();

        var narrative = InvestigationNarrative.Create(
            [new EvidenceNote(EvidenceId, "  " + why + "  ", "  spc.stand-in  ")],
            [new EvidenceMatrixEntry(RootCauseCategory.ProcessDrift, EvidenceId, EvidenceStance.Supports, why)],
            ids);

        narrative.Notes.Should().ContainSingle();
        narrative.Notes[0].WhyItMatters.Should().Be(why);
        narrative.Notes[0].SignalId.Should().Be("spc.stand-in");
        narrative.Matrix.Should().ContainSingle();
        narrative.Matrix[0].Stance.Should().Be(EvidenceStance.Supports);
    }

    [Fact]
    public void ABlankWhyItMattersIsStoredAsNull()
    {
        var narrative = InvestigationNarrative.Create(
            [new EvidenceNote(EvidenceId, "   ", null)],
            [],
            Ids());

        narrative.Notes[0].WhyItMatters.Should().BeNull();
        narrative.Notes[0].SignalId.Should().BeNull();
    }

    [Fact]
    public void ASentencePastTheLimitIsRejected()
    {
        var why = new string('b', InvestigationNarrative.MaxWhyItMattersLength + 1);

        var act = () => InvestigationNarrative.Create(
            [new EvidenceNote(EvidenceId, why, "spc.stand-in")],
            [],
            Ids());

        act.Should().Throw<IntakeValidationException>().WithMessage("*240*");
    }

    [Fact]
    public void AControlCharacterAndAnUnknownEvidenceIdAreRejected()
    {
        var control = () => InvestigationNarrative.Create(
            [new EvidenceNote(EvidenceId, "Drifted.\0", "spc.stand-in")],
            [],
            Ids());
        var unknown = () => InvestigationNarrative.Create(
            [new EvidenceNote(Guid.NewGuid(), "Outside the window.", "spc.stand-in")],
            [],
            Ids());
        var duplicate = () => InvestigationNarrative.Create(
            [
                new EvidenceNote(EvidenceId, "Outside the window.", "spc.stand-in"),
                new EvidenceNote(EvidenceId, "Same point.", "spc.stand-in"),
            ],
            [],
            Ids());

        control.Should().Throw<IntakeValidationException>().WithMessage("*control*");
        unknown.Should().Throw<IntakeValidationException>().WithMessage("*evidence item*");
        duplicate.Should().Throw<IntakeValidationException>().WithMessage("*one whyItMatters*");
    }

    [Fact]
    public void TheMatrixRejectsARepeatedCategoryForTheSameEvidence()
    {
        var act = () => InvestigationNarrative.Create(
            [],
            [
                new EvidenceMatrixEntry(RootCauseCategory.ProcessDrift, EvidenceId, EvidenceStance.Supports, "Outside the window."),
                new EvidenceMatrixEntry(RootCauseCategory.ProcessDrift, EvidenceId, EvidenceStance.RulesOut, "Outside the window."),
            ],
            Ids());

        act.Should().Throw<IntakeValidationException>().WithMessage("*already cites*");
    }

    [Fact]
    public void CreateKeepsRuledOutIdsThePackListAndTheGuardHit()
    {
        var ruledOutId = Guid.NewGuid();
        var unknownCited = Guid.NewGuid();
        var record = new RuledOutEntry(
            new EvidenceId(ruledOutId),
            "It matches the specification.",
            RuledOutSource.Record,
            RootCauseCategory.SupplyChainMaterial);
        var rejected = new RejectedAnswer(
            "A guess.",
            RootCauseCategory.ProcessDrift,
            [EvidenceId, unknownCited],
            [new RejectedRuledOut(ruledOutId, "The answer dismissed it.")]);
        var hit = new GuardListHit(unknownCited, GuardListHit.EvidenceIds);

        var narrative = InvestigationNarrative.Create([], [], Ids(), rejected, [record], hit);

        narrative.Rejected.Should().NotBeNull();
        narrative.Rejected!.EvidenceIds.Should().Equal(EvidenceId);
        narrative.Rejected.RuledOutIds.Should().ContainSingle(item =>
            item.EvidenceId == ruledOutId && item.Reason == "The answer dismissed it.");
        narrative.RecordRuledOut.Should().ContainSingle(item =>
            item.Id.Value == ruledOutId && item.Source == RuledOutSource.Record && item.Class == RootCauseCategory.SupplyChainMaterial);
        narrative.GuardHit.Should().Be(hit);
        var badList = () => new GuardListHit(Guid.NewGuid(), "causes");
        badList.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static HashSet<Guid> Ids() => [EvidenceId];
}
