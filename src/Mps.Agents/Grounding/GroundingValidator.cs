// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Explain;
using Mps.Agents.Impact;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Agents.Grounding;

/// <summary>
/// Rejects a category, cause, or sentence that is not tied to evidence ids already in the pack.
/// whyItMatters must name a cited id from that pack. Matrix rows must too. Gaps are repaired from templates.
/// A citation of an id that is not in the pack is rejected.
/// </summary>
public static class GroundingValidator
{
    public static GroundingResult Validate(ResolutionClaim claim, IReadOnlyList<EvidenceItem> pack)
    {
        return Validate(claim, pack, null);
    }

    public static GroundingResult Validate(ResolutionClaim claim, IReadOnlyList<EvidenceItem> pack, WhyItMattersCatalog? catalog)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(pack);
        catalog ??= WhyItMattersCatalog.Shared;

        var known = new Dictionary<EvidenceId, EvidenceItem>();
        foreach (var item in pack)
        {
            known[item.Id] = item;
        }

        // A why-it-matters note for an id outside the pack or not cited is not load-bearing: drop it and repair, since cited ids are still checked strictly.
        bool Keep(CitedEvidence explanation) => known.ContainsKey(explanation.EvidenceId) && claim.EvidenceIds.Contains(explanation.EvidenceId);
        var droppedUnknownExplanations = claim.Explanations.Any(explanation => !Keep(explanation));
        if (droppedUnknownExplanations)
        {
            claim = new ResolutionClaim(
                claim.Category,
                claim.Cause,
                claim.Confidence,
                claim.EvidenceIds,
                claim.CounterEvidence,
                claim.Actions,
                claim.Claims,
                claim.Explanations.Where(Keep).ToArray(),
                claim.EvidenceMatrix);
        }

        var failures = new List<string>();
        if (claim.Category is not null && claim.Cause is null)
        {
            failures.Add("category-without-cause");
        }

        if (claim.Category is null && claim.Cause is not null)
        {
            failures.Add("cause-without-category");
        }

        if (claim.Category is not null && claim.Confidence is null)
        {
            failures.Add("confidence-required");
        }
        else if (claim.Confidence is double confidence && (confidence < 0d || confidence > 1d || double.IsNaN(confidence)))
        {
            failures.Add("confidence-out-of-range");
        }

        if (claim.Category is not null && claim.EvidenceIds.Count == 0)
        {
            failures.Add("category-without-evidence");
        }

        foreach (var evidenceId in claim.EvidenceIds)
        {
            if (!known.ContainsKey(evidenceId))
            {
                failures.Add("unknown-evidence-id:" + evidenceId);
            }
        }

        foreach (var statement in claim.Claims)
        {
            if (statement.EvidenceIds.Count == 0)
            {
                failures.Add("uncited-claim");
            }

            foreach (var evidenceId in statement.EvidenceIds)
            {
                if (!known.ContainsKey(evidenceId))
                {
                    failures.Add("unknown-claim-evidence-id:" + evidenceId);
                }
            }
        }

        if (claim.CounterEvidence.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add("blank-counter-evidence");
        }

        if (claim.Actions.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add("blank-action");
        }

        foreach (var explanation in claim.Explanations)
        {
            if (!known.ContainsKey(explanation.EvidenceId))
            {
                failures.Add("why-it-matters-unknown:" + explanation.EvidenceId);
            }
            else if (!claim.EvidenceIds.Contains(explanation.EvidenceId))
            {
                failures.Add("why-it-matters-not-cited:" + explanation.EvidenceId);
            }
        }

        foreach (var entry in claim.EvidenceMatrix)
        {
            if (!known.ContainsKey(entry.EvidenceId))
            {
                failures.Add("matrix-unknown-evidence-id:" + entry.EvidenceId);
            }
        }

        if (failures.Count > 0)
        {
            return new GroundingResult(false, failures);
        }

        var repaired = droppedUnknownExplanations;
        var explanations = RepairExplanations(claim, known, catalog, ref repaired);
        var matrix = RepairMatrix(claim, known, catalog, ref repaired);
        if (!CoversAllCategories(matrix))
        {
            failures.Add("matrix-incomplete");
            return new GroundingResult(false, failures);
        }

