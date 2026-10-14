// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Agents.Explain;

/// <summary>
/// One cell of the evidence matrix: a root-cause class, an evidence id from the pack, and a stance.
/// </summary>
public sealed class EvidenceMatrixEntry
{
    public EvidenceMatrixEntry(RootCauseCategory category, EvidenceId evidenceId, EvidenceStance stance, string? whyItMatters)
    {
        if (evidenceId.IsEmpty)
        {
            throw new ArgumentException("Evidence id is required.", nameof(evidenceId));
        }

        if (!Enum.IsDefined(stance))
        {
            throw new ArgumentOutOfRangeException(nameof(stance), "Stance is not a known value.");
        }

        Category = category;
        EvidenceId = evidenceId;
        Stance = stance;
        WhyItMatters = string.IsNullOrWhiteSpace(whyItMatters) ? null : whyItMatters.Trim();
    }

    public RootCauseCategory Category { get; }

    public EvidenceId EvidenceId { get; }

    public EvidenceStance Stance { get; }

    public string? WhyItMatters { get; }
}
