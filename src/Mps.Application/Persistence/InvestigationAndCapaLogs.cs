// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Capa;
using Mps.Domain.Identity;
using Mps.Domain.Investigations;

namespace Mps.Application.Persistence;

public interface IInvestigationLog
{
    Task AddAsync(InvestigationSnapshot snapshot, CancellationToken cancellationToken);

    Task UpdateAsync(InvestigationSnapshot snapshot, CancellationToken cancellationToken);

    Task<InvestigationSnapshot?> GetAsync(InvestigationId id, CancellationToken cancellationToken);

    Task<InvestigationSnapshot?> FindActiveByIncidentAsync(IncidentId incidentId, CancellationToken cancellationToken);

    /// <summary>The latest investigation for the incident, or null when none has been started.</summary>
    Task<InvestigationSnapshot?> FindLatestByIncidentAsync(IncidentId incidentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<InvestigationSnapshot>> ListAsync(CancellationToken cancellationToken);
}

public interface ICapaLog
{
    Task AddAsync(CapaAction capa, string externalId, CancellationToken cancellationToken);

    Task UpdateAsync(CapaAction capa, CancellationToken cancellationToken);

    Task<StoredCapa?> GetAsync(CapaId id, CancellationToken cancellationToken);

    Task<StoredCapa?> FindByIncidentAsync(IncidentId incidentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredCapa>> ListAsync(CancellationToken cancellationToken);
}

public sealed record StoredCapa(CapaAction Capa, string ExternalId);
