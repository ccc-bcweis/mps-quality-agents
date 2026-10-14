// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Identity;
using Mps.Domain.Incidents;

namespace Mps.Agents.Incidents;

/// <summary>
/// Read port for incidents. The frozen application contracts do not expose one.
/// </summary>
public interface IIncidentSource
{
    Task<Incident?> FindAsync(IncidentId incidentId, CancellationToken cancellationToken);
}
