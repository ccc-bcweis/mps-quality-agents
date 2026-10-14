// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Evidence;
using Mps.Domain.Identity;

namespace Mps.Application.Persistence;

/// <summary>
/// Stores whyItMatters, signalId, and the evidence matrix for an investigation.
/// Stage is applied when the investigation row is inserted.
/// </summary>
public interface IInvestigationNarrativeLog
{
    void Stage(InvestigationId id, InvestigationNarrative narrative);

    Task<InvestigationNarrative> GetAsync(InvestigationId id, CancellationToken cancellationToken);
}
