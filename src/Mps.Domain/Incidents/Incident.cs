// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain;
using Mps.Domain.Identity;
using Mps.Domain.Lifecycle;

namespace Mps.Domain.Incidents;

public enum IncidentChannel
{
    IotSpcAlert,
    OperatorReport,
    QmsNonconformance,
    SupplierQualityNotice,
    FaultInjector,
}

public enum Severity
{
    Low,
    Medium,
    High,
    Critical,
}

public enum IncidentStage
{
    TicketOpened,
    Investigating,
    RecommendationReady,
    Escalated,
    AwaitingApproval,
    Approved,
    Rejected,
    Closed,
    ClosedByDemoReset,
}

public sealed class Alert
{
    public Alert(
        AlertId id,
        ToolId toolId,
        string? chamberId,
        string parameter,
        Severity severity,
        string rule,
        DateTimeOffset raisedAt,
        string correlationId)
    {
        if (id.IsEmpty)
        {
            throw new DomainRuleException("Alert id is required.");
        }

        if (toolId.IsEmpty)
        {
            throw new DomainRuleException("Tool id is required.");
        }

        Id = id;
        ToolId = toolId;
        ChamberId = string.IsNullOrWhiteSpace(chamberId) ? null : chamberId.Trim();
        Parameter = RequiredText.Value(parameter, "Parameter is required.");
        Severity = severity;
        Rule = RequiredText.Value(rule, "SPC rule is required.");
        RaisedAt = raisedAt;
        CorrelationId = RequiredText.Value(correlationId, "Correlation id is required.");
    }

    public AlertId Id { get; }

    public ToolId ToolId { get; }

    public string? ChamberId { get; }

    public string Parameter { get; }

    public Severity Severity { get; }

    public string Rule { get; }

    public DateTimeOffset RaisedAt { get; }

    public string CorrelationId { get; }

    /// <summary>When the signal came back in control. Null while the alert is active.</summary>
    public DateTimeOffset? ClearedAt { get; private set; }

    public bool IsActive => ClearedAt is null;

    public string Status => IsActive ? "active" : "cleared";

    public void Clear(DateTimeOffset at)
    {
        if (ClearedAt is null)
        {
            ClearedAt = at < RaisedAt ? RaisedAt : at;
        }
    }
}

public sealed class Ticket
{
    public Ticket(TicketId id, string number, IncidentId incidentId, ToolId toolId, string title, DateTimeOffset openedAt)
    {
        if (id.IsEmpty)
        {
            throw new DomainRuleException("Ticket id is required.");
        }

        if (incidentId.IsEmpty)
        {
            throw new DomainRuleException("Incident id is required.");
        }

        if (toolId.IsEmpty)
        {
            throw new DomainRuleException("Tool id is required.");
        }

        var normalizedNumber = RequiredText.Value(number, "Ticket number is required.");
        if (!normalizedNumber.StartsWith("TKT-", StringComparison.Ordinal))
        {
            throw new DomainRuleException("Ticket number must start with TKT-.");
        }

        Id = id;
        Number = normalizedNumber;
        IncidentId = incidentId;
        ToolId = toolId;
        Title = RequiredText.Value(title, "Ticket title is required.");
        OpenedAt = openedAt;
    }

    public TicketId Id { get; }

    public string Number { get; }

    public IncidentId IncidentId { get; }

    public ToolId ToolId { get; }

    public string Title { get; }

    public DateTimeOffset OpenedAt { get; }
}

/// <summary>
/// The normalized intake record. Every channel becomes one incident before investigation starts.
/// </summary>
public sealed class Incident
{
    private Incident(
        IncidentId id,
        ToolId toolId,
        string? chamberId,
        string parameter,
        IncidentChannel channel,
        Severity severity,
        string summary,
        string correlationId,
        DateTimeOffset openedAt)
    {
        Id = id;
        ToolId = toolId;
        ChamberId = chamberId;
        Parameter = parameter;
        Channel = channel;
        Severity = severity;
        Summary = summary;
        CorrelationId = correlationId;
        OpenedAt = openedAt;
        Stage = IncidentStage.TicketOpened;
    }

    public IncidentId Id { get; }

    public ToolId ToolId { get; }

    public string? ChamberId { get; }

    public string Parameter { get; }

    public IncidentChannel Channel { get; }

    public Severity Severity { get; }

    public string Summary { get; }

    public string CorrelationId { get; }

    public DateTimeOffset OpenedAt { get; }

    public IncidentStage Stage { get; private set; }

    public static Incident Open(
        IncidentId id,
        ToolId toolId,
        string? chamberId,
        string parameter,
        IncidentChannel channel,
        Severity severity,
        string summary,
        string correlationId,
        DateTimeOffset openedAt)
    {
        if (id.IsEmpty)
        {
            throw new DomainRuleException("Incident id is required.");
        }

        if (toolId.IsEmpty)
        {
            throw new DomainRuleException("Tool id is required.");
        }

        return new Incident(
            id,
            toolId,
            string.IsNullOrWhiteSpace(chamberId) ? null : chamberId.Trim(),
            RequiredText.Value(parameter, "Parameter is required."),
            channel,
            severity,
            RequiredText.Value(summary, "Incident summary is required."),
            RequiredText.Value(correlationId, "Correlation id is required."),
            openedAt);
    }

    public void Advance(IncidentStage next)
    {
        if (!IncidentLifecycle.CanTransition(Stage, next))
        {
            throw new DomainRuleException($"Cannot move an incident from {Stage} to {next}.");
        }

        Stage = next;
    }

    /// <summary>
    /// Closes an open incident for a demo reset. A normal close and an incident already closed this way are left unchanged by the caller.
    /// </summary>
    public void CloseByDemoReset()
    {
        if (Stage == IncidentStage.ClosedByDemoReset)
        {
            return;
        }

        Advance(IncidentStage.ClosedByDemoReset);
    }
}
