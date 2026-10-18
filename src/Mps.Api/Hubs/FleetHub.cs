// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.AspNetCore.SignalR;

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
