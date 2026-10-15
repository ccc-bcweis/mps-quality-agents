// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Http;

public sealed record TraceNodeResponse(
    string EventId,
    string Type,
    string Summary,
    DateTimeOffset OccurredAt,
    string? ToolId,
    string? ToolName,
    double? Value,
    double? LimitLow,
    double? LimitHigh,
    IReadOnlyList<TraceNodeResponse> Children,
    bool Truncated);

public sealed record CausalTraceResponse(
    string FocusEventId,
    string RootEventId,
    IReadOnlyList<TraceNodeResponse> Ancestors,
    TraceNodeResponse Tree);
