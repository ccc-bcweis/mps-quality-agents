// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.EntityFrameworkCore;

using Mps.Api.Persistence;

namespace Mps.Api.Http;

/// <summary>
/// Read-only audit trail. Correlation ids stay on this payload so the harness can show the trace.
/// Operator screens do not render them.
/// </summary>
public static class AuditLog
{
    public static async Task<AuditLogResponse> ReadAsync(QualityDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        var faults = await db.Faults.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var alerts = await db.Alerts.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var tickets = await db.Tickets.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var incidents = await db.Incidents.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var investigations = await db.Investigations.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var approvals = await db.Approvals.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var capas = await db.Capas.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var incidentById = incidents.ToDictionary(row => row.Id);
        var events = new List<AuditStamp>();

        foreach (var fault in faults)
        {
            events.Add(new AuditStamp(
                Utc(fault.AcceptedAt),
                0,
                "fault",
                "Fault accepted on " + fault.ToolId + " for " + fault.Parameter + ".",
                null,
                fault.ToolId,
                fault.CorrelationId,
                fault.Id.ToString("D")));
        }

        foreach (var alert in alerts)
        {
            events.Add(new AuditStamp(
                Utc(alert.RaisedAt),
                1,
                "alert",
                "Alert raised on " + alert.ToolId + " for " + alert.Parameter + ".",
                null,
                alert.ToolId,
                alert.CorrelationId,
                alert.Id.ToString("D")));
        }

        foreach (var incident in incidents)
        {
            events.Add(new AuditStamp(
                Utc(incident.OpenedAt),
                2,
                "incident",
                "Incident opened on " + incident.ToolId + ".",
                null,
                incident.ToolId,
                incident.CorrelationId,
                incident.Id.ToString("D")));
        }

        foreach (var ticket in tickets)
        {
            incidentById.TryGetValue(ticket.IncidentId, out var incident);
            events.Add(new AuditStamp(
                Utc(ticket.OpenedAt),
                3,
                "ticket",
                "Ticket " + ticket.Number + " opened on " + ticket.ToolId + ".",
                null,
                ticket.ToolId,
                incident?.CorrelationId,
                ticket.Id.ToString("D")));
        }

        foreach (var investigation in investigations)
        {
            incidentById.TryGetValue(investigation.IncidentId, out var incident);
            var toolId = incident?.ToolId;
            var where = string.IsNullOrWhiteSpace(toolId) ? "." : " for " + toolId + ".";
            events.Add(new AuditStamp(
                incident is null ? DateTimeOffset.UnixEpoch : Utc(incident.OpenedAt),
                4,
                "investigation",
                "Investigation started" + where,
                null,
                toolId,
                investigation.CorrelationId,
                investigation.Id.ToString("D")));
        }

        foreach (var approval in approvals)
        {
            incidentById.TryGetValue(approval.IncidentId, out var incident);
            var name = string.IsNullOrWhiteSpace(approval.ApproverId) ? "an approver" : approval.ApproverId.Trim();
            var summary = string.Equals(approval.Decision, "approve", StringComparison.OrdinalIgnoreCase)
                ? "Approved by " + name + "."
                : "Decision recorded by " + name + ".";
            events.Add(new AuditStamp(
                Utc(approval.IssuedAt),
                5,
                "approval",
                summary,
                name,
                incident?.ToolId,
                incident?.CorrelationId,
                approval.TokenId.ToString("D")));
        }

        foreach (var capa in capas)
        {
            incidentById.TryGetValue(capa.IncidentId, out var incident);
            var toolId = incident?.ToolId;
            var where = string.IsNullOrWhiteSpace(toolId) ? "." : " for " + toolId + ".";
            events.Add(new AuditStamp(
                Utc(capa.OpenedAt),
                6,
                "capa",
                "CAPA opened" + where,
                null,
                toolId,
                incident?.CorrelationId,
                capa.Id.ToString("D")));
        }

        var ordered = events
            .OrderByDescending(item => item.At)
            .ThenByDescending(item => item.Rank)
            .ToArray();
        var entries = ordered.Select(item => item.ToResponse()).ToArray();
        var traces = ordered
            .Where(item => !string.IsNullOrWhiteSpace(item.CorrelationId))
            .GroupBy(item => item.CorrelationId!, StringComparer.Ordinal)
            .Select(group => new CorrelationTraceResponse(
                group.Key,
                group.OrderBy(item => item.At).ThenBy(item => item.Rank).Select(item => item.ToResponse()).ToArray()))
            .OrderByDescending(trace => trace.Events.Count == 0 ? DateTimeOffset.UnixEpoch : trace.Events[^1].At)
            .ToArray();
        return new AuditLogResponse(entries, traces);
    }

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed record AuditStamp(
        DateTimeOffset At,
        int Rank,
        string Kind,
        string Summary,
        string? Actor,
        string? ToolId,
        string? CorrelationId,
        string? RecordId)
    {
        public AuditEventResponse ToResponse() => new(At, Kind, Summary, Actor, ToolId, CorrelationId, RecordId);
    }
}
