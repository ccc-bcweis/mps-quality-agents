// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json.Serialization;

using Mps.Application.Evidence;
using Mps.Domain.Capa;
using Mps.Domain.Equipment;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;

namespace Mps.Api.Http;

public sealed record HealthResponse(string Status, string Service);

public sealed record ToolResponse(string Id, string Name, ToolFamily Family, string Model, string Bay, IReadOnlyList<ChamberResponse> Chambers, string SiteId);

public sealed record ChamberResponse(string Id, string Name);

public sealed record AlertResponse(
    Guid Id,
    string ToolId,
    string? ChamberId,
    string Parameter,
    Severity Severity,
    string Rule,
    DateTimeOffset RaisedAt,
    string CorrelationId,
    string SiteId,
    string Status = "active",
    DateTimeOffset? ClearedAt = null,
    DateTimeOffset? LastSeenAt = null,
    int Occurrences = 1);

public sealed record TicketResponse(
    Guid Id,
    string Number,
    Guid IncidentId,
    string ToolId,
    string Title,
    DateTimeOffset OpenedAt,
    string SiteId,
    string Status = TicketStatus.Open);

/// <summary>A ticket is closed when its incident is closed (by work or by reset); otherwise it is open.</summary>
public static class TicketStatus
{
    public const string Open = "open";
    public const string Closed = "closed";

    public static string For(IncidentStage? stage) =>
        stage is IncidentStage.Closed or IncidentStage.ClosedByDemoReset ? Closed : Open;
}

public sealed record IncidentResponse(
    Guid Id,
    string ToolId,
    string? ChamberId,
    string Parameter,
    IncidentChannel Channel,
    Severity Severity,
    string Summary,
    string CorrelationId,
    DateTimeOffset OpenedAt,
    IncidentStage Stage,
    string SiteId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? StatusLabel = null);

/// <summary>Kind is approve, override, reject or requestInfo.</summary>
public sealed record DecisionRecordResponse(string Kind, string? Note, string? DecidedBy, DateTimeOffset DecidedAt);

public sealed record EvidenceResponse(
    Guid Id,
    EvidenceSource Source,
    string Title,
    string Excerpt,
    DateTimeOffset ObservedAt,
    string? ToolId,
    string? ExternalRef,
    string? WhyItMatters,
    string? SignalId,
    double? Value = null,
    string? Unit = null,
    double? SpecLow = null,
    double? SpecHigh = null,
    string? ToolName = null,
    string? ChamberName = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? SourceEventIds = null,
    double? ControlLow = null,
    double? ControlHigh = null,
    double? Center = null,
    string? LimitState = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SuppliedBy = null);

public sealed record EvidenceMatrixResponse(
    RootCauseCategory Category,
    Guid EvidenceId,
    EvidenceStance Stance,
    string? WhyItMatters);

public sealed record HypothesisResponse(
    Guid Id,
    RootCauseCategory Category,
    string Cause,
    double Confidence,
    IReadOnlyList<Guid> EvidenceIds,
    IReadOnlyList<string> CounterEvidence,
    IReadOnlyList<string> Actions);

public sealed record InvestigationResponse(
    Guid Id,
    Guid IncidentId,
    InvestigationStage Stage,
    IReadOnlyList<EvidenceResponse> Evidence,
    IReadOnlyList<HypothesisResponse> Hypotheses,
    string? EscalationReason,
    string CorrelationId,
    IReadOnlyList<EvidenceMatrixResponse> EvidenceMatrix,
    string SiteId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? StatusLabel = null)
{
    /// <summary>The recorded human decision, once made; the console shows it as a read-only record.</summary>
    public DecisionRecordResponse? Decision { get; init; }

    /// <summary>Round on the ticket (the evidence loop): 1 for the first run, then one more each time a person added evidence.</summary>
    public int Round { get; init; } = 1;

    /// <summary>The most rounds a ticket can have (data/metadata/investigation.yaml evidenceRounds.max).</summary>
    public int MaxRounds { get; init; } = Mps.Application.Decisions.EvidenceRoundSettings.Shared.MaxRounds;
}

