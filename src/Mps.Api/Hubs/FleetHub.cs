// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.AspNetCore.SignalR;

using Mps.Api.Http;
using Mps.Application.Bus;
using Mps.Application.Evidence;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Domain.Capa;
using Mps.Domain.Evidence;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;

namespace Mps.Api.Hubs;

/// <summary>
/// Fleet stream at /hubs/fleet. Every connection gets the record events (alertRaised, alertCleared, ticketOpened,
/// ticketChanged, incidentChanged, investigationChanged, capaChanged, capaAdvanced, faultInjected), the plant clock,
/// reset progress and resync. Telemetry goes only to pages that subscribe: toolLive to a tool, fleetLive to a plant.
/// </summary>
public sealed class FleetHub : Hub
{
    public const string FleetGroup = "fleet";

    private readonly LiveSubscriptions _subscriptions;

    public FleetHub(LiveSubscriptions subscriptions)
    {
        ArgumentNullException.ThrowIfNull(subscriptions);
        _subscriptions = subscriptions;
    }

    public override Task OnConnectedAsync() => Groups.AddToGroupAsync(Context.ConnectionId, FleetGroup);

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _subscriptions.Drop(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public Task SubscribeTool(string toolId)
    {
        var group = ToolGroup(Required(toolId, "Tool id"));
        _subscriptions.Add(Context.ConnectionId, group);
        return Groups.AddToGroupAsync(Context.ConnectionId, group);
    }

    public Task UnsubscribeTool(string toolId)
    {
        var group = ToolGroup(Required(toolId, "Tool id"));
        _subscriptions.Remove(Context.ConnectionId, group);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
    }

    /// <summary>Plant live tiles; <see cref="LiveSubscriptions.AllSites"/> watches every plant.</summary>
    public Task SubscribeSite(string siteId)
    {
        var group = SiteGroup(Required(siteId, "Plant id"));
        _subscriptions.Add(Context.ConnectionId, group);
        return Groups.AddToGroupAsync(Context.ConnectionId, group);
    }

    public Task UnsubscribeSite(string siteId)
    {
        var group = SiteGroup(Required(siteId, "Plant id"));
        _subscriptions.Remove(Context.ConnectionId, group);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
    }

    public static string ToolGroup(string toolId) => LiveSubscriptions.ToolKey(toolId);

    public static string SiteGroup(string siteId) => LiveSubscriptions.SiteKey(siteId);

    private static string Required(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new HubException(name + " is required.") : value;
}

public sealed class SignalRQualityNotifier : IQualityNotifier
{
    private readonly ILifecycleEventSink _events;
    private readonly ICascadeDirectory _cascade;
    private readonly IInvestigationNarrativeLog _narratives;
    private readonly IIncidentLog _incidents;
    private readonly ITicketLog _tickets;
    private readonly IHubContext<FleetHub> _hub;

    public SignalRQualityNotifier(
        ILifecycleEventSink events,
        ICascadeDirectory cascade,
        IInvestigationNarrativeLog narratives,
        IIncidentLog incidents,
        ITicketLog tickets,
        IHubContext<FleetHub> hub)
    {
        ArgumentNullException.ThrowIfNull(tickets);
        ArgumentNullException.ThrowIfNull(hub);
        _tickets = tickets;
        _hub = hub;
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(cascade);
        ArgumentNullException.ThrowIfNull(narratives);
        ArgumentNullException.ThrowIfNull(incidents);
        _events = events;
        _cascade = cascade;
        _narratives = narratives;
        _incidents = incidents;
    }

    public Task AlertRaisedAsync(Alert alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);
        var toolId = alert.ToolId.ToString();
        var reading = _cascade.ReadingEventId(toolId, alert.Parameter);
        var parent = reading
            ?? _cascade.FaultEventId(toolId, alert.Parameter)
            ?? CommandAmbient.EventId;
        return PublishAsync(
            LifecycleTypes.AlertRaised,
            alert.CorrelationId,
            parent,
            "spc",
            toolId,
            alert.ChamberId,
            alert.RaisedAt,
            new
            {
                signal = "alertRaised",
                toolId,
                parameter = alert.Parameter,
                rule = alert.Rule,
                body = ContractMapper.ToResponse(alert),
            },
            "alert",
            cancellationToken);
    }

    public async Task TicketOpenedAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        var stored = await _incidents.GetAsync(ticket.IncidentId, cancellationToken).ConfigureAwait(false);
        var correlation = stored?.Incident.CorrelationId
            ?? CommandAmbient.CorrelationId
            ?? ticket.Id.ToString();
        var parent = _cascade.FindStep(correlation, "rule")
            ?? _cascade.FindStep(correlation, "alert")
            ?? CommandAmbient.EventId;
        await PublishAsync(
            LifecycleTypes.TicketCreated,
            correlation,
            parent,
            "intake",
            ticket.ToolId.ToString(),
            stored?.Incident.ChamberId,
            ticket.OpenedAt,
            new
            {
                signal = "ticketOpened",
                toolId = ticket.ToolId.ToString(),
                body = ContractMapper.ToResponse(ticket),
            },
            "ticket",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task IncidentChangedAsync(Incident incident, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incident);
        var parent = _cascade.FindStep(incident.CorrelationId, "approval")
            ?? _cascade.FindStep(incident.CorrelationId, "recommendation")
            ?? _cascade.FindStep(incident.CorrelationId, "investigation")
            ?? _cascade.FindStep(incident.CorrelationId, "ticket")
            ?? _cascade.FindStep(incident.CorrelationId, "alert")
            ?? CommandAmbient.EventId;
        // The ticket's open/closed status follows its incident, so the changed ticket rides along for the Tickets list.
        var tickets = await _tickets.ListAsync(cancellationToken).ConfigureAwait(false);
        var related = tickets
            .Where(ticket => ticket.IncidentId == incident.Id)
            .Select(ticket => new { signal = "ticketChanged", body = ContractMapper.ToResponse(ticket, incident.Stage) })
            .ToArray();
        await PublishAsync(
            LifecycleTypes.IncidentChanged,
            incident.CorrelationId,
            parent,
            "intake",
            incident.ToolId.ToString(),
            incident.ChamberId,
            incident.OpenedAt,
            new
            {
                signal = "incidentChanged",
                toolId = incident.ToolId.ToString(),
                body = ContractMapper.ToResponse(incident),
                related,
            },
            "incident",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task InvestigationChangedAsync(InvestigationSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var narrative = await _narratives.GetAsync(snapshot.Id, cancellationToken).ConfigureAwait(false);
        var stored = await _incidents.GetAsync(snapshot.IncidentId, cancellationToken).ConfigureAwait(false);
        var toolId = stored?.Incident.ToolId.ToString();
        var site = ContractMapper.SiteForTool(toolId);
        // Per investigation: one scenario can open several (one per tool), and each lists its own steps.
        if (_cascade.FindStep(snapshot.CorrelationId, StepKey(snapshot)) is null)
        {
            await PublishStepsAsync(snapshot, stored?.Incident, cancellationToken).ConfigureAwait(false);
        }

        await PublishAsync(
            LifecycleTypes.InvestigationChanged,
            snapshot.CorrelationId,
            _cascade.FindStep(snapshot.CorrelationId, "investigation") ?? CommandAmbient.EventId,
            "investigation",
            toolId,
            stored?.Incident.ChamberId,
            DateTimeOffset.UtcNow,
            new
            {
                signal = "investigationChanged",
                toolId,
                body = ContractMapper.ToResponse(snapshot, narrative, site),
            },
            null,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task InvestigationRunningAsync(InvestigationSnapshot placeholder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(placeholder);
        var stored = await _incidents.GetAsync(placeholder.IncidentId, cancellationToken).ConfigureAwait(false);
        var site = ContractMapper.SiteForTool(stored?.Incident.ToolId.ToString());
        await _hub.Clients.Group(FleetHub.FleetGroup)
            .SendAsync("investigationChanged", ContractMapper.ToResponse(placeholder, InvestigationNarrative.Empty, site), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task CapaChangedAsync(CapaAction capa, string eventName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capa);
        var name = string.IsNullOrWhiteSpace(eventName) ? "capaChanged" : eventName.Trim();
        var stored = await _incidents.GetAsync(capa.IncidentId, cancellationToken).ConfigureAwait(false);
        var correlation = stored?.Incident.CorrelationId ?? capa.Id.ToString();
        var toolId = stored?.Incident.ToolId.ToString();
        var site = ContractMapper.SiteForTool(toolId);
        var created = !string.Equals(name, "capaAdvanced", StringComparison.Ordinal);
        var parent = created
            ? _cascade.FindStep(correlation, "approval") ?? CommandAmbient.EventId
            : CommandAmbient.EventId ?? _cascade.FindStep(correlation, "capa");
        await PublishAsync(
            created ? LifecycleTypes.CapaCreated : LifecycleTypes.CapaAdvanced,
            correlation,
            parent,
            "capa",
            toolId,
            stored?.Incident.ChamberId,
            capa.OpenedAt,
            new
            {
                signal = name,
                body = ContractMapper.ToResponse(capa, site, toolId),
            },
            created ? "capa" : null,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task FaultInjectedAsync(Guid faultId, string correlationId, string toolId, CancellationToken cancellationToken)
    {
        if (_cascade.FindStep(correlationId, "fault") is not null)
        {
            return;
        }

        await PublishAsync(
            LifecycleTypes.FaultInject,
            correlationId,
            CommandAmbient.EventId,
            "injector",
            toolId,
            null,
            DateTimeOffset.UtcNow,
            new
            {
                signal = "faultInjected",
                toolId,
                body = new FaultAcceptedResponse(faultId, correlationId),
            },
            "fault",
            cancellationToken).ConfigureAwait(false);
    }

    private static string StepKey(InvestigationSnapshot snapshot) => "investigation:" + snapshot.Id.Value.ToString("N");

    private async Task PublishStepsAsync(InvestigationSnapshot snapshot, Incident? incident, CancellationToken cancellationToken)
    {
        var parent = CommandAmbient.EventId
            ?? _cascade.FindStep(snapshot.CorrelationId, "ticket")
            ?? _cascade.FindStep(snapshot.CorrelationId, "alert");
        var started = await _events.PublishAsync(
            new LifecycleEvent(
                LifecycleTypes.InvestigationStep,
                snapshot.CorrelationId,
                parent,
                parent,
                "investigation",
                null,
                incident?.ToolId.ToString(),
                incident?.ChamberId,
                DateTimeOffset.UtcNow,
                new { kind = "started", investigationId = snapshot.Id.Value, stage = snapshot.Stage.ToString() }),
            cancellationToken).ConfigureAwait(false);
        _cascade.RememberStep(snapshot.CorrelationId, "investigation", started);
        _cascade.RememberStep(snapshot.CorrelationId, StepKey(snapshot), started);
        string? lastEvidence = null;
        foreach (var item in snapshot.Evidence)
        {
            var source = item.SourceEventIds.Count == 0 ? null : item.SourceEventIds[0];
            var causation = source ?? started;
            lastEvidence = await _events.PublishAsync(
                new LifecycleEvent(
                    LifecycleTypes.InvestigationStep,
                    snapshot.CorrelationId,
                    causation,
                    started,
                    "investigation",
                    null,
                    item.ToolId ?? incident?.ToolId.ToString(),
                    incident?.ChamberId,
                    item.ObservedAt,
                    new
                    {
                        kind = "evidence",
                        evidenceId = item.Id.Value,
                        title = item.Title,
                        source = item.Source.ToString(),
                        sourceEventIds = item.SourceEventIds,
                        value = item.Reading?.Value,
                        low = item.Reading?.SpecLow,
                        high = item.Reading?.SpecHigh,
                    }),
                cancellationToken).ConfigureAwait(false);
        }

        if (HasRecommendation(snapshot.Stage))
        {
            var recommendation = await _events.PublishAsync(
                new LifecycleEvent(
                    LifecycleTypes.InvestigationStep,
                    snapshot.CorrelationId,
                    lastEvidence ?? started,
                    lastEvidence ?? started,
                    "investigation",
                    null,
                    incident?.ToolId.ToString(),
                    incident?.ChamberId,
                    DateTimeOffset.UtcNow,
                    new { kind = "recommendation", investigationId = snapshot.Id.Value }),
                cancellationToken).ConfigureAwait(false);
            _cascade.RememberStep(snapshot.CorrelationId, "recommendation", recommendation);
        }
    }

    private async Task PublishAsync(
        string type,
        string correlationId,
        string? parent,
        string source,
        string? toolId,
        string? chamberId,
        DateTimeOffset occurredAt,
        object payload,
        string? step,
        CancellationToken cancellationToken)
    {
        var eventId = await _events.PublishAsync(
            new LifecycleEvent(type, correlationId, parent, parent, source, null, toolId, chamberId, occurredAt, payload),
            cancellationToken).ConfigureAwait(false);
        if (step is not null)
        {
            _cascade.RememberStep(correlationId, step, eventId);
        }
    }

    private static bool HasRecommendation(InvestigationStage stage) => stage is
        InvestigationStage.RecommendationReady
        or InvestigationStage.Escalated
        or InvestigationStage.AwaitingApproval
        or InvestigationStage.Approved
        or InvestigationStage.Rejected
        or InvestigationStage.Superseded
        or InvestigationStage.Failed;
}
