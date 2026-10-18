// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain;
using Mps.Domain.Identity;

namespace Mps.Domain.Hypotheses;

public enum RootCauseCategory
{
    EngineeringChange,
    EquipmentCalibrationOrAlignment,
    SupplyChainMaterial,
    ProcessDrift,
}

/// <summary>
/// A ranked root-cause claim. It must cite evidence. Confidence is a fraction from 0 to 1.
/// </summary>
public sealed class Hypothesis
{
    public Hypothesis(
        HypothesisId id,
        RootCauseCategory category,
        string cause,
        double confidence,
        IReadOnlyList<EvidenceId> evidenceIds,
        IReadOnlyList<string> counterEvidence,
        IReadOnlyList<string> actions,
        IReadOnlyList<RuledOutEntry>? ruledOut = null)
    {
        if (id.IsEmpty)
        {
            throw new DomainRuleException("Hypothesis id is required.");
        }

        ArgumentNullException.ThrowIfNull(evidenceIds);
        ArgumentNullException.ThrowIfNull(counterEvidence);
        ArgumentNullException.ThrowIfNull(actions);

        if (evidenceIds.Count == 0)
        {
            throw new DomainRuleException("A hypothesis must cite at least one evidence item.");
        }

        if (evidenceIds.Any(evidenceId => evidenceId.IsEmpty))
        {
            throw new DomainRuleException("Evidence ids are required.");
        }

        if (confidence is < 0d or > 1d || double.IsNaN(confidence))
        {
            throw new DomainRuleException("Confidence must be between 0 and 1.");
        }

        var ruledOutEntries = ruledOut?.ToArray() ?? [];
        if (ruledOutEntries.Length > 0)
        {
            var cited = new HashSet<EvidenceId>(evidenceIds);
            foreach (var entry in ruledOutEntries)
            {
                if (cited.Contains(entry.Id))
                {
                    throw new DomainRuleException("An evidence id cannot be both cited and ruled out.");
                }
            }
        }

        Id = id;
        Category = category;
        Cause = RequiredText.Value(cause, "Hypothesis cause is required.");
        Confidence = confidence;
        EvidenceIds = evidenceIds.ToArray();
        CounterEvidence = counterEvidence.Select(item => RequiredText.Value(item, "Counter-evidence text is required.")).ToArray();
        Actions = actions.Select(item => RequiredText.Value(item, "Proposed action text is required.")).ToArray();
        RuledOut = ruledOutEntries;
    }

    public HypothesisId Id { get; }

    public RootCauseCategory Category { get; }

    public string Cause { get; }

    public double Confidence { get; }

    public IReadOnlyList<EvidenceId> EvidenceIds { get; }

    public IReadOnlyList<string> CounterEvidence { get; }

    public IReadOnlyList<string> Actions { get; }

    public IReadOnlyList<RuledOutEntry> RuledOut { get; }
}
