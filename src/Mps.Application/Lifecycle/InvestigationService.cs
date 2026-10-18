// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Bus;
using Mps.Application.Errors;
using Mps.Application.Metrics;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Application.Workflow;
using Mps.Domain.Events;
using Mps.Domain.Evidence;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;

namespace Mps.Application.Lifecycle;

public interface IInvestigationService
{
    Task<InvestigationSnapshot> StartAsync(IncidentId incidentId, CancellationToken cancellationToken);

    /// <summary>
    /// Starts the next round on the ticket (the evidence loop): the agents run again from evidence gathering with the
    /// user-provided records included, and the evidence gate, grounding and cause checks apply as on the first round.
    /// </summary>
    Task<InvestigationSnapshot> StartRoundAsync(IncidentId incidentId, IReadOnlyList<EvidenceItem> userEvidence, CancellationToken cancellationToken);

    /// <summary>Every round on the ticket, oldest first, including one still gathering evidence.</summary>
    Task<IReadOnlyList<InvestigationSnapshot>> ListRoundsAsync(IncidentId incidentId, CancellationToken cancellationToken);

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
    private readonly InvestigationsInFlight _inFlight;

    public InvestigationService(
        IIncidentLog incidents,
        IInvestigationLog investigations,
        IInvestigationWorkflow workflow,
        IQualityNotifier notifier,
        TimeProvider time,
        IMetricsLog metrics,
        MetricsPolicy metricsPolicy,
        ILifecycleEventSink? events = null,
        ICascadeDirectory? hints = null,
        InvestigationsInFlight? inFlight = null)
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
        _inFlight = inFlight ?? new InvestigationsInFlight();
    }

    public Task<InvestigationSnapshot> StartAsync(IncidentId incidentId, CancellationToken cancellationToken) =>
        StartCoreAsync(incidentId, [], cancellationToken);

    public Task<InvestigationSnapshot> StartRoundAsync(IncidentId incidentId, IReadOnlyList<EvidenceItem> userEvidence, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userEvidence);
        if (userEvidence.Any(item => item.Source != EvidenceSource.UserProvided))
        {
            throw new IntakeValidationException("Only user-provided evidence can start a new round.");
        }

        return StartCoreAsync(incidentId, userEvidence, cancellationToken);
    }

    public async Task<IReadOnlyList<InvestigationSnapshot>> ListRoundsAsync(IncidentId incidentId, CancellationToken cancellationToken)
    {
        _ = await _incidents.GetAsync(incidentId, cancellationToken).ConfigureAwait(false)
            ?? throw new ResourceNotFoundException("Incident was not found.");
        var saved = (await _investigations.ListAsync(cancellationToken).ConfigureAwait(false))
            .Where(item => item.IncidentId == incidentId)
            .OrderBy(item => item.Round)
            .ToList();
        if (_inFlight.ForIncident(incidentId) is { } running && !_inFlight.IsAlreadySaved(running, saved))
        {
            saved.Add(running);
        }

        return saved;
    }

    private async Task<InvestigationSnapshot> StartCoreAsync(IncidentId incidentId, IReadOnlyList<EvidenceItem> userEvidence, CancellationToken requested)
    {
        // A demo reset cancels this run (and its wait for readings) so it neither holds the reset up nor writes into the fresh plant.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(requested, _inFlight.ResetToken);
        var cancellationToken = linked.Token;
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

        // Gathering can take minutes (the agent waits for enough readings). Show a placeholder meanwhile, and answer a
        // second Start with it instead of running a duplicate investigation that would look hung.
        var previous = await _investigations.FindLatestByIncidentAsync(incidentId, cancellationToken).ConfigureAwait(false);
        var round = (previous?.Round ?? 0) + 1;
        if (userEvidence.Count == 0 && previous is { Stage: InvestigationStage.Superseded })
        {
            // A plain Start after a closed round (the next round did not start) still carries what people added.
            userEvidence = InvestigationRounds.CarriedUserEvidence(previous);
        }
        var placeholder = Stamp(
            new InvestigationSnapshot(InvestigationId.New(), incidentId, InvestigationStage.Started, userEvidence, [], null, stored.Incident.CorrelationId)
            {
                Round = round,
            },
            stored.Incident);
        if (!_inFlight.TryBegin(placeholder, out var running))
        {
            return running;
        }

        await _notifier.InvestigationRunningAsync(placeholder, cancellationToken).ConfigureAwait(false);

        InvestigationId? saved = null;
        try
        {
            var result = await RunAsync(incidentId, stored, userEvidence, round, placeholder, cancellationToken).ConfigureAwait(false);
            saved = result.Id;
            return result;
        }
        finally
        {
            _inFlight.End(placeholder, saved);
        }
    }

    private async Task<InvestigationSnapshot> RunAsync(
        IncidentId incidentId,
        StoredIncident stored,
        IReadOnlyList<EvidenceItem> userEvidence,
        int round,
        InvestigationSnapshot placeholder,
        CancellationToken cancellationToken)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        using var activity = QualityDiagnostics.ActivitySource.StartActivity("investigation.start");
        activity?.SetTag("mps.investigation.round", round);
        var started = userEvidence.Count == 0
            ? await _workflow.StartAsync(incidentId, cancellationToken).ConfigureAwait(false)
            : await _workflow.StartRoundAsync(incidentId, userEvidence, cancellationToken).ConfigureAwait(false);
        if (started.IncidentId != incidentId)
        {
            throw new LifecycleConflictException("The workflow returned a different incident.");
        }

        if (started.Stage is InvestigationStage.AwaitingApproval or InvestigationStage.Approved or InvestigationStage.Rejected)
        {
            throw new LifecycleConflictException("The workflow cannot skip to a human decision.");
        }

        var aligned = Stamp(KeepUserEvidence(AlignCorrelation(started, stored.Incident.CorrelationId), userEvidence), stored.Incident);
        aligned = aligned.WithRound(round);
        var opened = await OpenCommandAsync(aligned, stored.Incident, cancellationToken).ConfigureAwait(false);
        using var command = CommandAmbient.Adopt(opened);
        aligned = await StampRecordsAsync(aligned, stored.Incident, opened, cancellationToken).ConfigureAwait(false);
        var target = Map(aligned.Stage);
        LifecycleMoves.WalkTo(stored, target);
        await _incidents.SaveAsync(stored, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        await _investigations.AddAsync(aligned, cancellationToken).ConfigureAwait(false);
        // Drop the placeholder before the notifiers. The list also filters, in case it already read this row.
        _inFlight.MarkSaved(placeholder, aligned.Id);
        await _notifier.InvestigationChangedAsync(aligned, cancellationToken).ConfigureAwait(false);
        await _notifier.IncidentChangedAsync(stored.Incident, cancellationToken).ConfigureAwait(false);
        var elapsed = watch.Elapsed.TotalMilliseconds;
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
        var id = _inFlight.SavedAs(investigationId) ?? investigationId;
        return await _investigations.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? _inFlight.ById(investigationId)
            ?? throw new ResourceNotFoundException("Investigation was not found.");
    }

    public async Task<InvestigationSnapshot> GetLatestByIncidentAsync(IncidentId incidentId, CancellationToken cancellationToken)
    {
        _ = await _incidents.GetAsync(incidentId, cancellationToken).ConfigureAwait(false)
            ?? throw new ResourceNotFoundException("Incident was not found.");
        var latest = await _investigations.FindLatestByIncidentAsync(incidentId, cancellationToken).ConfigureAwait(false);
        if (_inFlight.ForIncident(incidentId) is { } running && (latest is null || !_inFlight.IsAlreadySaved(running, [latest])))
        {
            return running;
        }

        return latest ?? throw new ResourceNotFoundException("Investigation was not found.");
    }

    /// <summary>Saved investigations plus any still gathering, newest first. A saved round is listed once.</summary>
    public async Task<IReadOnlyList<InvestigationSnapshot>> ListAsync(CancellationToken cancellationToken)
    {
        var saved = await _investigations.ListAsync(cancellationToken).ConfigureAwait(false);
        var running = _inFlight.Running;
        if (running.Count == 0)
        {
            return saved;
        }

        var pending = running.Where(placeholder => !_inFlight.IsAlreadySaved(placeholder, saved)).ToArray();
        return pending.Length == 0 ? saved : pending.Concat(saved).ToArray();
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
        InvestigationStage.ClosedByDemoReset => IncidentStage.ClosedByDemoReset,
        InvestigationStage.Superseded => IncidentStage.Investigating,
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
                    EventTypes.InvestigationStep,
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

        var parent = InvestigationParent.EventId
            ?? _hints?.FindStep(snapshot.CorrelationId, "ticket")
            ?? _hints?.FindStep(snapshot.CorrelationId, "alert");
        var eventId = await _events.PublishAsync(
            new LifecycleEvent(
                EventTypes.InvestigationStart,
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

    /// <summary>A person's records stay on the round even when the run ended before reading them (a failed gather).</summary>
    private static InvestigationSnapshot KeepUserEvidence(InvestigationSnapshot snapshot, IReadOnlyList<EvidenceItem> userEvidence)
    {
        var missing = userEvidence.Where(item => snapshot.Evidence.All(existing => existing.Id != item.Id)).ToArray();
        return missing.Length == 0 ? snapshot : snapshot.WithEvidence(snapshot.Evidence.Concat(missing).ToArray());
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
