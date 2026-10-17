// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain;
using Mps.Domain.Approval;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Domain.Investigations;

public enum InvestigationStage
{
    Started,
    EvidenceGathered,
    RecommendationReady,
    Escalated,
    AwaitingApproval,
    Approved,
    Rejected,
    Failed,
    ClosedByDemoReset,

    /// <summary>A person added evidence; this round is closed read-only and the next round re-runs the agents.</summary>
    Superseded,
}

public enum HumanDecisionKind
{
    Approve,
    Reject,
    RequestInfo,

    /// <summary>Approve an escalated investigation anyway. Requires a written justification kept on the approval record.</summary>
    Override,
}

public sealed class HumanDecision
{
    public HumanDecision(HumanDecisionKind kind, string? comment, ApprovalToken? approvalToken)
    {
        if (kind == HumanDecisionKind.Approve && approvalToken is null)
        {
            throw new DomainRuleException("Approval requires an approval token.");
        }

        Kind = kind;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        ApprovalToken = approvalToken;
    }

    public HumanDecisionKind Kind { get; }

    public string? Comment { get; }

    public ApprovalToken? ApprovalToken { get; }
}

public sealed class InvestigationSnapshot
{
    public InvestigationSnapshot(
        InvestigationId id,
        IncidentId incidentId,
        InvestigationStage stage,
        IReadOnlyList<EvidenceItem> evidence,
        IReadOnlyList<Hypothesis> hypotheses,
        string? escalationReason,
        string correlationId)
    {
        if (id.IsEmpty)
        {
            throw new DomainRuleException("Investigation id is required.");
        }

        if (incidentId.IsEmpty)
        {
            throw new DomainRuleException("Incident id is required.");
        }

        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(hypotheses);

        if (stage == InvestigationStage.Escalated && string.IsNullOrWhiteSpace(escalationReason))
        {
            throw new DomainRuleException("An escalation names the gap or conflict.");
        }

        Id = id;
        IncidentId = incidentId;
        Stage = stage;
        Evidence = evidence.ToArray();
        Hypotheses = hypotheses.ToArray();
        EscalationReason = string.IsNullOrWhiteSpace(escalationReason) ? null : escalationReason.Trim();
        CorrelationId = RequiredText.Value(correlationId, "Correlation id is required.");
    }

    public InvestigationId Id { get; }

    public IncidentId IncidentId { get; }

    public InvestigationStage Stage { get; }

    public IReadOnlyList<EvidenceItem> Evidence { get; }

    public IReadOnlyList<Hypothesis> Hypotheses { get; }

    public string? EscalationReason { get; }

    public string CorrelationId { get; }

    /// <summary>The person's decision on this investigation, once made: kind, note or justification, who, when.</summary>
    public DecisionRecord? Decision { get; init; }

    /// <summary>Round number on the ticket: 1 for the first run, one more for each time a person added evidence.</summary>
    public int Round { get; init; } = 1;

    public InvestigationSnapshot WithEvidence(IReadOnlyList<EvidenceItem> evidence) =>
        new(Id, IncidentId, Stage, evidence, Hypotheses, EscalationReason, CorrelationId) { Decision = Decision, Round = Round };

    public InvestigationSnapshot WithRound(int round) =>
        new(Id, IncidentId, Stage, Evidence, Hypotheses, EscalationReason, CorrelationId) { Decision = Decision, Round = round < 1 ? 1 : round };

    /// <summary>A copy at another stage and reason that keeps the decision and round.</summary>
    public InvestigationSnapshot With(InvestigationStage stage, string? escalationReason, DecisionRecord? decision = null) =>
        new(Id, IncidentId, stage, Evidence, Hypotheses, escalationReason, CorrelationId) { Decision = decision ?? Decision, Round = Round };
}

/// <summary>A recorded human decision. Kind is approve, override, reject or requestInfo.</summary>
public sealed record DecisionRecord(string Kind, string? Note, string? DecidedBy, DateTimeOffset DecidedAt);
