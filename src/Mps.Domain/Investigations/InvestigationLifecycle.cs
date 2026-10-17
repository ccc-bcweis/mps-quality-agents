// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain;

namespace Mps.Domain.Investigations;

/// <summary>
/// Demo reset may close an investigation that has not reached a human decision or a failure.
/// Approved, rejected, and failed investigations stay as they are so a finished decision is not relabeled.
/// </summary>
public static class InvestigationLifecycle
{
    public static bool CanCloseByDemoReset(InvestigationStage stage) => stage is
        InvestigationStage.Started
        or InvestigationStage.EvidenceGathered
        or InvestigationStage.RecommendationReady
        or InvestigationStage.Escalated
        or InvestigationStage.AwaitingApproval;

    public static InvestigationSnapshot CloseByDemoReset(InvestigationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Stage == InvestigationStage.ClosedByDemoReset)
        {
            return snapshot;
        }

        if (!CanCloseByDemoReset(snapshot.Stage))
        {
            throw new DomainRuleException($"Cannot close an investigation from {snapshot.Stage} by demo reset.");
        }

        return snapshot.With(InvestigationStage.ClosedByDemoReset, snapshot.EscalationReason);
    }
}
