// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Workflow;
using Mps.Domain.Identity;
using Mps.Domain.Investigations;

namespace Mps.Orchestration;

/// <summary>
/// Stub showing where a LangGraph service would plug in. It is not a runtime. See docs/langgraph-compat.md.
/// </summary>
public sealed class LangGraphWorkflowAdapter : IInvestigationWorkflow
{
    public Task<InvestigationSnapshot> StartAsync(IncidentId incidentId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        throw Stub(incidentId.ToString());
    }

    public Task<InvestigationSnapshot> ResumeWithHumanInputAsync(
        InvestigationId investigationId,
        HumanDecision decision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);
        _ = cancellationToken;
        throw Stub(investigationId.ToString());
    }

    public Task<InvestigationSnapshot> GetStateAsync(InvestigationId investigationId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        throw Stub(investigationId.ToString());
    }

    private static NotSupportedException Stub(string id) =>
        new($"LangGraphWorkflowAdapter is a stub and cannot run investigation {id}. Microsoft Agent Framework is the orchestration runtime. See docs/langgraph-compat.md.");
}
