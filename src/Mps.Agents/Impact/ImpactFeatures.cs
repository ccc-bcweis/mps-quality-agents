// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Impact;

namespace Mps.Agents.Impact;

/// <summary>
/// Scope, timing, and signal shape computed before the resolution agent runs.
/// </summary>
public sealed class ImpactFeatures
{
    public ImpactFeatures(
        ImpactScopeKind kind,
        string key,
        SignalShape shape,
        DateTimeOffset? alignedEventAt,
        string? alignmentNote)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Impact scope key is required.", nameof(key));
        }

        Kind = kind;
        Key = key.Trim();
        Shape = shape;
        AlignedEventAt = alignedEventAt;
        AlignmentNote = string.IsNullOrWhiteSpace(alignmentNote) ? null : alignmentNote.Trim();
    }

    public ImpactScopeKind Kind { get; }

    public string Key { get; }

    public SignalShape Shape { get; }

    public DateTimeOffset? AlignedEventAt { get; }

    public string? AlignmentNote { get; }

    public ImpactScope ToScope()
    {
        return new ImpactScope(Kind, Key, AlignedEventAt, AlignmentNote);
    }
}
