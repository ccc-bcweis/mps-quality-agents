// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

namespace Mps.Bus;

/// <summary>
/// One fact on the bus. The bus fills <see cref="EventId"/>, <see cref="Sequence"/>, and <see cref="RecordedAt"/>
/// when the caller leaves them empty. Payload is a JSON object.
/// </summary>
public sealed record EventEnvelope
{
    public const int CurrentSchemaVersion = 1;

    public EventEnvelope(
        string eventId,
        string type,
        string correlationId,
        string? causationId,
        string? parentEventId,
        long sequence,
        string source,
        string? siteId,
        string? toolId,
        string? chamberId,
        DateTimeOffset occurredAt,
        DateTimeOffset recordedAt,
        int schemaVersion,
        JsonElement payload)
    {
        EventId = eventId ?? string.Empty;
        Type = type ?? string.Empty;
        CorrelationId = correlationId ?? string.Empty;
        CausationId = string.IsNullOrWhiteSpace(causationId) ? null : causationId.Trim();
        ParentEventId = string.IsNullOrWhiteSpace(parentEventId) ? null : parentEventId.Trim();
        Sequence = sequence;
        Source = source ?? string.Empty;
        SiteId = Blank(siteId);
        ToolId = Blank(toolId);
        ChamberId = Blank(chamberId);
        OccurredAt = occurredAt;
        RecordedAt = recordedAt;
        SchemaVersion = schemaVersion;
        Payload = payload;
    }

    public string EventId { get; }

    public string Type { get; }

    public string CorrelationId { get; }

    public string? CausationId { get; }

    public string? ParentEventId { get; }

    public long Sequence { get; }

    public string Source { get; }

    public string? SiteId { get; }

    public string? ToolId { get; }

    public string? ChamberId { get; }

    public DateTimeOffset OccurredAt { get; }

    public DateTimeOffset RecordedAt { get; }

    public int SchemaVersion { get; }

    public JsonElement Payload { get; }

    public static EventEnvelope Draft(
        string type,
        string correlationId,
        string source,
        DateTimeOffset occurredAt,
        object? payload,
        string? causationId = null,
        string? parentEventId = null,
        string? siteId = null,
        string? toolId = null,
        string? chamberId = null)
    {
        if (string.IsNullOrWhiteSpace(type) || !type.Contains('.', StringComparison.Ordinal))
        {
            throw new ArgumentException("Event type must be a dotted name.", nameof(type));
        }

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException("Correlation id is required.", nameof(correlationId));
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Source is required.", nameof(source));
        }

        return new EventEnvelope(
            string.Empty,
            type.Trim(),
            correlationId.Trim(),
            causationId,
            parentEventId,
            0,
            source.Trim(),
            siteId,
            toolId,
            chamberId,
            occurredAt,
            default,
            CurrentSchemaVersion,
            EventJson.Payload(payload));
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
