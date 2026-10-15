// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Metadata;
using Mps.Domain.Incidents;

namespace Mps.Application.Intake;

public sealed class IntakeSubmission
{
    public IntakeSubmission(
        IncidentChannel channel,
        string toolId,
        string? chamberId,
        string parameter,
        string summary,
        Severity? requestedSeverity,
        Severity? signalSeverity,
        string? correlationId,
        DateTimeOffset? occurredAt,
        string? alertRuleId = null,
        int repeatCount = 0,
        double? confidence = null,
        string? evidenceSufficiency = null)
    {
        Channel = channel;
        ToolId = toolId ?? string.Empty;
        ChamberId = chamberId;
        Parameter = parameter ?? string.Empty;
        Summary = summary ?? string.Empty;
        RequestedSeverity = requestedSeverity;
        SignalSeverity = signalSeverity;
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim();
        OccurredAt = occurredAt;
        AlertRuleId = string.IsNullOrWhiteSpace(alertRuleId) ? null : alertRuleId.Trim();
        RepeatCount = repeatCount < 0 ? 0 : repeatCount;
        Confidence = confidence;
        EvidenceSufficiency = string.IsNullOrWhiteSpace(evidenceSufficiency) ? null : evidenceSufficiency.Trim();
    }

    public IncidentChannel Channel { get; }

    public string ToolId { get; }

    public string? ChamberId { get; }

    public string Parameter { get; }

    public string Summary { get; }

    public Severity? RequestedSeverity { get; }

    public Severity? SignalSeverity { get; }

    public string? CorrelationId { get; }

    public DateTimeOffset? OccurredAt { get; }

    public string? AlertRuleId { get; }

    public int RepeatCount { get; }

    public double? Confidence { get; }

    public string? EvidenceSufficiency { get; }
}

public sealed class IntakeResult
{
    public IntakeResult(
        Incident incident,
        Ticket? ticket,
        bool linkedToExisting,
        bool escalationRequired,
        RuleDecision? decision = null)
    {
        ArgumentNullException.ThrowIfNull(incident);
        Incident = incident;
        Ticket = ticket;
        LinkedToExisting = linkedToExisting;
        EscalationRequired = escalationRequired;
        Decision = decision;
    }

    public Incident Incident { get; }

    public Ticket? Ticket { get; }

    public bool LinkedToExisting { get; }

    public bool EscalationRequired { get; }

    public RuleDecision? Decision { get; }
}
