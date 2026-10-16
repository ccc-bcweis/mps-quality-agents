// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Metadata;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;

namespace Mps.Application.Persistence;

public interface IAlertLog
{
    Task AddAsync(Alert alert, CancellationToken cancellationToken);

    /// <summary>Saves rule, severity, last-seen time and count after <see cref="Alert.Update"/>.</summary>
    Task UpdateAsync(Alert alert, CancellationToken cancellationToken);

    /// <summary>Marks the alert cleared at <paramref name="at"/>. A no-op when it is already cleared.</summary>
    Task ClearAsync(AlertId id, DateTimeOffset at, CancellationToken cancellationToken);

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

public interface ITicketLog
{
    Task<IReadOnlyList<Ticket>> ListAsync(CancellationToken cancellationToken);
}

/// <summary>
/// An incident plus the flags the lifecycle needs that the domain entity does not store.
/// Critical severity sets <see cref="EscalationRequired"/>. Entering Escalated sets <see cref="HasEscalated"/>.
/// </summary>
public sealed class StoredIncident
{
    public StoredIncident(Incident incident, bool escalationRequired, bool hasEscalated, RuleDecision? decision = null)
    {
        ArgumentNullException.ThrowIfNull(incident);
        Incident = incident;
        EscalationRequired = escalationRequired;
        HasEscalated = hasEscalated;
        Decision = decision;
    }

    public Incident Incident { get; }

    public bool EscalationRequired { get; }

    public bool HasEscalated { get; private set; }

    public RuleDecision? Decision { get; }

    public void MarkEscalated() => HasEscalated = true;
}
