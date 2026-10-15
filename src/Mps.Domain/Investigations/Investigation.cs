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

    public InvestigationSnapshot WithEvidence(IReadOnlyList<EvidenceItem> evidence) =>
        new(Id, IncidentId, Stage, evidence, Hypotheses, EscalationReason, CorrelationId);
}
