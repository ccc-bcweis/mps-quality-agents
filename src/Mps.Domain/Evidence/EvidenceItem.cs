// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain;
using Mps.Domain.Identity;

namespace Mps.Domain.Evidence;

public enum EvidenceSource
{
    Telemetry,
    Spc,
    Mes,
    Qms,
    Plm,
    Erp,
    Cmms,
    Document,
    Ticketing,
    ManualMeasurement,

    /// <summary>Typed in by a person on the Investigation page (Add evidence). Carries who supplied it.</summary>
    UserProvided,
}

public sealed class EvidenceItem
{
    public EvidenceItem(
        EvidenceId id,
        EvidenceSource source,
        string title,
        string excerpt,
        DateTimeOffset observedAt,
        string? toolId,
        string? externalRef,
        EvidenceReading? reading = null,
        IReadOnlyList<string>? sourceEventIds = null,
        string? suppliedBy = null)
    {
        if (id.IsEmpty)
        {
            throw new DomainRuleException("Evidence id is required.");
        }

        Id = id;
        Source = source;
        Title = RequiredText.Value(title, "Evidence title is required.");
        Excerpt = excerpt?.Trim() ?? string.Empty;
        ObservedAt = observedAt;
        ToolId = string.IsNullOrWhiteSpace(toolId) ? null : toolId.Trim();
        ExternalRef = string.IsNullOrWhiteSpace(externalRef) ? null : externalRef.Trim();
        Reading = Normalize(reading);
        SourceEventIds = NormalizeIds(sourceEventIds);
        SuppliedBy = string.IsNullOrWhiteSpace(suppliedBy) ? null : suppliedBy.Trim();
        if (source == EvidenceSource.UserProvided && SuppliedBy is null)
        {
            throw new DomainRuleException("User-provided evidence names who supplied it.");
        }
    }

    public EvidenceId Id { get; }

    public EvidenceSource Source { get; }

    public string Title { get; }

    public string Excerpt { get; }

    public DateTimeOffset ObservedAt { get; }

    public string? ToolId { get; }

    public string? ExternalRef { get; }

    public EvidenceReading? Reading { get; }

    public IReadOnlyList<string> SourceEventIds { get; }

    /// <summary>Who typed this record in, for user-provided evidence; null for records the agents gathered.</summary>
    public string? SuppliedBy { get; }

    public EvidenceItem WithSourceEventIds(IReadOnlyList<string> sourceEventIds) =>
        new(Id, Source, Title, Excerpt, ObservedAt, ToolId, ExternalRef, Reading, sourceEventIds, SuppliedBy);

    private static List<string> NormalizeIds(IReadOnlyList<string>? sourceEventIds)
    {
        if (sourceEventIds is null || sourceEventIds.Count == 0)
        {
            return [];
        }

        var list = new List<string>(sourceEventIds.Count);
        foreach (var id in sourceEventIds)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var trimmed = id.Trim();
            if (!list.Contains(trimmed, StringComparer.Ordinal))
            {
                list.Add(trimmed);
            }
        }

        return list;
    }

    private static EvidenceReading? Normalize(EvidenceReading? reading)
    {
        if (reading is null || reading.IsEmpty)
        {
            return null;
        }

        if (reading.Value is double value && !double.IsFinite(value))
        {
            throw new DomainRuleException("Evidence value must be finite.");
        }

        if (reading.SpecLow is double low && !double.IsFinite(low))
        {
            throw new DomainRuleException("Evidence spec must be finite.");
        }

        if (reading.SpecHigh is double high && !double.IsFinite(high))
        {
            throw new DomainRuleException("Evidence spec must be finite.");
        }

        if (reading.SpecLow is double specLow && reading.SpecHigh is double specHigh && specLow > specHigh)
        {
            throw new DomainRuleException("Evidence spec low cannot exceed spec high.");
        }

        return reading with
        {
            Unit = Blank(reading.Unit),
            ToolName = Blank(reading.ToolName),
            ChamberName = Blank(reading.ChamberName),
        };
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
