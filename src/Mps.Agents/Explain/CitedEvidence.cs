// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Identity;

namespace Mps.Agents.Explain;

/// <summary>
/// Plain-language explanation for one evidence id. A sensor signal also names its signal id.
/// </summary>
public sealed class CitedEvidence
{
    public CitedEvidence(EvidenceId evidenceId, string whyItMatters, string? signalId)
    {
        if (evidenceId.IsEmpty)
        {
            throw new ArgumentException("Evidence id is required.", nameof(evidenceId));
        }

        if (string.IsNullOrWhiteSpace(whyItMatters))
        {
            throw new ArgumentException("whyItMatters is required.", nameof(whyItMatters));
        }

        EvidenceId = evidenceId;
        WhyItMatters = whyItMatters.Trim();
        SignalId = string.IsNullOrWhiteSpace(signalId) ? null : signalId.Trim();
    }

    public EvidenceId EvidenceId { get; }

    public string WhyItMatters { get; }

    public string? SignalId { get; }
}
