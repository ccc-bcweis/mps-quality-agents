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
    string? ChamberName,
    double? Center = null,
    double? Sigma = null)
{
    /// <summary>Lower control limit: centre minus three sigma. Empty without both.</summary>
    public double? ControlLow => Center is double center && Sigma is double sigma ? center - (3d * sigma) : null;

    /// <summary>Upper control limit: centre plus three sigma. Empty without both.</summary>
    public double? ControlHigh => Center is double center && Sigma is double sigma ? center + (3d * sigma) : null;

    /// <summary>True when the value sits outside the recipe spec. Empty when either is missing.</summary>
    public bool? IsOutOfSpec => Value is double value && (SpecLow is not null || SpecHigh is not null)
        ? (SpecLow is double low && value < low) || (SpecHigh is double high && value > high)
        : null;

    public bool IsEmpty =>
        Value is null
        && string.IsNullOrWhiteSpace(Unit)
        && SpecLow is null
        && SpecHigh is null
        && string.IsNullOrWhiteSpace(ToolName)
        && string.IsNullOrWhiteSpace(ChamberName)
        && Center is null
        && Sigma is null;

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
