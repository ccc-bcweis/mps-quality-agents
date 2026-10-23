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
    [InlineData("", "bryan", "Type the evidence to add.")]
    [InlineData("   ", "bryan", "Type the evidence to add.")]
    [InlineData(null, "bryan", "Type the evidence to add.")]
    [InlineData("text", " ", "User-provided evidence names who supplied it.")]
    [InlineData("text", null, "User-provided evidence names who supplied it.")]
    public void UserEvidenceNeedsTextAndAnAuthor(string? text, string? who, string message)
    {
        var act = () => InvestigationRounds.UserEvidence(text!, who!, At, "E-01", 1);

        act.Should().Throw<DomainRuleException>().WithMessage(message);
    }

    [Fact]
    public void UserEvidenceAcceptsTheLengthCapAndNamesTheNextRound()
    {
        var atCap = new string('a', InvestigationRounds.MaxEvidenceLength);
        var item = InvestigationRounds.UserEvidence("  " + atCap + "  ", "  bryan  ", At, "  E-02  ", 2);

        item.Excerpt.Should().Be(atCap);
        item.SuppliedBy.Should().Be("bryan");
        item.Title.Should().Be("Added by bryan for round 3");
        item.ExternalRef.Should().Be("USER-R2");
        item.ToolId.Should().Be("E-02");
        item.Source.Should().Be(EvidenceSource.UserProvided);
        item.Id.IsEmpty.Should().BeFalse();

        InvestigationRounds.UserEvidence(atCap, "bryan", At, null, 0).Title.Should().Be("Added by bryan for round 1");
        InvestigationRounds.UserEvidence(atCap, "bryan", At, "   ", 0).ToolId.Should().BeNull();

        var tooLong = () => InvestigationRounds.UserEvidence(atCap + "b", "bryan", At, "E-02", 2);
        tooLong.Should().Throw<DomainRuleException>().WithMessage("The evidence text is too long.");
    }

    [Fact]
    public void EvidenceCanBeAddedUntilTheRoundCap()
    {
        InvestigationRounds.CanAddEvidence(Round(InvestigationStage.RecommendationReady).WithRound(4), 5).Should().BeTrue();
        InvestigationRounds.CanAddEvidence(Round(InvestigationStage.AwaitingApproval).WithRound(5), 5).Should().BeFalse();
        InvestigationRounds.CanAddEvidence(Round(InvestigationStage.Escalated).WithRound(1), 1).Should().BeFalse();
        InvestigationRounds.CanAddEvidence(Round(InvestigationStage.Started).WithRound(1), 5).Should().BeFalse();

        var decided = Round(InvestigationStage.RecommendationReady).With(InvestigationStage.RecommendationReady, null, new DecisionRecord("approve", null, "qe", At));
        InvestigationRounds.CanAddEvidence(decided, 5).Should().BeFalse();
        InvestigationRounds.IsOpenForDecision(decided).Should().BeFalse();
        InvestigationRounds.IsOpenForDecision(Round(InvestigationStage.AwaitingApproval)).Should().BeTrue();

        var missing = () => InvestigationRounds.IsOpenForDecision(null!);
        missing.Should().Throw<ArgumentNullException>().WithParameterName("round");
    }

    [Fact]
    public void SupersedeWithoutNewEvidenceClosesTheRoundAndKeepsWhatWasAlreadyThere()
    {
        var round = Round(InvestigationStage.AwaitingApproval).WithRound(2);
        var decision = new DecisionRecord("requestInfo", "note", "bryan", At);

        var closed = InvestigationRounds.Supersede(round, decision, 5);

        closed.Stage.Should().Be(InvestigationStage.Superseded);
        closed.Round.Should().Be(2);
        closed.Decision.Should().Be(decision);
        closed.Evidence.Should().Equal(round.Evidence);
        closed.Should().NotBeSameAs(round);
        round.Stage.Should().Be(InvestigationStage.AwaitingApproval);

        InvestigationRounds.CapReached(5).Should().Be(
            "This ticket has reached 5 rounds, the most allowed. Approve, override or reject this round.");
        InvestigationRounds.CapReached(12).Should().Be(
            "This ticket has reached 12 rounds, the most allowed. Approve, override or reject this round.");
    }

    [Fact]
    public void SupersedeRejectsGatheredEvidenceAClosedRoundAndAMissingDecision()
    {
        var decision = new DecisionRecord("requestInfo", "note", "bryan", At);
        var gathered = InvestigationRounds.UserEvidence("text", "bryan", At, "E-01", 1);
        var notUser = new EvidenceItem(EvidenceId.New(), EvidenceSource.Spc, "Chart", "Point", At, "E-01", "E-01", suppliedBy: "bryan");

        var wrongSource = () => InvestigationRounds.Supersede(Round(InvestigationStage.RecommendationReady), decision, 5, notUser);
        wrongSource.Should().Throw<DomainRuleException>().WithMessage("Only user-provided evidence closes a round.");

        var closedRound = () => InvestigationRounds.Supersede(Round(InvestigationStage.Approved), decision, 5, gathered);
        closedRound.Should().Throw<DomainRuleException>().WithMessage("*not waiting for a decision*");

        var capped = () => InvestigationRounds.Supersede(Round(InvestigationStage.Escalated).WithRound(5), decision, 5);
        capped.Should().Throw<DomainRuleException>().WithMessage(InvestigationRounds.CapReached(5));

        var missingRound = () => InvestigationRounds.Supersede(null!, decision, 5);
        missingRound.Should().Throw<ArgumentNullException>().WithParameterName("round");
        var missingDecision = () => InvestigationRounds.Supersede(Round(InvestigationStage.Escalated), null!, 5);
        missingDecision.Should().Throw<ArgumentNullException>().WithParameterName("decision");
    }

    [Fact]
    public void CarriedEvidenceIsOnlyTheUserProvidedRecordsInOrder()
    {
        var user = InvestigationRounds.UserEvidence("Bearing wear.", "bryan", At, "E-01", 1);
        var chart = new EvidenceItem(EvidenceId.New(), EvidenceSource.Spc, "Chart", "Point out.", At, "E-01", "E-01");
        var round = new InvestigationSnapshot(
            InvestigationId.New(),
            IncidentId.New(),
            InvestigationStage.Superseded,
            [chart, user],
            [],
            null,
            "corr");

        InvestigationRounds.CarriedUserEvidence(round).Should().Equal(user);

        var missing = () => InvestigationRounds.CarriedUserEvidence(null!);
        missing.Should().Throw<ArgumentNullException>().WithParameterName("round");
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
