// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Persistence;
using Mps.Application.Evidence;
using Mps.Application.Persistence;
using Mps.Domain.Capa;
using Mps.Domain.Equipment;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;
using Mps.Domain.Lifecycle;
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
            string.IsNullOrWhiteSpace(tool.SiteId) ? SiteIds.Default : tool.SiteId);

    public static AlertResponse ToResponse(Alert alert) =>
        new(
            alert.Id.Value,
            alert.ToolId.ToString(),
            alert.ChamberId,
            alert.Parameter,
            alert.Severity,
            alert.Rule,
            alert.RaisedAt,
            alert.CorrelationId,
            SiteForTool(alert.ToolId.ToString()),
            alert.Status,
            alert.ClearedAt,
            alert.LastSeenAt,
            alert.Occurrences);

    public static TicketResponse ToResponse(Ticket ticket) => ToResponse(ticket, null);

    public static TicketResponse ToResponse(Ticket ticket, IncidentStage? incidentStage) =>
        new(ticket.Id.Value, ticket.Number, ticket.IncidentId.Value, ticket.ToolId.ToString(), ticket.Title, ticket.OpenedAt, SiteForTool(ticket.ToolId.ToString()), TicketStatus.For(incidentStage));

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
            incident.Stage,
            SiteForTool(incident.ToolId.ToString()),
            incident.Stage == IncidentStage.ClosedByDemoReset ? DemoReset.StatusLabel : null);

    public static InvestigationResponse ToResponse(InvestigationSnapshot snapshot) =>
        ToResponse(snapshot, InvestigationNarrative.Empty, string.Empty);

    public static InvestigationResponse ToResponse(InvestigationSnapshot snapshot, InvestigationNarrative narrative) =>
        ToResponse(snapshot, narrative, string.Empty);

    public static InvestigationResponse ToResponse(InvestigationSnapshot snapshot, InvestigationNarrative narrative, string siteId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(narrative);
        var notes = narrative.Notes.ToDictionary(note => note.EvidenceId);
        var recordRuledOut = RecordRuledOutIds(snapshot, narrative);
        return new InvestigationResponse(
            snapshot.Id.Value,
            snapshot.IncidentId.Value,
            snapshot.Stage,
            snapshot.Evidence.Select(item =>
            {
                notes.TryGetValue(item.Id.Value, out var note);
                var reading = CompleteReading(item);
                var (controlLow, controlHigh) = reading.ControlLimits(Mps.Application.Spc.SpcRuleSettings.Shared.ControlLimitSigma);
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
                    reading.ChamberName,
                    item.SourceEventIds.Count == 0 ? null : item.SourceEventIds,
                    controlLow,
                    controlHigh,
                    reading.Center,
                    LimitState.Of(reading.Value, reading.SpecLow, reading.SpecHigh, controlLow, controlHigh, reading.Center is not null),
                    item.SuppliedBy)
                {
                    Status = EvidenceStatus(item, note, recordRuledOut),
                };
            }).ToArray(),
            snapshot.Hypotheses.Select(item => new HypothesisResponse(
                item.Id.Value,
                item.Category,
                item.Cause,
                item.Confidence,
                item.EvidenceIds.Select(id => id.Value).ToArray(),
                item.CounterEvidence,
                item.Actions,
                item.RuledOut.Select(entry => new RuledOutItemResponse(entry.Id.Value, entry.Reason, entry.Source, entry.Class)).ToArray())).ToArray(),
            snapshot.EscalationReason,
            snapshot.CorrelationId,
            narrative.Matrix.Select(entry => new EvidenceMatrixResponse(
                entry.Category,
                entry.EvidenceId,
                entry.Stance,
                entry.WhyItMatters)).ToArray(),
            SiteForTool(siteId),
            snapshot.Stage == InvestigationStage.ClosedByDemoReset ? DemoReset.StatusLabel : null)
        {
            Decision = snapshot.Decision is { } decision
                ? new DecisionRecordResponse(decision.Kind, decision.Note, decision.DecidedBy, decision.DecidedAt)
                : null,
            Round = snapshot.Round,
            MaxRounds = Mps.Application.Decisions.EvidenceRoundSettings.Shared.MaxRounds,
            RejectedAnswer = snapshot.Stage == InvestigationStage.Escalated && narrative.Rejected is { } rejected
                ? new RejectedAnswerResponse(
                    rejected.Cause,
                    rejected.Category,
                    rejected.EvidenceIds,
                    rejected.RuledOutIds.Select(item => new RejectedRuledOutResponse(item.EvidenceId, item.Reason)).ToArray())
                : null,
        };
    }

    public static CapaResponse ToResponse(CapaAction capa, string siteId, string? toolId = null) =>
        new(capa.Id.Value, capa.IncidentId.Value, capa.Summary, capa.ApprovalTokenId.Value, capa.ActionHash, capa.Stage, capa.OpenedAt, SiteForTool(siteId), ToolId: string.IsNullOrWhiteSpace(toolId) ? null : toolId);

    /// <summary>Plant id for a tool, or the site id itself when the caller already resolved it.</summary>
    public static string SiteForTool(string? toolOrSiteId)
    {
        if (string.IsNullOrWhiteSpace(toolOrSiteId))
        {
            return SiteIds.Default;
        }

        var value = toolOrSiteId.Trim();
        if (SiteIds.IsKnown(value))
        {
            return value;
        }

        return SiteIds.ForTool(value);
    }

    public static CapaResponse WithApproval(CapaResponse capa, ApprovalRow? approval)
    {
        if (approval is null || string.IsNullOrWhiteSpace(approval.ApproverId))
        {
            return capa;
        }

        return capa with
        {
            ApprovedBy = approval.ApproverId.Trim(),
            Note = string.IsNullOrWhiteSpace(approval.Comment) ? null : approval.Comment.Trim(),
            ApprovedAt = new DateTimeOffset(DateTime.SpecifyKind(approval.IssuedAt, DateTimeKind.Utc)),
        };
    }

    /// <summary>
    /// Fills only the unit from the family catalog when the pack did not store one. Spec and control limits come from
    /// the recipe step window the reading was judged against, stored on the pack; a family envelope is never shown as a spec.
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
        }

        return new EvidenceReading(
            reading?.Value,
            string.IsNullOrWhiteSpace(unit) ? null : unit.Trim(),
            specLow,
            specHigh,
            reading?.ToolName ?? PlainEquipment.ToolName(item.ToolId),
            reading?.ChamberName,
            reading?.Center,
            reading?.Sigma);
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

    private static HashSet<Guid> RecordRuledOutIds(InvestigationSnapshot snapshot, InvestigationNarrative narrative)
    {
        var ids = new HashSet<Guid>();
        foreach (var hypothesis in snapshot.Hypotheses)
        {
            foreach (var entry in hypothesis.RuledOut)
            {
                if (entry.Source == RuledOutSource.Record)
                {
                    ids.Add(entry.Id.Value);
                }
            }
        }

        foreach (var entry in narrative.RecordRuledOut)
        {
            ids.Add(entry.Id.Value);
        }

        return ids;
    }

    /// <summary>A signal or ticket is context even when a record rule also names it. Agent rulings do not change status.</summary>
    private static string EvidenceStatus(EvidenceItem item, EvidenceNote? note, HashSet<Guid> recordRuledOut)
    {
        if (!string.IsNullOrWhiteSpace(note?.SignalId)
            || item.Source is EvidenceSource.Spc or EvidenceSource.Telemetry or EvidenceSource.Ticketing
            || (item.Source == EvidenceSource.ManualMeasurement && item.Reading?.Value is not null))
        {
            return EvidenceItemStatus.Context;
        }

        return recordRuledOut.Contains(item.Id.Value) ? EvidenceItemStatus.RuledOut : EvidenceItemStatus.Candidate;
    }

    /// <summary>The metrics contract's run mode: live when a real model answers, replay for offline saved answers.</summary>
    public static string ModeFrom(Mps.Api.Live.ModelMode mode) => mode is Mps.Api.Live.ModelMode.Azure or Mps.Api.Live.ModelMode.OpenAI
        ? "live"
        : "replay";
}
