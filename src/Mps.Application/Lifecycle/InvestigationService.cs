// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Errors;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Application.Workflow;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;

namespace Mps.Application.Lifecycle;

public interface IInvestigationService
{
    Task<InvestigationSnapshot> StartAsync(IncidentId incidentId, CancellationToken cancellationToken);

    Task<InvestigationSnapshot> GetAsync(InvestigationId investigationId, CancellationToken cancellationToken);
}

public sealed class InvestigationService : IInvestigationService
{
    private readonly IIncidentLog _incidents;
    private readonly IInvestigationLog _investigations;
    private readonly IInvestigationWorkflow _workflow;
    private readonly IQualityNotifier _notifier;
    private readonly TimeProvider _time;

    public InvestigationService(
        IIncidentLog incidents,
        IInvestigationLog investigations,
        IInvestigationWorkflow workflow,
        IQualityNotifier notifier,
        TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(incidents);
        ArgumentNullException.ThrowIfNull(investigations);
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(time);
        _incidents = incidents;
        _investigations = investigations;
        _workflow = workflow;
        _notifier = notifier;
        _time = time;
    }

    public async Task<InvestigationSnapshot> StartAsync(IncidentId incidentId, CancellationToken cancellationToken)
    {
        var stored = await _incidents.GetAsync(incidentId, cancellationToken).ConfigureAwait(false)
            ?? throw new ResourceNotFoundException("Incident was not found.");
        var active = await _investigations.FindActiveByIncidentAsync(incidentId, cancellationToken).ConfigureAwait(false);
        if (active is not null)
        {
            throw new LifecycleConflictException("An investigation is already open for this incident.");
        }

        if (stored.Incident.Stage is IncidentStage.Closed or IncidentStage.Approved)
        {
            throw new LifecycleConflictException($"Cannot start an investigation from {stored.Incident.Stage}.");
        }

        var started = await _workflow.StartAsync(incidentId, cancellationToken).ConfigureAwait(false);
        if (started.IncidentId != incidentId)
        {
            throw new LifecycleConflictException("The workflow returned a different incident.");
        }

        if (started.Stage is InvestigationStage.AwaitingApproval or InvestigationStage.Approved or InvestigationStage.Rejected)
        {
            throw new LifecycleConflictException("The workflow cannot skip to a human decision.");
        }

        var aligned = AlignCorrelation(started, stored.Incident.CorrelationId);
        var target = Map(aligned.Stage);
        LifecycleMoves.WalkTo(stored, target);
        await _incidents.SaveAsync(stored, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        await _investigations.AddAsync(aligned, cancellationToken).ConfigureAwait(false);
        await _notifier.InvestigationChangedAsync(aligned, cancellationToken).ConfigureAwait(false);
        await _notifier.IncidentChangedAsync(stored.Incident, cancellationToken).ConfigureAwait(false);
        return aligned;
    }

    public async Task<InvestigationSnapshot> GetAsync(InvestigationId investigationId, CancellationToken cancellationToken)
    {
        return await _investigations.GetAsync(investigationId, cancellationToken).ConfigureAwait(false)
            ?? throw new ResourceNotFoundException("Investigation was not found.");
    }

    public static IncidentStage Map(InvestigationStage stage) => stage switch
    {
        InvestigationStage.Started => IncidentStage.Investigating,
        InvestigationStage.EvidenceGathered => IncidentStage.Investigating,
        InvestigationStage.RecommendationReady => IncidentStage.RecommendationReady,
        InvestigationStage.Escalated => IncidentStage.Escalated,
        InvestigationStage.Failed => IncidentStage.Escalated,
        InvestigationStage.AwaitingApproval => IncidentStage.AwaitingApproval,
        InvestigationStage.Approved => IncidentStage.Approved,
        InvestigationStage.Rejected => IncidentStage.Rejected,
        _ => throw new LifecycleConflictException($"Investigation stage {stage} is not mapped."),
    };

    private static InvestigationSnapshot AlignCorrelation(InvestigationSnapshot snapshot, string correlationId)
    {
        if (string.Equals(snapshot.CorrelationId, correlationId, StringComparison.Ordinal))
        {
            return snapshot;
        }

        return new InvestigationSnapshot(
            snapshot.Id,
            snapshot.IncidentId,
            snapshot.Stage,
            snapshot.Evidence,
            snapshot.Hypotheses,
            snapshot.EscalationReason,
            correlationId);
    }
}
