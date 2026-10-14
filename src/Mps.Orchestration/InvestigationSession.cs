// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Agents.AI.Workflows;

using Mps.Agents.Explain;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;
using Mps.Domain.Investigations;

namespace Mps.Orchestration;

/// <summary>
/// Durable investigation state. The workflow graph exchanges ids. This object holds the evidence.
/// </summary>
public sealed class InvestigationSession
{
    private readonly List<EvidenceItem> _evidence = [];
    private readonly List<Hypothesis> _hypotheses = [];
    private readonly List<CitedEvidence> _explanations = [];
    private readonly List<EvidenceMatrixEntry> _matrix = [];

    public InvestigationSession(InvestigationId id, IncidentId incidentId)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("Investigation id is required.", nameof(id));
        }

        if (incidentId.IsEmpty)
        {
            throw new ArgumentException("Incident id is required.", nameof(incidentId));
        }

        Id = id;
        IncidentId = incidentId;
    }

    public InvestigationId Id { get; }

    public IncidentId IncidentId { get; }

    public string CorrelationId { get; internal set; } = "pending";

    public InvestigationStage Stage { get; internal set; } = InvestigationStage.Started;

    public string? EscalationReason { get; internal set; }

    public IReadOnlyList<EvidenceItem> Evidence => _evidence;

    public IReadOnlyList<Hypothesis> Hypotheses => _hypotheses;

    public IReadOnlyList<CitedEvidence> Explanations => _explanations;

    public IReadOnlyList<EvidenceMatrixEntry> EvidenceMatrix => _matrix;

    /// <summary>
    /// Checkpoint taken while the run is waiting for a human decision. Resume needs the same checkpoint manager.
    /// </summary>
    public CheckpointInfo? Checkpoint { get; internal set; }

    public HumanDecision? PendingDecision { get; internal set; }

    public string? ProposedSummary { get; internal set; }

    public int ResolutionCalls { get; internal set; }

    public string? InvestigationPromptVersion { get; internal set; }

    public string? ResolutionPromptVersion { get; internal set; }

    public InvestigationSnapshot ToSnapshot()
    {
        return new InvestigationSnapshot(
            Id,
            IncidentId,
            Stage,
            _evidence,
            _hypotheses,
            EscalationReason,
            CorrelationId);
    }

    internal void ReplaceEvidence(IEnumerable<EvidenceItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _evidence.Clear();
        _evidence.AddRange(items);
    }

    internal void AddHypothesis(Hypothesis hypothesis)
    {
        ArgumentNullException.ThrowIfNull(hypothesis);
        _hypotheses.Add(hypothesis);
    }

    internal void ClearHypotheses()
    {
        _hypotheses.Clear();
    }

    internal void ReplaceExplanations(IEnumerable<CitedEvidence> explanations)
    {
        ArgumentNullException.ThrowIfNull(explanations);
        _explanations.Clear();
        _explanations.AddRange(explanations);
    }

    internal void ReplaceMatrix(IEnumerable<EvidenceMatrixEntry> matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        _matrix.Clear();
        _matrix.AddRange(matrix);
    }
}
