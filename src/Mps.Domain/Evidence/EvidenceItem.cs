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
        string? externalRef)
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
    }

    public EvidenceId Id { get; }

    public EvidenceSource Source { get; }

    public string Title { get; }

    public string Excerpt { get; }

    public DateTimeOffset ObservedAt { get; }

    public string? ToolId { get; }

    public string? ExternalRef { get; }
}
