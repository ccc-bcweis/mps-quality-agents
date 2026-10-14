// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Errors;
using Mps.Application.Persistence;
using Mps.Domain;
using Mps.Domain.Capa;
using Mps.Domain.Incidents;
using Mps.Domain.Lifecycle;

namespace Mps.Application.Lifecycle;

/// <summary>
/// Single-step lifecycle moves. A request that skips a stage is rejected.
/// Critical incidents cannot enter approval until they have been escalated.
/// </summary>
public static class LifecycleMoves
{
    public static void MoveOne(StoredIncident stored, IncidentStage next)
    {
        ArgumentNullException.ThrowIfNull(stored);
        if (stored.EscalationRequired
            && !stored.HasEscalated
            && stored.Incident.Stage == IncidentStage.RecommendationReady
            && next == IncidentStage.AwaitingApproval)
        {
            throw new LifecycleConflictException("Critical severity requires escalation before approval.");
        }

        try
        {
            stored.Incident.Advance(next);
        }
        catch (DomainRuleException exception)
        {
            throw new LifecycleConflictException(exception.Message, exception);
        }

        if (next == IncidentStage.Escalated)
        {
            stored.MarkEscalated();
        }
    }

    public static void MoveOne(CapaAction capa, CapaStage next)
    {
        ArgumentNullException.ThrowIfNull(capa);
        try
        {
            capa.Advance(next);
        }
        catch (DomainRuleException exception)
        {
            throw new LifecycleConflictException(exception.Message, exception);
        }
    }

    public static void WalkTo(StoredIncident stored, IncidentStage target)
    {
        ArgumentNullException.ThrowIfNull(stored);
        foreach (var step in IncidentStagePath.Between(stored.Incident.Stage, target, BlockApproval(stored)))
        {
            MoveOne(stored, step);
        }
    }

    public static bool BlockApproval(StoredIncident stored) =>
        stored.EscalationRequired && !stored.HasEscalated;
}
