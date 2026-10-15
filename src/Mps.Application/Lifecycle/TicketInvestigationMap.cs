// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Identity;
using Mps.Domain.Investigations;

namespace Mps.Application.Lifecycle;

/// <summary>Picks the investigation a ticket should open. The ticket id is not an input.</summary>
public static class TicketInvestigationMap
{
    /// <summary>
    /// The last investigation for <paramref name="incidentId"/>.
    /// <paramref name="oldestFirst"/> is start order. A rejected investigation still counts.
    /// </summary>
    public static InvestigationSnapshot? Latest(IncidentId incidentId, IEnumerable<InvestigationSnapshot> oldestFirst)
    {
        ArgumentNullException.ThrowIfNull(oldestFirst);
        InvestigationSnapshot? latest = null;
        foreach (var snapshot in oldestFirst)
        {
            if (snapshot.IncidentId == incidentId)
            {
                latest = snapshot;
            }
        }

        return latest;
    }
}
