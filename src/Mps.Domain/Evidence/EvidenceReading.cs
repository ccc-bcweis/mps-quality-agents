// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain;

namespace Mps.Domain.Evidence;

/// <summary>
/// Optional measured reading on an evidence item. Older packs leave it empty.
/// </summary>
public sealed record EvidenceReading(
    double? Value,
    string? Unit,
    double? SpecLow,
    double? SpecHigh,
    string? ToolName,
    string? ChamberName)
{
    public bool IsEmpty =>
        Value is null
        && string.IsNullOrWhiteSpace(Unit)
        && SpecLow is null
        && SpecHigh is null
        && string.IsNullOrWhiteSpace(ToolName)
        && string.IsNullOrWhiteSpace(ChamberName);

    public static EvidenceReading Place(string? toolId, string? chamberId, double? value = null)
    {
        if (value is double measured && !double.IsFinite(measured))
        {
            throw new DomainRuleException("Evidence value must be finite.");
        }

        return new EvidenceReading(
            value,
            null,
            null,
            null,
            PlainEquipment.ToolName(toolId),
            PlainEquipment.ChamberName(chamberId));
    }
}
