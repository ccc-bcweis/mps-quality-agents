// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Bus;
using Mps.Application.Errors;
using Mps.Application.Metrics;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Application.Workflow;
using Mps.Domain.Evidence;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;

namespace Mps.Application.Lifecycle;

public interface IInvestigationService
{
    Task<InvestigationSnapshot> StartAsync(IncidentId incidentId, CancellationToken cancellationToken);

    Task<InvestigationSnapshot> GetAsync(InvestigationId investigationId, CancellationToken cancellationToken);

    Task<InvestigationSnapshot> GetLatestByIncidentAsync(IncidentId incidentId, CancellationToken cancellationToken);

    /// <summary>Every investigation, newest first.</summary>
    Task<IReadOnlyList<InvestigationSnapshot>> ListAsync(CancellationToken cancellationToken);
}

public sealed class InvestigationService : IInvestigationService
{
    private readonly IIncidentLog _incidents;
    private readonly IInvestigationLog _investigations;
    private readonly IInvestigationWorkflow _workflow;
    private readonly IQualityNotifier _notifier;
    private readonly TimeProvider _time;
    private readonly IMetricsLog _metrics;
    private readonly MetricsPolicy _metricsPolicy;
    private readonly ILifecycleEventSink? _events;
    private readonly ICascadeDirectory? _hints;

    public InvestigationService(
        IIncidentLog incidents,
        IInvestigationLog investigations,
        IInvestigationWorkflow workflow,
        IQualityNotifier notifier,
        TimeProvider time,
        IMetricsLog metrics,
        MetricsPolicy metricsPolicy,
        ILifecycleEventSink? events = null,
        ICascadeDirectory? hints = null)
    {
        ArgumentNullException.ThrowIfNull(incidents);
        ArgumentNullException.ThrowIfNull(investigations);
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(metricsPolicy);
        _incidents = incidents;
        _investigations = investigations;
        _workflow = workflow;
        _notifier = notifier;
        _time = time;
        _metrics = metrics;
        _metricsPolicy = metricsPolicy;
        _events = events;
        _hints = hints;
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

        if (stored.Incident.Stage is IncidentStage.Closed or IncidentStage.Approved or IncidentStage.ClosedByDemoReset)
        {
            throw new LifecycleConflictException($"Cannot start an investigation from {stored.Incident.Stage}.");
        }

        var startedAt = _time.GetUtcNow();
        using var activity = QualityDiagnostics.ActivitySource.StartActivity("investigation.start");
        var started = await _workflow.StartAsync(incidentId, cancellationToken).ConfigureAwait(false);
        if (started.IncidentId != incidentId)
        {
            throw new LifecycleConflictException("The workflow returned a different incident.");
        }

        if (started.Stage is InvestigationStage.AwaitingApproval or InvestigationStage.Approved or InvestigationStage.Rejected)
        {
            throw new LifecycleConflictException("The workflow cannot skip to a human decision.");
        }

        var aligned = Stamp(AlignCorrelation(started, stored.Incident.CorrelationId), stored.Incident);
        var opened = await OpenCommandAsync(aligned, stored.Incident, cancellationToken).ConfigureAwait(false);
        using var command = CommandAmbient.Adopt(opened);
        aligned = await StampRecordsAsync(aligned, stored.Incident, opened, cancellationToken).ConfigureAwait(false);
        var target = Map(aligned.Stage);
        LifecycleMoves.WalkTo(stored, target);
        await _incidents.SaveAsync(stored, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        await _investigations.AddAsync(aligned, cancellationToken).ConfigureAwait(false);
        await _notifier.InvestigationChangedAsync(aligned, cancellationToken).ConfigureAwait(false);
        await _notifier.IncidentChangedAsync(stored.Incident, cancellationToken).ConfigureAwait(false);
        var elapsed = (_time.GetUtcNow() - startedAt).TotalMilliseconds;
        await _metrics.RecordAsync(
            new InvestigationObservation(
                aligned.Id.Value,
                _time.GetUtcNow(),
                false,
                false,
                GroundingScore.For(aligned.Evidence, aligned.Hypotheses),
                1,
                aligned.Stage == InvestigationStage.Failed ? 1 : 0,
                elapsed < 0d ? 0d : elapsed,
                _metricsPolicy.CostPerInvestigationUsd),
            cancellationToken).ConfigureAwait(false);
        return aligned;
    }

    public async Task<InvestigationSnapshot> GetAsync(InvestigationId investigationId, CancellationToken cancellationToken)
    {
        return await _investigations.GetAsync(investigationId, cancellationToken).ConfigureAwait(false)
            ?? throw new ResourceNotFoundException("Investigation was not found.");
    }

    public async Task<InvestigationSnapshot> GetLatestByIncidentAsync(IncidentId incidentId, CancellationToken cancellationToken)
    {
        _ = await _incidents.GetAsync(incidentId, cancellationToken).ConfigureAwait(false)
            ?? throw new ResourceNotFoundException("Incident was not found.");
        return await _investigations.FindLatestByIncidentAsync(incidentId, cancellationToken).ConfigureAwait(false)
            ?? throw new ResourceNotFoundException("Investigation was not found.");
    }

    public Task<IReadOnlyList<InvestigationSnapshot>> ListAsync(CancellationToken cancellationToken) =>
        _investigations.ListAsync(cancellationToken);

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
        InvestigationStage.ClosedByDemoReset => IncidentStage.ClosedByDemoReset,
        _ => throw new LifecycleConflictException($"Investigation stage {stage} is not mapped."),
    };

