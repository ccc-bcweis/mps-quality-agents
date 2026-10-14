// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Capa;
using Mps.Domain.Equipment;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;

namespace Mps.Api.Http;

public sealed record HealthResponse(string Status, string Service);

public sealed record ToolResponse(string Id, string Name, ToolFamily Family, string Model, string Bay, IReadOnlyList<ChamberResponse> Chambers);

public sealed record ChamberResponse(string Id, string Name);

public sealed record AlertResponse(
    Guid Id,
    string ToolId,
    string? ChamberId,
    string Parameter,
    Severity Severity,
    string Rule,
    DateTimeOffset RaisedAt,
    string CorrelationId);

public sealed record TicketResponse(
    Guid Id,
    string Number,
    Guid IncidentId,
    string ToolId,
    string Title,
    DateTimeOffset OpenedAt);

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
    IncidentStage Stage);

public sealed record EvidenceResponse(
    Guid Id,
    EvidenceSource Source,
    string Title,
    string Excerpt,
    DateTimeOffset ObservedAt,
    string? ToolId,
    string? ExternalRef);

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
    string CorrelationId);

public sealed record CapaResponse(
    Guid Id,
    Guid IncidentId,
    string Summary,
    Guid ApprovalTokenId,
    string ActionHash,
    CapaStage Stage,
    DateTimeOffset OpenedAt);

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
}
