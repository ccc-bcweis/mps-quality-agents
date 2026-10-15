// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Evidence;
using Mps.Application.Persistence;
using Mps.Domain.Capa;
using Mps.Domain.Equipment;
using Mps.Domain.Evidence;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;
using Mps.Simulator.Fleet;
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
                var reading = CompleteReading(item);
                return new EvidenceResponse(
                    item.Id.Value,
                    item.Source,
                    item.Title,
                    item.Excerpt,
                    item.ObservedAt,
                    item.ToolId,
                    item.ExternalRef,
                    note?.WhyItMatters,
                    note?.SignalId,
                    reading.Value,
                    reading.Unit,
                    reading.SpecLow,
                    reading.SpecHigh,
                    reading.ToolName,
                    reading.ChamberName);
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

    /// <summary>
    /// Fills unit and spec from the family catalog when the pack only stored the measured value.
    /// </summary>
    internal static EvidenceReading CompleteReading(EvidenceItem item)
    {
        var reading = item.Reading;
        var unit = reading?.Unit;
        var specLow = reading?.SpecLow;
        var specHigh = reading?.SpecHigh;
        var parameter = ParameterFrom(item);
        if (parameter is not null && FamilyFrom(item.ToolId) is { } family
            && FamilyParameterCatalog.For(family).TryGetValue(parameter, out var limit))
        {
            unit ??= limit.Unit;
            specLow ??= limit.Minimum;
            specHigh ??= limit.Maximum;
        }

        return new EvidenceReading(
            reading?.Value,
            string.IsNullOrWhiteSpace(unit) ? null : unit.Trim(),
            specLow,
            specHigh,
            reading?.ToolName ?? PlainEquipment.ToolName(item.ToolId),
            reading?.ChamberName);
    }

    private static string? ParameterFrom(EvidenceItem item)
    {
        if (item.Source is not (EvidenceSource.Spc or EvidenceSource.Telemetry))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(item.ExternalRef))
        {
            return null;
        }

        var parts = item.ExternalRef.Split(':', 4);
        if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[2]))
        {
            return null;
        }

        return parts[2];
    }

    private static ToolFamily? FamilyFrom(string? toolId)
    {
        var name = PlainEquipment.ToolName(toolId);
        if (name is null || string.IsNullOrWhiteSpace(toolId))
        {
            return null;
        }

        return toolId.Trim()[0] switch
        {
            'D' => ToolFamily.Deposition,
            'E' => ToolFamily.Etch,
            'S' => ToolFamily.Strip,
            'C' => ToolFamily.Clean,
            _ => null,
        };
    }

    public static string ModeFrom(string? configured) => configured?.Trim().ToLowerInvariant() switch
    {
        "live" or "azure" or "openai" => "live",
        _ => "replay",
    };
}
