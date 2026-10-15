// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Incidents;

namespace Mps.Domain.Lifecycle;

/// <summary>
/// Allowed incident transitions. Alert creation is outside this machine; an incident starts as a ticket.
/// </summary>
public static class IncidentLifecycle
{
    private static readonly HashSet<(IncidentStage From, IncidentStage To)> Allowed = new()
    {
        (IncidentStage.TicketOpened, IncidentStage.Investigating),
        (IncidentStage.Investigating, IncidentStage.RecommendationReady),
        (IncidentStage.Investigating, IncidentStage.Escalated),
        (IncidentStage.RecommendationReady, IncidentStage.AwaitingApproval),
        (IncidentStage.RecommendationReady, IncidentStage.Escalated),
        (IncidentStage.Escalated, IncidentStage.Investigating),
        (IncidentStage.Escalated, IncidentStage.AwaitingApproval),
        (IncidentStage.Escalated, IncidentStage.Closed),
        (IncidentStage.AwaitingApproval, IncidentStage.Approved),
        (IncidentStage.AwaitingApproval, IncidentStage.Rejected),
        (IncidentStage.AwaitingApproval, IncidentStage.Escalated),
        (IncidentStage.Approved, IncidentStage.Closed),
        (IncidentStage.Rejected, IncidentStage.Investigating),
        (IncidentStage.Rejected, IncidentStage.Closed),
        (IncidentStage.TicketOpened, IncidentStage.ClosedByDemoReset),
        (IncidentStage.Investigating, IncidentStage.ClosedByDemoReset),
        (IncidentStage.RecommendationReady, IncidentStage.ClosedByDemoReset),
        (IncidentStage.Escalated, IncidentStage.ClosedByDemoReset),
        (IncidentStage.AwaitingApproval, IncidentStage.ClosedByDemoReset),
        (IncidentStage.Approved, IncidentStage.ClosedByDemoReset),
        (IncidentStage.Rejected, IncidentStage.ClosedByDemoReset),
    };

    public static bool CanTransition(IncidentStage from, IncidentStage to) => Allowed.Contains((from, to));
}
