// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain;

namespace Mps.Domain.Impact;

public enum ImpactScopeKind
{
    Chamber,
    Tool,
    Recipe,
    MaterialLot,
}

/// <summary>
/// Deterministic scope and timing features handed to the resolution agent. The agent does not compute them.
/// </summary>
public sealed record ImpactScope
{
    public ImpactScope(ImpactScopeKind kind, string key, DateTimeOffset? alignedEventAt, string? alignmentNote)
    {
        Kind = kind;
        Key = RequiredText.Value(key, "Impact scope key is required.");
        AlignedEventAt = alignedEventAt;
        AlignmentNote = string.IsNullOrWhiteSpace(alignmentNote) ? null : alignmentNote.Trim();
    }

    public ImpactScopeKind Kind { get; }

    public string Key { get; }

    public DateTimeOffset? AlignedEventAt { get; }

    public string? AlignmentNote { get; }
}