        foreach (var explanation in explanations)
        {
            if (!PlainSentence.IsValid(explanation.WhyItMatters))
            {
                failures.Add("why-it-matters-invalid:" + explanation.EvidenceId);
            }
        }

        if (failures.Count > 0)
        {
            return new GroundingResult(false, failures);
        }

        if (!repaired)
        {
            return new GroundingResult(true, [], claim, false);
        }

        var replacement = new ResolutionClaim(
            claim.Category,
            claim.Cause,
            claim.Confidence,
            claim.EvidenceIds,
            claim.CounterEvidence,
            claim.Actions,
            claim.Claims,
            explanations,
            matrix);
        return new GroundingResult(true, [], replacement, true);
    }

    private static List<CitedEvidence> RepairExplanations(
        ResolutionClaim claim,
        Dictionary<EvidenceId, EvidenceItem> known,
        WhyItMattersCatalog catalog,
        ref bool repaired)
    {
        var result = new List<CitedEvidence>();
        foreach (var evidenceId in claim.EvidenceIds.Distinct())
        {
            if (!known.TryGetValue(evidenceId, out var item))
            {
                continue;
            }

            var supplied = claim.Explanations.FirstOrDefault(explanation => explanation.EvidenceId == evidenceId);
            if (supplied is not null && PlainSentence.IsValid(supplied.WhyItMatters) && SignalOk(item, supplied.SignalId))
            {
                result.Add(supplied);
                continue;
            }

            repaired = true;
            result.Add(catalog.ExplainItem(item));
        }

        return result;
    }

    private static bool SignalOk(EvidenceItem item, string? signalId)
    {
        var sensor = item.Source is EvidenceSource.Telemetry or EvidenceSource.Spc;
        if (!sensor)
        {
            return string.IsNullOrWhiteSpace(signalId);
        }

        if (string.IsNullOrWhiteSpace(signalId))
        {
            return false;
        }

        var trimmed = signalId.Trim();
        return trimmed.Length is >= 1 and <= 80 && !trimmed.Any(char.IsWhiteSpace);
    }

    private static List<EvidenceMatrixEntry> RepairMatrix(
        ResolutionClaim claim,
        Dictionary<EvidenceId, EvidenceItem> known,
        WhyItMattersCatalog catalog,
        ref bool repaired)
    {
        if (MatrixIsComplete(claim.EvidenceMatrix, known))
        {
            var kept = new List<EvidenceMatrixEntry>(claim.EvidenceMatrix.Count);
            foreach (var entry in claim.EvidenceMatrix)
            {
                if (string.IsNullOrWhiteSpace(entry.WhyItMatters) || PlainSentence.IsValid(entry.WhyItMatters))
                {
                    kept.Add(entry);
                    continue;
                }

                repaired = true;
                var replacement = entry.Stance == EvidenceStance.Supports && known.TryGetValue(entry.EvidenceId, out var item)
                    ? catalog.ExplainItem(item).WhyItMatters
                    : EvidenceMatrixBuilder.StanceText(entry.Stance, entry.Category);
                kept.Add(new EvidenceMatrixEntry(entry.Category, entry.EvidenceId, entry.Stance, replacement));
            }

            return kept;
        }

        repaired = true;
        var items = known.Values.ToArray();
        return EvidenceMatrixBuilder.FromItems(items, SignalShape.Unknown, claim.Category, catalog).ToList();
    }

    private static bool MatrixIsComplete(IReadOnlyList<EvidenceMatrixEntry> matrix, Dictionary<EvidenceId, EvidenceItem> known)
    {
        if (!CoversAllCategories(matrix))
        {
            return false;
        }

        foreach (var entry in matrix)
        {
            if (!known.ContainsKey(entry.EvidenceId))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CoversAllCategories(IReadOnlyList<EvidenceMatrixEntry> matrix)
    {
        var seen = new HashSet<RootCauseCategory>();
        foreach (var entry in matrix)
        {
            if (Enum.IsDefined(entry.Category))
            {
                seen.Add(entry.Category);
            }
        }

        return seen.Count == 4
            && seen.Contains(RootCauseCategory.EngineeringChange)
            && seen.Contains(RootCauseCategory.EquipmentCalibrationOrAlignment)
            && seen.Contains(RootCauseCategory.SupplyChainMaterial)
            && seen.Contains(RootCauseCategory.ProcessDrift);
    }
}
