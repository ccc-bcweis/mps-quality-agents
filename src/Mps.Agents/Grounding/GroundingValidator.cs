// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Evidence;
using Mps.Domain.Identity;

namespace Mps.Agents.Grounding;

/// <summary>
/// Rejects a category, cause, or sentence that is not tied to evidence ids already in the pack.
/// </summary>
public static class GroundingValidator
{
    public static GroundingResult Validate(ResolutionClaim claim, IReadOnlyList<EvidenceItem> pack)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(pack);

        var known = pack.Select(item => item.Id).ToHashSet();
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
            if (!known.Contains(evidenceId))
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
                if (!known.Contains(evidenceId))
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

        return new GroundingResult(failures.Count == 0, failures);
    }
}
