// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.AspNetCore.SignalR;

using Mps.Api.Http;
using Mps.Application.Notifications;
using Mps.Domain.Capa;
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

    public SignalRQualityNotifier(IHubContext<FleetHub> hub)
    {
        ArgumentNullException.ThrowIfNull(hub);
        _hub = hub;
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

    public Task InvestigationChangedAsync(InvestigationSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return SendAsync("investigationChanged", null, ContractMapper.ToResponse(snapshot), cancellationToken);
    }

    public Task CapaChangedAsync(CapaAction capa, string eventName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capa);
        var name = string.IsNullOrWhiteSpace(eventName) ? "capaChanged" : eventName.Trim();
        return SendAsync(name, null, ContractMapper.ToResponse(capa), cancellationToken);
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
