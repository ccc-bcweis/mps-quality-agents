// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.AspNetCore.SignalR;

using Mps.Api.Http;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Domain.Capa;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;

namespace Mps.Api.Hubs;

/// <summary>
/// Fleet stream at /hubs/fleet. Events: alertRaised, ticketOpened, incidentChanged, investigationChanged, capaChanged, faultInjected.
/// </summary>
public sealed class FleetHub : Hub
{
    public const string FleetGroup = "fleet";

    public override Task OnConnectedAsync() => Groups.AddToGroupAsync(Context.ConnectionId, FleetGroup);

    public Task SubscribeTool(string toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId))
        {
            throw new HubException("Tool id is required.");
        }

        return Groups.AddToGroupAsync(Context.ConnectionId, ToolGroup(toolId));
    }

    public static string ToolGroup(string toolId) => "tool:" + toolId.Trim();
}

public sealed class SignalRQualityNotifier : IQualityNotifier
{
    private readonly IHubContext<FleetHub> _hub;
    private readonly IInvestigationNarrativeLog _narratives;
    private readonly IIncidentLog _incidents;

    public SignalRQualityNotifier(IHubContext<FleetHub> hub, IInvestigationNarrativeLog narratives, IIncidentLog incidents)
    {
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentNullException.ThrowIfNull(narratives);
        ArgumentNullException.ThrowIfNull(incidents);
        _hub = hub;
        _narratives = narratives;
        _incidents = incidents;
    }

    public Task AlertRaisedAsync(Alert alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);
        return SendAsync("alertRaised", alert.ToolId.ToString(), ContractMapper.ToResponse(alert), cancellationToken);
    }

    public Task TicketOpenedAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        return SendAsync("ticketOpened", ticket.ToolId.ToString(), ContractMapper.ToResponse(ticket), cancellationToken);
    }

    public Task IncidentChangedAsync(Incident incident, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incident);
        return SendAsync("incidentChanged", incident.ToolId.ToString(), ContractMapper.ToResponse(incident), cancellationToken);
    }

    public async Task InvestigationChangedAsync(InvestigationSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var narrative = await _narratives.GetAsync(snapshot.Id, cancellationToken).ConfigureAwait(false);
        var site = await SiteForAsync(snapshot.IncidentId, cancellationToken).ConfigureAwait(false);
        await SendAsync("investigationChanged", null, ContractMapper.ToResponse(snapshot, narrative, site), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task CapaChangedAsync(CapaAction capa, string eventName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capa);
        var name = string.IsNullOrWhiteSpace(eventName) ? "capaChanged" : eventName.Trim();
        var site = await SiteForAsync(capa.IncidentId, cancellationToken).ConfigureAwait(false);
        await SendAsync(name, null, ContractMapper.ToResponse(capa, site), cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> SiteForAsync(IncidentId incidentId, CancellationToken cancellationToken)
    {
        var stored = await _incidents.GetAsync(incidentId, cancellationToken).ConfigureAwait(false);
        return ContractMapper.SiteForTool(stored?.Incident.ToolId.ToString());
    }

    public Task FaultInjectedAsync(Guid faultId, string correlationId, string toolId, CancellationToken cancellationToken) =>
        SendAsync("faultInjected", toolId, new FaultAcceptedResponse(faultId, correlationId), cancellationToken);

    private Task SendAsync(string eventName, string? toolId, object payload, CancellationToken cancellationToken)
    {
        var fleet = _hub.Clients.Group(FleetHub.FleetGroup).SendAsync(eventName, payload, cancellationToken);
        if (string.IsNullOrWhiteSpace(toolId))
        {
            return fleet;
        }

        var tool = _hub.Clients.Group(FleetHub.ToolGroup(toolId)).SendAsync(eventName, payload, cancellationToken);
        return Task.WhenAll(fleet, tool);
    }
}
