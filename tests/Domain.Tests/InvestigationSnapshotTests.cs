// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Approval;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;
using Mps.Domain.Investigations;
using Mps.Domain.Lifecycle;

namespace Mps.Domain.Tests;

public sealed class InvestigationSnapshotTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);

    private static readonly byte[] Key = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

    [Fact]
    public void ASnapshotCopiesEvidenceAndHypothesesAndTrimsTheCorrelation()
    {
        var evidence = new List<EvidenceItem> { Item() };
        var hypotheses = new List<Hypothesis> { Cause(evidence[0].Id) };
        var snapshot = new InvestigationSnapshot(
            InvestigationId.New(),
            IncidentId.New(),
            InvestigationStage.EvidenceGathered,
            evidence,
            hypotheses,
            "  ",
            "  corr-1  ");

        snapshot.Stage.Should().Be(InvestigationStage.EvidenceGathered);
        snapshot.EscalationReason.Should().BeNull();
        snapshot.CorrelationId.Should().Be("corr-1");
        snapshot.Decision.Should().BeNull();
        snapshot.Round.Should().Be(1);
        snapshot.Evidence.Should().Equal(evidence);
        snapshot.Hypotheses.Should().Equal(hypotheses);

        evidence.Clear();
        hypotheses.Clear();
        snapshot.Evidence.Should().ContainSingle();
        snapshot.Hypotheses.Should().ContainSingle();
    }

    [Fact]
    public void AnEscalationKeepsATrimmedReasonAndAPlainStageDropsABlankOne()
    {
        var escalated = new InvestigationSnapshot(
            InvestigationId.New(),
            IncidentId.New(),
            InvestigationStage.Escalated,
            [],
            [],
            "  The charts disagree.  ",
            "corr-1");

        escalated.EscalationReason.Should().Be("The charts disagree.");
        escalated.Evidence.Should().BeEmpty();
        escalated.Hypotheses.Should().BeEmpty();

        var started = new InvestigationSnapshot(
            InvestigationId.New(),
            IncidentId.New(),
            InvestigationStage.Started,
            [],
            [],
            null,
            "corr-1");
        started.EscalationReason.Should().BeNull();
    }

    [Fact]
    public void ASnapshotRequiresIdsListsAndACorrelationAndAnEscalationNamesTheGap()
    {
        var investigationId = InvestigationId.New();
        var incidentId = IncidentId.New();
        var evidence = new List<EvidenceItem>();
        var hypotheses = new List<Hypothesis>();

        Rejects(() => new InvestigationSnapshot(default, incidentId, InvestigationStage.Started, evidence, hypotheses, null, "c"), "Investigation id is required.");
        Rejects(() => new InvestigationSnapshot(investigationId, default, InvestigationStage.Started, evidence, hypotheses, null, "c"), "Incident id is required.");
        Rejects(() => new InvestigationSnapshot(investigationId, incidentId, InvestigationStage.Started, evidence, hypotheses, null, " "), "Correlation id is required.");
        Rejects(() => new InvestigationSnapshot(investigationId, incidentId, InvestigationStage.Started, evidence, hypotheses, null, null!), "Correlation id is required.");
        Rejects(() => new InvestigationSnapshot(investigationId, incidentId, InvestigationStage.Escalated, evidence, hypotheses, null, "c"), "An escalation names the gap or conflict.");
        Rejects(() => new InvestigationSnapshot(investigationId, incidentId, InvestigationStage.Escalated, evidence, hypotheses, "  ", "c"), "An escalation names the gap or conflict.");

        var missingEvidence = () => new InvestigationSnapshot(investigationId, incidentId, InvestigationStage.Started, null!, hypotheses, null, "c");
        missingEvidence.Should().Throw<ArgumentNullException>().WithParameterName("evidence");
        var missingHypotheses = () => new InvestigationSnapshot(investigationId, incidentId, InvestigationStage.Started, evidence, null!, null, "c");
        missingHypotheses.Should().Throw<ArgumentNullException>().WithParameterName("hypotheses");
    }

    [Fact]
    public void WithCopiesTheStageTheEvidenceTheRoundAndTheDecision()
    {
        var decision = new DecisionRecord("reject", "  gap  ", "qe", At);
        var snapshot = new InvestigationSnapshot(
            InvestigationId.New(),
            IncidentId.New(),
            InvestigationStage.Escalated,
            [Item()],
            [],
            "gap",
            "corr-1")
        {
            Decision = decision,
            Round = 2,
        };
        var added = Item();
        var evidence = new List<EvidenceItem> { added };

        var withEvidence = snapshot.WithEvidence(evidence);
        withEvidence.Evidence.Should().ContainSingle().Which.Should().BeSameAs(added);
        withEvidence.Stage.Should().Be(InvestigationStage.Escalated);
        withEvidence.Decision.Should().Be(decision);
        withEvidence.Round.Should().Be(2);
        withEvidence.EscalationReason.Should().Be("gap");
        evidence.Clear();
        withEvidence.Evidence.Should().ContainSingle();

        snapshot.WithRound(0).Round.Should().Be(1);
        snapshot.WithRound(-3).Round.Should().Be(1);
        snapshot.WithRound(1).Round.Should().Be(1);
        snapshot.WithRound(4).Round.Should().Be(4);
        snapshot.Round.Should().Be(2);

        var replacement = new DecisionRecord("approve", null, "qe", At);
        var moved = snapshot.With(InvestigationStage.Approved, "  still the gap  ", replacement);
        moved.Stage.Should().Be(InvestigationStage.Approved);
        moved.EscalationReason.Should().Be("still the gap");
        moved.Decision.Should().Be(replacement);
        moved.Round.Should().Be(2);
        moved.Evidence.Should().Equal(snapshot.Evidence);

        var kept = snapshot.With(InvestigationStage.AwaitingApproval, null, null);
        kept.Decision.Should().Be(decision);
        kept.EscalationReason.Should().BeNull();
        kept.Stage.Should().Be(InvestigationStage.AwaitingApproval);
    }

    [Theory]
    [InlineData(HumanDecisionKind.Reject)]
    [InlineData(HumanDecisionKind.RequestInfo)]
    [InlineData(HumanDecisionKind.Override)]
    public void ADecisionOtherThanApproveDoesNotNeedAToken(HumanDecisionKind kind)
    {
        var decision = new HumanDecision(kind, "  Looks grounded.  ", null);

        decision.Kind.Should().Be(kind);
        decision.Comment.Should().Be("Looks grounded.");
        decision.ApprovalToken.Should().BeNull();

        new HumanDecision(kind, "   ", null).Comment.Should().BeNull();
        new HumanDecision(kind, null, null).Comment.Should().BeNull();
    }

    [Fact]
    public void ApproveRequiresATokenAndKeepsIt()
    {
        var missing = () => new HumanDecision(HumanDecisionKind.Approve, "ok", null);
        missing.Should().Throw<DomainRuleException>().WithMessage("Approval requires an approval token.");

        var token = ApprovalToken.Issue(ApprovalTokenId.New(), "eng-17", "ABC", At, TimeSpan.FromMinutes(15), Key);
        var decision = new HumanDecision(HumanDecisionKind.Approve, "  ok  ", token);

        decision.Kind.Should().Be(HumanDecisionKind.Approve);
        decision.Comment.Should().Be("ok");
        decision.ApprovalToken.Should().BeSameAs(token);

        new HumanDecision(HumanDecisionKind.Override, null, token).ApprovalToken.Should().BeSameAs(token);
    }

    [Theory]
    [InlineData(InvestigationStage.Started, true)]
    [InlineData(InvestigationStage.EvidenceGathered, true)]
    [InlineData(InvestigationStage.RecommendationReady, true)]
    [InlineData(InvestigationStage.Escalated, true)]
    [InlineData(InvestigationStage.AwaitingApproval, true)]
    [InlineData(InvestigationStage.Approved, false)]
    [InlineData(InvestigationStage.Rejected, false)]
    [InlineData(InvestigationStage.Failed, false)]
    [InlineData(InvestigationStage.Superseded, false)]
    [InlineData(InvestigationStage.ClosedByDemoReset, false)]
    public void DemoResetClosesOnlyAnInvestigationThatHasNotFinished(InvestigationStage stage, bool allowed)
    {
        InvestigationLifecycle.CanCloseByDemoReset(stage).Should().Be(allowed);
        DemoReset.StatusLabel.Should().Be("Closed by reset");
    }

    [Fact]
    public void DemoResetKeepsTheRoundTheDecisionAndTheReason()
    {
        var decision = new DecisionRecord("requestInfo", "note", "bryan", At);
        var snapshot = new InvestigationSnapshot(
            InvestigationId.New(),
            IncidentId.New(),
            InvestigationStage.Escalated,
            [Item()],
            [],
            "gap",
            "corr-1")
        {
            Decision = decision,
            Round = 3,
        };

        var closed = InvestigationLifecycle.CloseByDemoReset(snapshot);

        closed.Should().NotBeSameAs(snapshot);
        closed.Stage.Should().Be(InvestigationStage.ClosedByDemoReset);
        closed.EscalationReason.Should().Be("gap");
        closed.Decision.Should().Be(decision);
        closed.Round.Should().Be(3);
        closed.Evidence.Should().Equal(snapshot.Evidence);
        closed.CorrelationId.Should().Be(snapshot.CorrelationId);

        var again = InvestigationLifecycle.CloseByDemoReset(closed);
        again.Should().BeSameAs(closed);

        var finished = () => InvestigationLifecycle.CloseByDemoReset(Snapshot(InvestigationStage.Approved, null));
        finished.Should().Throw<DomainRuleException>().WithMessage("*Cannot close an investigation from Approved by demo reset*");
        var failed = () => InvestigationLifecycle.CloseByDemoReset(Snapshot(InvestigationStage.Failed, null));
        failed.Should().Throw<DomainRuleException>().WithMessage("*Cannot close an investigation from Failed by demo reset*");
        var rejected = () => InvestigationLifecycle.CloseByDemoReset(Snapshot(InvestigationStage.Rejected, null));
        rejected.Should().Throw<DomainRuleException>().WithMessage("*Cannot close an investigation from Rejected by demo reset*");
        var superseded = () => InvestigationLifecycle.CloseByDemoReset(Snapshot(InvestigationStage.Superseded, null));
        superseded.Should().Throw<DomainRuleException>().WithMessage("*Cannot close an investigation from Superseded by demo reset*");

        var missing = () => InvestigationLifecycle.CloseByDemoReset(null!);
        missing.Should().Throw<ArgumentNullException>().WithParameterName("snapshot");
    }

    private static InvestigationSnapshot Snapshot(InvestigationStage stage, string? reason)
    {
        var evidence = new[] { Item() };
        return new InvestigationSnapshot(InvestigationId.New(), IncidentId.New(), stage, evidence, [], reason, "corr-1");
    }

    private static EvidenceItem Item()
    {
        return new EvidenceItem(EvidenceId.New(), EvidenceSource.Spc, "Chart", "Point out.", At, "E-02", "E-01");
    }

    private static Hypothesis Cause(EvidenceId evidenceId)
    {
        return new Hypothesis(
            HypothesisId.New(),
            RootCauseCategory.ProcessDrift,
            "The chamber has drifted.",
            0.4,
            [evidenceId],
            [],
            []);
    }

    private static void Rejects<T>(Func<T> act, string message)
    {
        act.Should().Throw<DomainRuleException>().WithMessage(message);
    }
}
