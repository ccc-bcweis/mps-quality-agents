// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Evidence;
using Mps.Application.Persistence;
using Mps.Domain.Capa;
using Mps.Domain.Equipment;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;
using Mps.Simulator.Sites;

namespace Mps.Api.Http;

public static class ContractMapper
{
    public static ToolResponse ToResponse(Tool tool) =>
        new(
            tool.Id.ToString(),
            tool.Name,
            tool.Family,
            tool.Model,
            tool.Bay,
            tool.Chambers.Select(chamber => new ChamberResponse(chamber.Id, chamber.Name)).ToArray(),
            string.IsNullOrWhiteSpace(tool.SiteId) ? SiteIds.Northwater : tool.SiteId);

    public static AlertResponse ToResponse(Alert alert) =>
        new(
            alert.Id.Value,
            alert.ToolId.ToString(),
            alert.ChamberId,
            alert.Parameter,
            alert.Severity,
            alert.Rule,
            alert.RaisedAt,
            alert.CorrelationId);

    public static TicketResponse ToResponse(Ticket ticket) =>
        new(ticket.Id.Value, ticket.Number, ticket.IncidentId.Value, ticket.ToolId.ToString(), ticket.Title, ticket.OpenedAt);

    public static IncidentResponse ToResponse(Incident incident) =>
        new(
            incident.Id.Value,
            incident.ToolId.ToString(),
            incident.ChamberId,
            incident.Parameter,
            incident.Channel,
            incident.Severity,
            incident.Summary,
            incident.CorrelationId,
            incident.OpenedAt,
            incident.Stage);

    public static InvestigationResponse ToResponse(InvestigationSnapshot snapshot) =>
        ToResponse(snapshot, InvestigationNarrative.Empty);

    public static InvestigationResponse ToResponse(InvestigationSnapshot snapshot, InvestigationNarrative narrative)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(narrative);
        var notes = narrative.Notes.ToDictionary(note => note.EvidenceId);
        return new InvestigationResponse(
            snapshot.Id.Value,
            snapshot.IncidentId.Value,
            snapshot.Stage,
            snapshot.Evidence.Select(item =>
            {
                notes.TryGetValue(item.Id.Value, out var note);
                return new EvidenceResponse(
                    item.Id.Value,
                    item.Source,
                    item.Title,
                    item.Excerpt,
                    item.ObservedAt,
                    item.ToolId,
                    item.ExternalRef,
                    note?.WhyItMatters,
                    note?.SignalId);
            }).ToArray(),
            snapshot.Hypotheses.Select(item => new HypothesisResponse(
                item.Id.Value,
                item.Category,
                item.Cause,
                item.Confidence,
                item.EvidenceIds.Select(id => id.Value).ToArray(),
                item.CounterEvidence,
                item.Actions)).ToArray(),
            snapshot.EscalationReason,
            snapshot.CorrelationId,
            narrative.Matrix.Select(entry => new EvidenceMatrixResponse(
                entry.Category,
                entry.EvidenceId,
                entry.Stance,
                entry.WhyItMatters)).ToArray());
    }

    public static CapaResponse ToResponse(CapaAction capa) =>
        new(capa.Id.Value, capa.IncidentId.Value, capa.Summary, capa.ApprovalTokenId.Value, capa.ActionHash, capa.Stage, capa.OpenedAt);

    public static string ModeFrom(string? configured) => configured?.Trim().ToLowerInvariant() switch
    {
        "live" or "azure" or "openai" => "live",
        _ => "replay",
    };
}
