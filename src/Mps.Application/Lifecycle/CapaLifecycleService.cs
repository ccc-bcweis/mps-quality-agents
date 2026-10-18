// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Bus;
using Mps.Application.Errors;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Domain.Capa;
using Mps.Domain.Events;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Lifecycle;

namespace Mps.Application.Lifecycle;

public interface ICapaLifecycleService
{
    Task<StoredCapa> GetAsync(CapaId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredCapa>> ListAsync(CancellationToken cancellationToken);

    Task<StoredCapa> AdvanceAsync(CapaId id, CapaStage stage, CancellationToken cancellationToken);
}

public sealed class CapaLifecycleService : ICapaLifecycleService
{
    private readonly ICapaLog _capas;
    private readonly IQualityNotifier _notifier;
    private readonly IIncidentLog? _incidents;
    private readonly IAlertLog? _alerts;
    private readonly IPlantFaultControl? _plant;
    private readonly ILifecycleEventSink? _events;
    private readonly TimeProvider _time;

    public CapaLifecycleService(
        ICapaLog capas,
        IQualityNotifier notifier,
        IIncidentLog? incidents = null,
        IAlertLog? alerts = null,
        IPlantFaultControl? plant = null,
        ILifecycleEventSink? events = null,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(capas);
        ArgumentNullException.ThrowIfNull(notifier);
        _capas = capas;
        _notifier = notifier;
        _incidents = incidents;
        _alerts = alerts;
        _plant = plant;
        _events = events;
        _time = time ?? TimeProvider.System;
    }

    public async Task<StoredCapa> GetAsync(CapaId id, CancellationToken cancellationToken)
    {
        return await _capas.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new ResourceNotFoundException("CAPA was not found.");
    }

    public Task<IReadOnlyList<StoredCapa>> ListAsync(CancellationToken cancellationToken) =>
        _capas.ListAsync(cancellationToken);

    public async Task<StoredCapa> AdvanceAsync(CapaId id, CapaStage stage, CancellationToken cancellationToken)
    {
        var stored = await GetAsync(id, cancellationToken).ConfigureAwait(false);
        var incident = _incidents is null ? null : await _incidents.GetAsync(stored.Capa.IncidentId, cancellationToken).ConfigureAwait(false);
        if (stage == CapaStage.Verification && incident is not null)
        {
            await RequireInControlAsync(incident.Incident, cancellationToken).ConfigureAwait(false);
        }

        LifecycleMoves.MoveOne(stored.Capa, stage);
        await _capas.UpdateAsync(stored.Capa, cancellationToken).ConfigureAwait(false);
        await _notifier.CapaChangedAsync(stored.Capa, "capaAdvanced", cancellationToken).ConfigureAwait(false);
        if (incident is null)
        {
            return stored;
        }

        if (stage == CapaStage.Containment)
        {
            await ContainAsync(incident.Incident, stored.Capa.Id, cancellationToken).ConfigureAwait(false);
        }
        else if (stage == CapaStage.Closed)
        {
            await CloseIncidentAsync(incident, cancellationToken).ConfigureAwait(false);
        }

        return stored;
    }

    /// <summary>Containment removes the faults on the incident's tool, so its readings settle back to nominal.</summary>
    private async Task ContainAsync(Incident incident, CapaId capaId, CancellationToken cancellationToken)
    {
        if (_plant is null)
        {
            return;
        }

        var toolId = incident.ToolId.ToString();
        var removed = _plant.ClearTool(toolId);
        if (_events is not null)
        {
            await _events.PublishAsync(
                new LifecycleEvent(
                    EventTypes.FaultCleared,
                    incident.CorrelationId,
                    null,
                    null,
                    "capa",
                    null,
                    toolId,
                    incident.ChamberId,
                    _time.GetUtcNow(),
                    new { capaId = capaId.Value, toolId, removed, reason = "CAPA moved to Containment" }),
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Verification needs the incident's signal back in control: no active alert on that tool and parameter.</summary>
    private async Task RequireInControlAsync(Incident incident, CancellationToken cancellationToken)
    {
        if (_alerts is null)
        {
            return;
        }

        var alerts = await _alerts.ListBySignalAsync(incident.ToolId, incident.Parameter, incident.ChamberId, cancellationToken).ConfigureAwait(false);
        var active = alerts.Count(alert => alert.IsActive);
        if (active > 0)
        {
            throw new LifecycleConflictException(
                $"Readings on {incident.ToolId} {Mps.Application.Metadata.ParameterNames.Display(incident.Parameter)} are not back in control yet. {active} alert(s) still active; an alert clears after {Mps.Application.Spc.SpcRuleSettings.Shared.Clearing.Readings} in-control readings in a row.");
        }
    }

    /// <summary>Closing the CAPA closes the incident and its ticket. The investigation keeps its Approved verdict.</summary>
    private async Task CloseIncidentAsync(StoredIncident stored, CancellationToken cancellationToken)
    {
        if (_incidents is null || !IncidentLifecycle.CanTransition(stored.Incident.Stage, IncidentStage.Closed))
        {
            return;
        }

        stored.Incident.Advance(IncidentStage.Closed);
        await _incidents.SaveAsync(stored, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        await _notifier.IncidentChangedAsync(stored.Incident, cancellationToken).ConfigureAwait(false);
    }
}