public sealed record CapaResponse(
    Guid Id,
    Guid IncidentId,
    string Summary,
    Guid ApprovalTokenId,
    string ActionHash,
    CapaStage Stage,
    DateTimeOffset OpenedAt,
    string SiteId,
    string? ApprovedBy = null,
    DateTimeOffset? ApprovedAt = null,
    string? ToolId = null,
    string? Note = null);

public sealed record AuditEventResponse(
    DateTimeOffset At,
    string Kind,
    string Summary,
    string? Actor,
    string? ToolId,
    string? CorrelationId,
    string? RecordId);

public sealed record CorrelationTraceResponse(string CorrelationId, IReadOnlyList<AuditEventResponse> Events);

public sealed record AuditLogResponse(IReadOnlyList<AuditEventResponse> Entries, IReadOnlyList<CorrelationTraceResponse> Traces);

public sealed record DecisionResponse(InvestigationResponse Investigation, CapaResponse? Capa);

public sealed record FaultAcceptedResponse(Guid FaultId, string CorrelationId);

public sealed record MetricsResponse(
    string Mode,
    double? CompletionRate,
    double? GroundingScore,
    double? CategoryAccuracy,
    int ToolFailureCount,
    double? LatencyP95Ms,
    double? CostPerInvestigationUsd,
    double SpendTodayUsd,
    double SpendCapUsd);

public sealed class OpenIncidentBody
{
    public string? ToolId { get; set; }

    public string? ChamberId { get; set; }

    public string? Parameter { get; set; }

    public string? Summary { get; set; }

    public Severity? Severity { get; set; }
}

public sealed class DecisionBody
{
    public HumanDecisionKind? Kind { get; set; }

    public string? Comment { get; set; }

    public string? ApproverId { get; set; }
}

public sealed class AdvanceCapaBody
{
    public CapaStage? Stage { get; set; }
}

public sealed class FaultBody
{
    public string? ToolId { get; set; }

    public string? ChamberId { get; set; }

    public string? Parameter { get; set; }

    public string? Shape { get; set; }

    public double? Magnitude { get; set; }

    public RootCauseCategory? RootCauseCategory { get; set; }

    public bool Confounders { get; set; }

    public double? DurationMinutes { get; set; }
}

public sealed record ThroughputResponse(double WaferStartsPerHour, double WaferOutsPerHour, int Wip, double YieldPct, double UptimePct);

public sealed record SiteBaySummaryResponse(string Id, string Name, int ToolCount);

public sealed record SiteSummaryResponse(
    string Id,
    string Name,
    string Role,
    string Location,
    string Timezone,
    IReadOnlyList<SiteBaySummaryResponse> Bays,
    ThroughputResponse Kpis);

public sealed record PlacedToolResponse(string Id, string Name, ToolFamily Family, string Model, string Status, double X, double Y, string SiteId);

public sealed record FleetLiveResponse(DateTimeOffset SimulatedNow, IReadOnlyList<FleetLiveToolResponse> Tools);

public sealed record FleetLiveToolResponse(string ToolId, string SiteId, string Status, IReadOnlyList<LiveReadingResponse> Readings);

public sealed record ToolLiveResponse(
    string ToolId,
    string SiteId,
    DateTimeOffset SimulatedNow,
    string Status,
    IReadOnlyList<LiveReadingResponse> Readings,
    IReadOnlyList<LiveSeriesResponse> Series)
{
    /// <summary>Readable name of the recipe running now, from the fleet data (e.g. "PECVD oxide").</summary>
    public string? RecipeName { get; init; }
}

/// <summary>
/// One live reading with the recipe-step window it is judged against. Spec is the recipe minimum and maximum; control
/// limits are the nominal plus or minus three sigma. LimitState is outOfSpec, outOfControl or inControl, or null without a window.
/// </summary>
public sealed record LiveReadingResponse(
    string SensorId,
    string Parameter,
    string ChamberId,
    string Unit,
    DateTimeOffset At,
    double Value,
    string Status,
    double? SpecLow = null,
    double? SpecHigh = null,
    double? Center = null,
    double? ControlLow = null,
    double? ControlHigh = null,
    string? LimitState = null);

/// <summary>One live series. Center and Sigma are the limits the rules engine checks; null when the series has none.</summary>
public sealed record LiveSeriesResponse(string Parameter, string ChamberId, IReadOnlyList<LivePointResponse> Points, double? Center = null, double? Sigma = null);

