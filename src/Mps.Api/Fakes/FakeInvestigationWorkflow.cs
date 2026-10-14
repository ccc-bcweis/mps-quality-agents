// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Persistence;
using Mps.Application.Workflow;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;
using Mps.Domain.Investigations;

namespace Mps.Api.Fakes;

/// <summary>
/// Deterministic stand-in until the agent lane's workflow is composed in. It does not call a model and it cannot write.
/// </summary>
public sealed class FakeInvestigationWorkflow : IInvestigationWorkflow
{
    private readonly IInvestigationLog _investigations;
    private readonly TimeProvider _time;

    public FakeInvestigationWorkflow(IInvestigationLog investigations, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(investigations);
        ArgumentNullException.ThrowIfNull(time);
        _investigations = investigations;
        _time = time;
    }

    public Task<InvestigationSnapshot> StartAsync(IncidentId incidentId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var evidenceId = EvidenceId.New();
        var evidence = new EvidenceItem(
            evidenceId,
            EvidenceSource.Spc,
            "SPC stand-in",
            "Deterministic stand-in until the agent workflow is connected.",
            _time.GetUtcNow(),
            null,
            null);
        var hypothesis = new Hypothesis(
            HypothesisId.New(),
            RootCauseCategory.ProcessDrift,
            "Deterministic stand-in. The agent workflow has not ranked a cause.",
            0.5d,
            [evidenceId],
            ["No confounder was evaluated by the stand-in."],
            ["Hold the affected lot and confirm the signal with a process engineer."]);
        var snapshot = new InvestigationSnapshot(
            InvestigationId.New(),
            incidentId,
            InvestigationStage.RecommendationReady,
            [evidence],
            [hypothesis],
            null,
            "inv-" + incidentId.ToString());
        return Task.FromResult(snapshot);
    }

    public async Task<InvestigationSnapshot> ResumeWithHumanInputAsync(
        InvestigationId investigationId,
        HumanDecision decision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var current = await _investigations.GetAsync(investigationId, cancellationToken).ConfigureAwait(false)
            ?? throw new Application.Errors.ResourceNotFoundException("Investigation was not found.");
        var stage = decision.Kind switch
        {
            HumanDecisionKind.Approve => InvestigationStage.Approved,
            HumanDecisionKind.Reject => InvestigationStage.Rejected,
            HumanDecisionKind.RequestInfo => InvestigationStage.Escalated,
            _ => current.Stage,
        };
        var reason = stage == InvestigationStage.Escalated
            ? decision.Comment ?? current.EscalationReason ?? "More information was requested."
            : current.EscalationReason;
        return new InvestigationSnapshot(
            current.Id,
            current.IncidentId,
            stage,
            current.Evidence,
            current.Hypotheses,
            reason,
            current.CorrelationId);
    }

    public async Task<InvestigationSnapshot> GetStateAsync(InvestigationId investigationId, CancellationToken cancellationToken)
    {
        return await _investigations.GetAsync(investigationId, cancellationToken).ConfigureAwait(false)
            ?? throw new Application.Errors.ResourceNotFoundException("Investigation was not found.");
    }
}
