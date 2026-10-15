// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Mps.Bus;

namespace Mps.Api.Demo;

/// <summary>One bus envelope saved for a scenario recording. Payload is cloned so the bus can reuse its buffer.</summary>
public sealed record ReplayMessage(
    string EventId,
    string Type,
    string CorrelationId,
    string? CausationId,
    string? ParentEventId,
    long Sequence,
    string Source,
    string? SiteId,
    string? ToolId,
    string? ChamberId,
    DateTimeOffset OccurredAt,
    DateTimeOffset RecordedAt,
    int SchemaVersion,
    JsonElement Payload)
{
    public static ReplayMessage From(EventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return new ReplayMessage(
            envelope.EventId,
            envelope.Type,
            envelope.CorrelationId,
            envelope.CausationId,
            envelope.ParentEventId,
            envelope.Sequence,
            envelope.Source,
            envelope.SiteId,
            envelope.ToolId,
            envelope.ChamberId,
            envelope.OccurredAt,
            envelope.RecordedAt,
            envelope.SchemaVersion,
            envelope.Payload.Clone());
    }
}
