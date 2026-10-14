// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Identity;

namespace Mps.Agents.Grounding;

/// <summary>
/// One sentence the resolution agent asserts. It must cite evidence that is already in the pack.
/// </summary>
public sealed class GroundedStatement
{
    public GroundedStatement(string text, IReadOnlyList<EvidenceId> evidenceIds)
    {
        ArgumentNullException.ThrowIfNull(evidenceIds);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("A grounded statement needs text.", nameof(text));
        }

        Text = text.Trim();
        EvidenceIds = evidenceIds.ToArray();
    }

    public string Text { get; }

    public IReadOnlyList<EvidenceId> EvidenceIds { get; }
}
