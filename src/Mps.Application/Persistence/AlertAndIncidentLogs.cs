// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Identity;
using Mps.Domain.Incidents;

namespace Mps.Application.Persistence;

public interface IAlertLog
{
    Task AddAsync(Alert alert, CancellationToken cancellationToken);

    Task<IReadOnlyList<Alert>> ListAsync(ToolId? toolId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Alert>> ListBySignalAsync(
        ToolId toolId,
        string parameter,
        string? chamberId,
        CancellationToken cancellationToken);
}

public interface IIncidentLog
{
    Task AddAsync(StoredIncident incident, CancellationToken cancellationToken);

    Task<StoredIncident?> FindOpenAsync(ToolId toolId, string parameter, CancellationToken cancellationToken);

    Task<StoredIncident?> GetAsync(IncidentId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredIncident>> ListAsync(CancellationToken cancellationToken);

    Task SaveAsync(StoredIncident incident, DateTimeOffset at, CancellationToken cancellationToken);
}

/// <summary>
/// An incident plus the flags the lifecycle needs that the domain entity does not store.
/// Critical severity sets <see cref="EscalationRequired"/>. Entering Escalated sets <see cref="HasEscalated"/>.
/// </summary>
public sealed class StoredIncident
{
    public StoredIncident(Incident incident, bool escalationRequired, bool hasEscalated)
    {
        ArgumentNullException.ThrowIfNull(incident);
        Incident = incident;
        EscalationRequired = escalationRequired;
        HasEscalated = hasEscalated;
    }

    public Incident Incident { get; }

    public bool EscalationRequired { get; }

    public bool HasEscalated { get; private set; }

    public void MarkEscalated() => HasEscalated = true;
}
