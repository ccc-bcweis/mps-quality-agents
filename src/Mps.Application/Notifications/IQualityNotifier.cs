// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Capa;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;

namespace Mps.Application.Notifications;

public interface IQualityNotifier
{
    Task AlertRaisedAsync(Alert alert, CancellationToken cancellationToken);

    Task TicketOpenedAsync(Ticket ticket, CancellationToken cancellationToken);

    Task IncidentChangedAsync(Incident incident, CancellationToken cancellationToken);

    Task InvestigationChangedAsync(InvestigationSnapshot snapshot, CancellationToken cancellationToken);

    Task CapaChangedAsync(CapaAction capa, string eventName, CancellationToken cancellationToken);

    Task FaultInjectedAsync(Guid faultId, string correlationId, string toolId, CancellationToken cancellationToken);
}

public sealed class NullQualityNotifier : IQualityNotifier
{
    public static NullQualityNotifier Instance { get; } = new();

    public Task AlertRaisedAsync(Alert alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    public Task TicketOpenedAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    public Task IncidentChangedAsync(Incident incident, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incident);
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    public Task InvestigationChangedAsync(InvestigationSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    public Task CapaChangedAsync(CapaAction capa, string eventName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capa);
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    public Task FaultInjectedAsync(Guid faultId, string correlationId, string toolId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.CompletedTask;
    }
}
