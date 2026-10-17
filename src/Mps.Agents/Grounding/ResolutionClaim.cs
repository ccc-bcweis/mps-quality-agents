// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Explain;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Agents.Grounding;

/// <summary>
/// The resolution agent's structured claim. Counter-evidence is text. Citations are evidence ids.
/// </summary>
public sealed class ResolutionClaim
{
    public ResolutionClaim(
        RootCauseCategory? category,
        string? cause,
        double? confidence,
        IReadOnlyList<EvidenceId> evidenceIds,
        IReadOnlyList<string> counterEvidence,
        IReadOnlyList<string> actions,
        IReadOnlyList<GroundedStatement> claims,
        IReadOnlyList<CitedEvidence>? explanations = null,
        IReadOnlyList<EvidenceMatrixEntry>? evidenceMatrix = null,
        string? shape = null)
    {
        ArgumentNullException.ThrowIfNull(evidenceIds);
        ArgumentNullException.ThrowIfNull(counterEvidence);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(claims);

        Category = category;
        Cause = string.IsNullOrWhiteSpace(cause) ? null : cause.Trim();
        Confidence = confidence;
        EvidenceIds = evidenceIds.ToArray();
        CounterEvidence = counterEvidence.ToArray();
        Actions = actions.ToArray();
        Claims = claims.ToArray();
        Explanations = explanations?.ToArray() ?? [];
        EvidenceMatrix = evidenceMatrix?.ToArray() ?? [];
        Shape = string.IsNullOrWhiteSpace(shape) ? null : shape.Trim();
    }

    public RootCauseCategory? Category { get; }

    public string? Cause { get; }

    public double? Confidence { get; }

    public IReadOnlyList<EvidenceId> EvidenceIds { get; }

    public IReadOnlyList<string> CounterEvidence { get; }

    public IReadOnlyList<string> Actions { get; }

    public IReadOnlyList<GroundedStatement> Claims { get; }

    public IReadOnlyList<CitedEvidence> Explanations { get; }

    public IReadOnlyList<EvidenceMatrixEntry> EvidenceMatrix { get; }

    /// <summary>The signal shape the agent reports, an id from the signal shape catalog. The cause check compares it with the measured shape.</summary>
    public string? Shape { get; }
}
