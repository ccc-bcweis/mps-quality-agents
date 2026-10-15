// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Incidents;
using Mps.Application.Persistence;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;

namespace Mps.Api.Live;

/// <summary>Reads incidents for the agent workflow from the API store, one scope per call.</summary>
public sealed class StoreIncidentSource : IIncidentSource
{
    private readonly IServiceScopeFactory _scopes;

    public StoreIncidentSource(IServiceScopeFactory scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        _scopes = scopes;
    }

    public async Task<Incident?> FindAsync(IncidentId incidentId, CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IIncidentLog>().GetAsync(incidentId, cancellationToken).ConfigureAwait(false);
        return stored?.Incident;
    }
}
