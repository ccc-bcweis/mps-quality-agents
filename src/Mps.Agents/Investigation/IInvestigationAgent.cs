// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Incidents;

namespace Mps.Agents.Investigation;

/// <summary>
/// Collects an evidence pack. It does not diagnose, write, or approve.
/// </summary>
public interface IInvestigationAgent
{
    Task<GatherResult> GatherAsync(Incident incident, CancellationToken cancellationToken);
}
