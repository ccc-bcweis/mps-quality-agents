// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Investigation;
using Mps.Domain.Incidents;

namespace Mps.Api.Demo;

/// <summary>
/// Binds enterprise-tool lookups to the incident's correlation for the whole evidence gather.
/// The tool ports do not take a correlation, so the board reads it from this async flow.
/// </summary>
public sealed class CorrelationBoundInvestigationAgent : IInvestigationAgent
{
    private readonly IInvestigationAgent _inner;
    private readonly ScenarioEvidenceBoard _board;

    public CorrelationBoundInvestigationAgent(IInvestigationAgent inner, ScenarioEvidenceBoard board)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(board);
        _inner = inner;
        _board = board;
    }

    public async Task<GatherResult> GatherAsync(Incident incident, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incident);
        if (string.IsNullOrWhiteSpace(incident.CorrelationId))
        {
            return await _inner.GatherAsync(incident, cancellationToken).ConfigureAwait(false);
        }

        using (_board.Use(incident.CorrelationId))
        {
            return await _inner.GatherAsync(incident, cancellationToken).ConfigureAwait(false);
        }
    }
}
