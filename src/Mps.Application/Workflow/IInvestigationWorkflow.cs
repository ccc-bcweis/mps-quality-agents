// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Evidence;
using Mps.Domain.Identity;
using Mps.Domain.Investigations;

namespace Mps.Application.Workflow;

/// <summary>
/// Orchestration boundary. Microsoft Agent Framework implements this. A LangGraph service would too.
/// </summary>
public interface IInvestigationWorkflow
{
    Task<InvestigationSnapshot> StartAsync(IncidentId incidentId, CancellationToken cancellationToken);

    /// <summary>
    /// A later round (the evidence loop): gathers again, adds the user-provided records to the pack, and runs the
    /// evidence gate, the resolution and its guards as on the first round.
    /// </summary>
    Task<InvestigationSnapshot> StartRoundAsync(
        IncidentId incidentId,
        IReadOnlyList<EvidenceItem> userEvidence,
        CancellationToken cancellationToken);

    Task<InvestigationSnapshot> ResumeWithHumanInputAsync(
        InvestigationId investigationId,
        HumanDecision decision,
        CancellationToken cancellationToken);

    Task<InvestigationSnapshot> GetStateAsync(
        InvestigationId investigationId,
        CancellationToken cancellationToken);
}
