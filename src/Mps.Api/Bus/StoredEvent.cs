// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Bus;

/// <summary>One row from the event log or the telemetry table. Telemetry rows use the reading type.</summary>
public sealed record StoredEvent(
    string EventId,
    string Type,
    string CorrelationId,
    string? CausationId,
    string? ParentEventId,
    long Sequence,
    string? SiteId,
    string? ToolId,
    string? ChamberId,
    DateTimeOffset OccurredAt,
    string PayloadJson,
    string? Parameter,
    double? Value,
    double? LimitLow,
    double? LimitHigh,
    string? Unit,
    bool FromTelemetry);
