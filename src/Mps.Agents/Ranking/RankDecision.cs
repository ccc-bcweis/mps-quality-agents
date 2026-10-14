// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Grounding;

namespace Mps.Agents.Ranking;

/// <summary>
/// A ranked root cause, or an escalation when the primaries overlap or none apply.
/// </summary>
public sealed class RankDecision
{
    private RankDecision(bool escalate, string? reason, ResolutionClaim? claim)
    {
        Escalate = escalate;
        Reason = reason;
        Claim = claim;
    }

    public bool Escalate { get; }

    public string? Reason { get; }

    public ResolutionClaim? Claim { get; }

    public static RankDecision Escalation(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("An escalation names the overlap or the gap.", nameof(reason));
        }

        return new RankDecision(true, reason.Trim(), null);
    }

    public static RankDecision Recommendation(ResolutionClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        return new RankDecision(false, null, claim);
    }
}
