// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Domain.Events;

/// <summary>
/// Dotted event names. Commands and domain facts share one envelope. The bus, the application and the API all read
/// these, so each name is spelled once.
/// </summary>
public static class EventTypes
{
    public const string TelemetryReading = "telemetry.reading";

    public const string AlertRaised = "alert.raised";

    public const string AlertCleared = "alert.cleared";

    public const string FaultCleared = "fault.cleared";

    public const string ScenarioStopped = "command.scenario.stop";

    public const string RuleMatched = "rule.matched";

    public const string TicketCreated = "ticket.created";

    public const string TicketCapped = "ticket.capped";

    public const string IncidentChanged = "incident.changed";

    public const string InvestigationStep = "investigation.step";

    public const string InvestigationChanged = "investigation.changed";

    public const string ApprovalGranted = "approval.granted";

    public const string CapaCreated = "capa.created";

    public const string CapaAdvanced = "capa.advanced";

    public const string DocumentUploaded = "document.uploaded";

    public const string FaultInject = "command.fault.inject";

    public const string ScenarioStart = "command.scenario.start";

    public const string ResetHard = "command.reset.hard";

    public const string ResetSoft = "command.reset.soft";

    public const string OperatorReport = "command.operator.report";

    public const string DocumentUpload = "command.document.upload";

    public const string ApprovalGrant = "command.approval.grant";

    public const string CapaAdvance = "command.capa.advance";

    public const string InvestigationStart = "command.investigation.start";

    public static bool IsTelemetry(string type) =>
        string.Equals(type, TelemetryReading, StringComparison.Ordinal);
}
