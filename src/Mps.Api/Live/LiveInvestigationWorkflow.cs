// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Explain;
using Mps.Agents.Impact;
using Mps.Application.Evidence;
using Mps.Application.Persistence;
using Mps.Application.Workflow;
using Mps.Domain.Identity;
using Mps.Domain.Investigations;
using Mps.Orchestration;

using AppMatrixEntry = Mps.Application.Evidence.EvidenceMatrixEntry;
using AppStance = Mps.Application.Evidence.EvidenceStance;

namespace Mps.Api.Live;

/// <summary>
/// Hosts the Microsoft Agent Framework workflow behind the API lifecycle. The framework halts at the
/// approval port; the API calls that stage "recommendation ready" until a person decides.
/// </summary>
public sealed class LiveInvestigationWorkflow : IInvestigationWorkflow
{
    private readonly MafInvestigationWorkflow _inner;
    private readonly IInvestigationNarrativeLog _narratives;
    private readonly WhyItMattersCatalog _catalog;

    public LiveInvestigationWorkflow(MafInvestigationWorkflow inner, IInvestigationNarrativeLog narratives, WhyItMattersCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(narratives);
        ArgumentNullException.ThrowIfNull(catalog);
        _inner = inner;
        _narratives = narratives;
        _catalog = catalog;
    }

    public Task<InvestigationSnapshot> StartAsync(IncidentId incidentId, CancellationToken cancellationToken) =>
        StartRoundAsync(incidentId, [], cancellationToken);

    public async Task<InvestigationSnapshot> StartRoundAsync(IncidentId incidentId, IReadOnlyList<Mps.Domain.Evidence.EvidenceItem> userEvidence, CancellationToken cancellationToken)
    {
        var snapshot = ToApi(await _inner.StartRoundAsync(incidentId, userEvidence, cancellationToken).ConfigureAwait(false));
        if (snapshot.Evidence.Count > 0)
        {
            _narratives.Stage(snapshot.Id, Narrative(snapshot, _inner.RejectedAnswerFor(snapshot.Id)));
        }

        return snapshot;
    }

    public async Task<InvestigationSnapshot> ResumeWithHumanInputAsync(InvestigationId investigationId, HumanDecision decision, CancellationToken cancellationToken) =>
        await _inner.ResumeWithHumanInputAsync(investigationId, decision, cancellationToken).ConfigureAwait(false);

    public async Task<InvestigationSnapshot> GetStateAsync(InvestigationId investigationId, CancellationToken cancellationToken) =>
        ToApi(await _inner.GetStateAsync(investigationId, cancellationToken).ConfigureAwait(false));

    private static InvestigationSnapshot ToApi(InvestigationSnapshot snapshot)
    {
        if (snapshot.Stage != InvestigationStage.AwaitingApproval)
        {
            return snapshot;
        }

        return new InvestigationSnapshot(
            snapshot.Id,
            snapshot.IncidentId,
            InvestigationStage.RecommendationReady,
            snapshot.Evidence,
            snapshot.Hypotheses,
            snapshot.EscalationReason,
            snapshot.CorrelationId);
    }

    private InvestigationNarrative Narrative(InvestigationSnapshot snapshot, RejectedAnswer? rejected)
    {
        var chosen = snapshot.Hypotheses.Count > 0 ? snapshot.Hypotheses[0].Category : (Mps.Domain.Hypotheses.RootCauseCategory?)null;
        var notes = snapshot.Evidence
            .Select(item =>
            {
                var cited = _catalog.ExplainItem(item);
                return new EvidenceNote(item.Id.Value, cited.WhyItMatters, cited.SignalId);
            })
            .ToArray();
        var matrix = EvidenceMatrixBuilder.FromItems(snapshot.Evidence, SignalShape.Unknown, chosen, _catalog)
            .Select(entry => new AppMatrixEntry(entry.Category, entry.EvidenceId.Value, (AppStance)(int)entry.Stance, entry.WhyItMatters))
            .ToArray();
        return InvestigationNarrative.Create(notes, matrix, snapshot.Evidence.Select(item => item.Id.Value).ToHashSet(), rejected);
    }
}
