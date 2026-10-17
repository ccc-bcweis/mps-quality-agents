// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain;
using Mps.Domain.Evidence;
using Mps.Domain.Identity;
using Mps.Domain.Investigations;

namespace Mps.Domain.Tests;

/// <summary>The evidence-loop state machine: which rounds take evidence, what closing one does, and the cap.</summary>
public sealed class InvestigationRoundsTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 17, 22, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(InvestigationStage.RecommendationReady, true)]
    [InlineData(InvestigationStage.AwaitingApproval, true)]
    [InlineData(InvestigationStage.Escalated, true)]
    [InlineData(InvestigationStage.Started, false)]
    [InlineData(InvestigationStage.EvidenceGathered, false)]
    [InlineData(InvestigationStage.Approved, false)]
    [InlineData(InvestigationStage.Rejected, false)]
    [InlineData(InvestigationStage.Superseded, false)]
    [InlineData(InvestigationStage.Failed, false)]
    [InlineData(InvestigationStage.ClosedByDemoReset, false)]
    public void OnlyARoundWaitingForAPersonTakesEvidence(InvestigationStage stage, bool open)
    {
        var round = Round(stage);

        InvestigationRounds.IsOpenForDecision(round).Should().Be(open);
        InvestigationRounds.CanAddEvidence(round, 5).Should().Be(open);
    }

    [Fact]
    public void ARoundWithADecisionOnFileIsClosed()
    {
        var round = Round(InvestigationStage.Escalated).With(InvestigationStage.Escalated, "gap", new DecisionRecord("reject", null, "qe", At));

        InvestigationRounds.IsOpenForDecision(round).Should().BeFalse();
    }

    [Fact]
    public void SupersedeKeepsTheRoundTheDecisionAndTheEvidence()
    {
        var round = Round(InvestigationStage.RecommendationReady).WithRound(2);
        var decision = new DecisionRecord("requestInfo", "Operator error.", "bryan", At);

        var added = InvestigationRounds.UserEvidence("Operator error.", "bryan", At, "E-01", 2);

        var closed = InvestigationRounds.Supersede(round, decision, 5, added);

        closed.Stage.Should().Be(InvestigationStage.Superseded);
        closed.Round.Should().Be(2);
        closed.Decision.Should().Be(decision);
        closed.Evidence.Should().HaveCount(round.Evidence.Count + 1);
        InvestigationRounds.CarriedUserEvidence(closed).Should().ContainSingle().Which.Should().BeSameAs(added);
        closed.Hypotheses.Should().BeEquivalentTo(round.Hypotheses, "the round's recommendation stays as history");
    }

    [Fact]
    public void SupersedeRefusesAClosedRoundAndTheLastRound()
    {
        var decision = new DecisionRecord("requestInfo", "note", "bryan", At);

        var closed = () => InvestigationRounds.Supersede(Round(InvestigationStage.Approved), decision, 5);
        var last = () => InvestigationRounds.Supersede(Round(InvestigationStage.Escalated).WithRound(5), decision, 5);

        closed.Should().Throw<DomainRuleException>();
        last.Should().Throw<DomainRuleException>().WithMessage("*reached 5 rounds*");
        InvestigationRounds.CanAddEvidence(Round(InvestigationStage.Escalated).WithRound(5), 5).Should().BeFalse();
    }

    [Fact]
    public void UserEvidenceIsMarkedWithWhoAndWhen()
    {
        var item = InvestigationRounds.UserEvidence("  The bearing was worn.  ", "bryan", At, "E-01", 1);

        item.Source.Should().Be(EvidenceSource.UserProvided);
        item.SuppliedBy.Should().Be("bryan");
        item.ObservedAt.Should().Be(At);
        item.Excerpt.Should().Be("The bearing was worn.");
        item.ExternalRef.Should().Be("USER-R1");
        item.Title.Should().Be("Added by bryan for round 2");
    }

    [Theory]
    [InlineData("", "bryan")]
    [InlineData("text", " ")]
    public void UserEvidenceNeedsTextAndAnAuthor(string text, string who)
    {
        var act = () => InvestigationRounds.UserEvidence(text, who, At, "E-01", 1);

        act.Should().Throw<DomainRuleException>();
    }

    [Fact]
    public void AUserProvidedRecordWithoutAnAuthorIsRejected()
    {
        var act = () => new EvidenceItem(EvidenceId.New(), EvidenceSource.UserProvided, "Note", "text", At, null, null);

        act.Should().Throw<DomainRuleException>();
    }

    private static InvestigationSnapshot Round(InvestigationStage stage)
    {
        var evidence = new EvidenceItem(EvidenceId.New(), EvidenceSource.Spc, "Chart", "Point out.", At, "E-01", null);
        return new InvestigationSnapshot(
            InvestigationId.New(),
            IncidentId.New(),
            stage,
            [evidence],
            [],
            stage == InvestigationStage.Escalated ? "gap" : null,
            "corr");
    }
}
