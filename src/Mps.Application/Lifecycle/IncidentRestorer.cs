// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Identity;
using Mps.Domain.Incidents;

namespace Mps.Application.Lifecycle;

/// <summary>
/// Rebuilds an incident at a stored stage. The domain type always opens at TicketOpened and has no rehydrate method.
/// </summary>
public static class IncidentRestorer
{
    public static Incident Restore(
        IncidentId id,
        ToolId toolId,
        string? chamberId,
        string parameter,
        IncidentChannel channel,
        Severity severity,
        string summary,
        string correlationId,
        DateTimeOffset openedAt,
        IncidentStage stage)
    {
        var incident = Incident.Open(id, toolId, chamberId, parameter, channel, severity, summary, correlationId, openedAt);
        foreach (var step in IncidentStagePath.Between(IncidentStage.TicketOpened, stage, blockApproval: false))
        {
            incident.Advance(step);
        }

        return incident;
    }
}
