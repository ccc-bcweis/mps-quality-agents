// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Agents.Grounding;

/// <summary>
/// Whether a resolution claim is grounded in the evidence pack.
/// </summary>
public sealed class GroundingResult
{
    public GroundingResult(bool accepted, IReadOnlyList<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        Failures = failures.ToArray();
        Accepted = accepted && Failures.Count == 0;
    }

    public bool Accepted { get; }

    public IReadOnlyList<string> Failures { get; }
}
