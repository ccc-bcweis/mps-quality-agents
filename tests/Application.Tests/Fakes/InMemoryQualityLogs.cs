// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Persistence;
using Mps.Application.Writes;
using Mps.Domain.Equipment;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;

namespace Mps.Application.Tests.Fakes;

public sealed class InMemoryAlertLog : IAlertLog
{
    private readonly List<Alert> _alerts = [];

    public IReadOnlyList<Alert> Alerts => _alerts;

    public Task AddAsync(Alert alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);
        _ = cancellationToken;
        _alerts.Add(alert);
        return Task.CompletedTask;
    }

    public Task ClearAsync(AlertId id, DateTimeOffset at, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _alerts.FirstOrDefault(alert => alert.Id == id)?.Clear(at);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Alert>> ListAsync(ToolId? toolId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        IReadOnlyList<Alert> matches = toolId is null
            ? _alerts.ToArray()
            : _alerts.Where(alert => alert.ToolId == toolId.Value).ToArray();
        return Task.FromResult(matches);
    }

    public Task<IReadOnlyList<Alert>> ListBySignalAsync(
        ToolId toolId,
        string parameter,
        string? chamberId,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var normalizedChamber = string.IsNullOrWhiteSpace(chamberId) ? null : chamberId.Trim();
        IReadOnlyList<Alert> matches = _alerts.Where(alert =>
                alert.ToolId == toolId
                && string.Equals(alert.Parameter, parameter, StringComparison.OrdinalIgnoreCase)
                && string.Equals(alert.ChamberId, normalizedChamber, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return Task.FromResult(matches);
    }
}

public sealed class InMemoryIncidentLog : IIncidentLog
{
    private readonly List<StoredIncident> _incidents = [];

    public IReadOnlyList<StoredIncident> Incidents => _incidents;

    public Task AddAsync(StoredIncident incident, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incident);
        _ = cancellationToken;
        _incidents.Add(incident);
        return Task.CompletedTask;
    }

    public Task<StoredIncident?> FindOpenAsync(ToolId toolId, string parameter, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var match = _incidents.FirstOrDefault(stored =>
            stored.Incident.Stage is not (IncidentStage.Closed or IncidentStage.ClosedByDemoReset)
            && stored.Incident.ToolId == toolId
            && string.Equals(stored.Incident.Parameter, parameter, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(match);
    }

    public Task<StoredIncident?> GetAsync(IncidentId id, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(_incidents.FirstOrDefault(stored => stored.Incident.Id == id));
    }

    public Task<IReadOnlyList<StoredIncident>> ListAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<StoredIncident>>(_incidents.ToArray());
    }

    public Task SaveAsync(StoredIncident incident, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incident);
        _ = at;
        _ = cancellationToken;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryTicketingPort : ITicketingPort
{
    private int _next = 1000;

    public List<Ticket> Tickets { get; } = [];

    public Task<Ticket> OpenAsync(Incident incident, string title, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incident);
        _ = cancellationToken;
        _next++;
        var ticket = new Ticket(
            TicketId.New(),
            "TKT-" + _next.ToString(System.Globalization.CultureInfo.InvariantCulture),
            incident.Id,
            incident.ToolId,
            title,
            incident.OpenedAt);
        Tickets.Add(ticket);
        return Task.FromResult(ticket);
    }
}

public sealed class InMemoryFleetCatalog : IFleetCatalog
{
    private readonly List<Tool> _tools = [];

    public void Add(Tool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        _tools.Add(tool);
    }

    public Task<IReadOnlyList<Tool>> ListAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<Tool>>(_tools.ToArray());
    }

    public Task<Tool?> FindAsync(ToolId toolId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(_tools.FirstOrDefault(tool => tool.Id == toolId));
    }
}