public sealed record LivePointResponse(DateTimeOffset At, double Value);

/// <summary>
/// Hub message toolLive, sent to the pages watching one tool. Readings are the tool's latest values (a full,
/// small set). Series carry only the points kept since the last message, with the limits the rules engine uses.
/// Resync asks the page to load GET /api/tools/{toolId}/live again because more points arrived than one message carries.
/// </summary>
public sealed record ToolLiveDelta(
    string ToolId,
    string SiteId,
    DateTimeOffset SimulatedNow,
    string Status,
    IReadOnlyList<LiveReadingResponse> Readings,
    IReadOnlyList<LiveSeriesResponse> Series,
    string? RecipeName,
    bool Resync,
    bool Manual = false);

/// <summary>Hub message fleetLive, sent to the pages watching a plant: only the tools whose readings changed.</summary>
public sealed record FleetLiveDelta(DateTimeOffset SimulatedNow, IReadOnlyList<FleetLiveToolResponse> Tools);

/// <summary>Hub message clock: the plant clock every record is stamped with.</summary>
public sealed record LiveClockMessage(DateTimeOffset SimulatedNow);

/// <summary>Hub message resync: live data was rebuilt (a reset), so every page loads its data again.</summary>
public sealed record LiveResyncMessage(string Reason);

public sealed record SiteBayDetailResponse(string Id, string Name, IReadOnlyList<PlacedToolResponse> Tools, ThroughputResponse Kpis);

public sealed record WaferFlowResponse(string FromBayId, string ToBayId, string Label);

public sealed record SiteDetailResponse(
    string Id,
    string Name,
    string Role,
    string Location,
    string Timezone,
    IReadOnlyList<SiteBayDetailResponse> Bays,
    IReadOnlyList<WaferFlowResponse> Flows,
    ThroughputResponse Kpis);

public sealed record SiteParameterStatsResponse(
    string SiteId,
    string SiteName,
    double Mean,
    double Sigma,
    double AlarmRate,
    double UptimePct,
    double YieldPct,
    bool Discrepancy,
    string? Reason);

public sealed record ParameterComparisonResponse(string Parameter, string Unit, IReadOnlyList<SiteParameterStatsResponse> Sites);

public sealed record FamilyComparisonResponse(ToolFamily Family, string ReferenceSiteId, IReadOnlyList<ParameterComparisonResponse> Parameters);

public sealed record PrintoutInspectionResponse(
    string ToolId,
    string? ChamberId,
    DateTimeOffset ObservedAt,
    string Instrument,
    string Parameter,
    double Value,
    string Unit,
    double SpecMinimum,
    double SpecMaximum,
    string? Shape,
    string? OperatorInitials,
    string ParseMethod,
    bool OutOfSpec,
    Guid? FileId = null);

public sealed class PrintoutConfirmBody
{
    public string? ToolId { get; set; }

    public string? ChamberId { get; set; }

    public DateTimeOffset? ObservedAt { get; set; }

    public string? Instrument { get; set; }

    public string? Parameter { get; set; }

    public double? Value { get; set; }

    public string? Unit { get; set; }

    public double? SpecMinimum { get; set; }

    public double? SpecMaximum { get; set; }

    public string? Shape { get; set; }

    public string? OperatorInitials { get; set; }

    /// <summary>The time printed on the sheet, sent when the user pressed Now so the audit trail keeps it.</summary>
    public DateTimeOffset? PrintedObservedAt { get; set; }

    /// <summary>The upload the inspect call kept, attached to the evidence.</summary>
    public Guid? FileId { get; set; }
}

public sealed record ManualReadingResponse(
    Guid EvidenceId,
    Guid? IncidentId,
    Guid? AlertId,
    string ToolId,
    string SiteId,
    string? ChamberId,
    string Parameter,
    string Instrument,
    double Value,
    string Unit,
    double SpecMinimum,
    double SpecMaximum,
    bool OutOfSpec,
    DateTimeOffset ObservedAt,
    DateTimeOffset? PrintedObservedAt,
    bool OlderThanWindow,
    string? OperatorInitials,
    string? FileName,
    string? FileUrl,
    string Source,
    bool Active);

public sealed record ReplayCaptureResponse(string ScenarioId, int MessageCount);
