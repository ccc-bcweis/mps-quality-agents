// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Identity;

namespace Mps.Orchestration;

/// <summary>
/// In-memory sessions shared by every workflow instance that must resume the same investigation.
/// </summary>
public sealed class InvestigationSessionStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, InvestigationSession> _sessions = [];

    public InvestigationSession Create(InvestigationId id, IncidentId incidentId)
    {
        var session = new InvestigationSession(id, incidentId);
        lock (_gate)
        {
            _sessions.Add(id.Value, session);
        }

        return session;
    }

    public InvestigationSession Require(InvestigationId id)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(id.Value, out var session))
            {
                return session;
            }
        }

        throw new InvalidOperationException("Investigation " + id + " was not found.");
    }
}
