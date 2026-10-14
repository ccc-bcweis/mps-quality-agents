// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Agents.Evidence;

/// <summary>
/// Gaps and conflicts found before a root-cause category is allowed.
/// </summary>
public sealed class GateResult
{
    public GateResult(
        GateOutcome outcome,
        IReadOnlyList<string> gaps,
        IReadOnlyList<string> conflicts,
        string? escalationReason)
    {
        ArgumentNullException.ThrowIfNull(gaps);
        ArgumentNullException.ThrowIfNull(conflicts);
        if (outcome != GateOutcome.Sufficient && string.IsNullOrWhiteSpace(escalationReason))
        {
            throw new ArgumentException("A request or escalation names the gap or conflict.", nameof(escalationReason));
        }

        Outcome = outcome;
        Gaps = gaps.ToArray();
        Conflicts = conflicts.ToArray();
        EscalationReason = string.IsNullOrWhiteSpace(escalationReason) ? null : escalationReason.Trim();
    }

    public GateOutcome Outcome { get; }

    public IReadOnlyList<string> Gaps { get; }

    public IReadOnlyList<string> Conflicts { get; }

    public string? EscalationReason { get; }
}