    private InvestigationSnapshot Stamp(InvestigationSnapshot snapshot, Incident incident)
    {
        var reading = _hints?.ReadingEventId(incident.ToolId.ToString(), incident.Parameter);
        if (reading is null)
        {
            return snapshot;
        }

        var changed = false;
        var evidence = new EvidenceItem[snapshot.Evidence.Count];
        for (var index = 0; index < snapshot.Evidence.Count; index++)
        {
            var item = snapshot.Evidence[index];
            if (item.Source is EvidenceSource.Spc or EvidenceSource.Telemetry && item.SourceEventIds.Count == 0)
            {
                evidence[index] = item.WithSourceEventIds([reading]);
                changed = true;
            }
            else
            {
                evidence[index] = item;
            }
        }

        return changed ? snapshot.WithEvidence(evidence) : snapshot;
    }

    /// <summary>
    /// Enterprise records (change orders, calibrations, lots, maintenance) are read by the investigation, not streamed.
    /// Each one is recorded on the bus as an evidence step caused by the investigation start, so its trace reaches the root.
    /// </summary>
    private async Task<InvestigationSnapshot> StampRecordsAsync(
        InvestigationSnapshot snapshot,
        Incident incident,
        CommandAmbient.Opened? opened,
        CancellationToken cancellationToken)
    {
        var cause = opened?.EventId ?? _hints?.FindStep(snapshot.CorrelationId, "ticket");
        if (_events is null || cause is null)
        {
            return snapshot;
        }

        var changed = false;
        var evidence = new EvidenceItem[snapshot.Evidence.Count];
        for (var index = 0; index < snapshot.Evidence.Count; index++)
        {
            var item = snapshot.Evidence[index];
            if (item.SourceEventIds.Count > 0)
            {
                evidence[index] = item;
                continue;
            }

            var eventId = await _events.PublishAsync(
                new LifecycleEvent(
                    LifecycleTypes.InvestigationStep,
                    snapshot.CorrelationId,
                    cause,
                    cause,
                    "investigation",
                    null,
                    item.ToolId ?? incident.ToolId.ToString(),
                    incident.ChamberId,
                    _time.GetUtcNow(),
                    new { kind = "evidence", source = item.Source.ToString(), title = item.Title, record = item.ExternalRef }),
                cancellationToken).ConfigureAwait(false);
            evidence[index] = eventId is null ? item : item.WithSourceEventIds([eventId]);
            changed |= eventId is not null;
        }

        return changed ? snapshot.WithEvidence(evidence) : snapshot;
    }

    private async ValueTask<CommandAmbient.Opened?> OpenCommandAsync(
        InvestigationSnapshot snapshot,
        Incident incident,
        CancellationToken cancellationToken)
    {
        if (_events is null || CommandAmbient.EventId is not null)
        {
            return null;
        }

        var parent = _hints?.FindStep(snapshot.CorrelationId, "ticket") ?? _hints?.FindStep(snapshot.CorrelationId, "alert");
        var eventId = await _events.PublishAsync(
            new LifecycleEvent(
                LifecycleTypes.InvestigationStart,
                snapshot.CorrelationId,
                parent,
                parent,
                "investigation",
                null,
                incident.ToolId.ToString(),
                incident.ChamberId,
                _time.GetUtcNow(),
                new { incidentId = incident.Id.Value, investigationId = snapshot.Id.Value }),
            cancellationToken).ConfigureAwait(false);
        return new CommandAmbient.Opened(snapshot.CorrelationId, eventId);
    }

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
