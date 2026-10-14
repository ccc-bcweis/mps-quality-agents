// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Agents.Grounding;

/// <summary>
/// Whether a resolution claim is grounded in the evidence pack.
/// </summary>
public sealed class GroundingResult
{
    public GroundingResult(bool accepted, IReadOnlyList<string> failures)
        : this(accepted, failures, null, false)
    {
    }

    public GroundingResult(bool accepted, IReadOnlyList<string> failures, ResolutionClaim? claim, bool repaired)
    {
        ArgumentNullException.ThrowIfNull(failures);
        Failures = failures.ToArray();
        Accepted = accepted && Failures.Count == 0;
        Claim = Accepted ? claim : null;
        Repaired = Accepted && repaired;
    }

    public bool Accepted { get; }

    public IReadOnlyList<string> Failures { get; }

    /// <summary>
    /// The claim to publish. Set when <see cref="Accepted"/> is true, after any repair.
    /// </summary>
    public ResolutionClaim? Claim { get; }

    public bool Repaired { get; }
}
