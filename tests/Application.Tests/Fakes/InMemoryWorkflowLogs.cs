// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Lifecycle;
using Mps.Application.Persistence;
using Mps.Application.Workflow;
using Mps.Domain.Capa;
using Mps.Domain.Identity;
using Mps.Domain.Investigations;

namespace Mps.Application.Tests.Fakes;

public sealed class InMemoryInvestigationLog : IInvestigationLog
{
    private readonly List<InvestigationSnapshot> _snapshots = [];

    public Task AddAsync(InvestigationSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _ = cancellationToken;
        _snapshots.Add(snapshot);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(InvestigationSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _ = cancellationToken;
        var index = _snapshots.FindIndex(item => item.Id == snapshot.Id);
        if (index >= 0)
        {
            _snapshots[index] = snapshot;
        }
        else
        {
            _snapshots.Add(snapshot);
        }

        return Task.CompletedTask;
    }

    public Task<InvestigationSnapshot?> GetAsync(InvestigationId id, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(_snapshots.LastOrDefault(item => item.Id == id));
    }

    public Task<InvestigationSnapshot?> FindActiveByIncidentAsync(IncidentId incidentId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var match = _snapshots.LastOrDefault(item =>
            item.IncidentId == incidentId
            && item.Stage is not (InvestigationStage.Rejected or InvestigationStage.Failed or InvestigationStage.ClosedByDemoReset));
        return Task.FromResult(match);
    }

    public Task<InvestigationSnapshot?> FindLatestByIncidentAsync(IncidentId incidentId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(TicketInvestigationMap.Latest(incidentId, _snapshots));
    }

    public Task<IReadOnlyList<InvestigationSnapshot>> ListAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        IReadOnlyList<InvestigationSnapshot> newestFirst = _snapshots.AsEnumerable().Reverse().ToArray();
        return Task.FromResult(newestFirst);
    }
}

public sealed class InMemoryCapaLog : ICapaLog
{
    private readonly List<StoredCapa> _capas = [];

    public Task AddAsync(CapaAction capa, string externalId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capa);
        _ = cancellationToken;
        _capas.Add(new StoredCapa(capa, externalId));
        return Task.CompletedTask;
    }

    public Task UpdateAsync(CapaAction capa, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capa);
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    public Task<StoredCapa?> GetAsync(CapaId id, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(_capas.FirstOrDefault(item => item.Capa.Id == id));
    }

    public Task<StoredCapa?> FindByIncidentAsync(IncidentId incidentId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(_capas.FirstOrDefault(item => item.Capa.IncidentId == incidentId));
    }

    public Task<IReadOnlyList<StoredCapa>> ListAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<StoredCapa>>(_capas.ToArray());
    }
}

public sealed class InMemoryApprovalLog : IApprovalLog
{
    private readonly List<StoredApproval> _approvals = [];

    public IReadOnlyList<StoredApproval> Approvals => _approvals;

    public Task AddAsync(StoredApproval approval, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approval);
        _ = cancellationToken;
        _approvals.Add(approval);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(StoredApproval approval, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approval);
        _ = cancellationToken;
        var index = _approvals.FindIndex(item => item.Token.Id == approval.Token.Id);
        if (index < 0)
        {
            throw new InvalidOperationException("Approval was not found.");
        }

        _approvals[index] = approval;
        return Task.CompletedTask;
    }

    public Task<StoredApproval?> FindByInvestigationAsync(InvestigationId investigationId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(_approvals.LastOrDefault(item => item.InvestigationId == investigationId));
    }

    public Task<IReadOnlyList<StoredApproval>> ListByInvestigationAsync(InvestigationId investigationId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        IReadOnlyList<StoredApproval> matches = _approvals.Where(item => item.InvestigationId == investigationId).ToArray();
        return Task.FromResult(matches);
    }
}

public sealed class StubWorkflow : IInvestigationWorkflow
{
    public InvestigationStage Stage { get; set; } = InvestigationStage.RecommendationReady;

    public Task<InvestigationSnapshot> StartAsync(IncidentId incidentId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var evidenceId = EvidenceId.New();
        var evidence = new Domain.Evidence.EvidenceItem(
            evidenceId,
            Domain.Evidence.EvidenceSource.Spc,
            "SPC stand-in",
            "Deterministic stand-in.",
            new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero),
            "E-02",
            null);
        var hypothesis = new Domain.Hypotheses.Hypothesis(
            HypothesisId.New(),
            Domain.Hypotheses.RootCauseCategory.ProcessDrift,
            "Stand-in cause.",
            0.5d,
            [evidenceId],
            ["No confounder was evaluated."],
            ["Hold the affected lot."]);
        var reason = Stage == InvestigationStage.Escalated ? "The stand-in escalated." : null;
        var snapshot = new InvestigationSnapshot(
            InvestigationId.New(),
            incidentId,
            Stage,
            [evidence],
            [hypothesis],
            reason,
            "workflow-" + incidentId);
        return Task.FromResult(snapshot);
    }

    public Task<InvestigationSnapshot> ResumeWithHumanInputAsync(
        InvestigationId investigationId,
        HumanDecision decision,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Resume is covered by the approval service.");

    public Task<InvestigationSnapshot> GetStateAsync(InvestigationId investigationId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Get is served from the investigation log.");
}
