// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

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
        DateTimeOffset? occurredAt)
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
}

public sealed class IntakeResult
{
    public IntakeResult(Incident incident, Ticket? ticket, bool linkedToExisting, bool escalationRequired)
    {
        ArgumentNullException.ThrowIfNull(incident);
        Incident = incident;
        Ticket = ticket;
        LinkedToExisting = linkedToExisting;
        EscalationRequired = escalationRequired;
    }

    public Incident Incident { get; }

    public Ticket? Ticket { get; }

    public bool LinkedToExisting { get; }

    public bool EscalationRequired { get; }
}
