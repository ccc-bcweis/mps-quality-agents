// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

using Mps.Domain;
using Mps.Domain.Evidence;
using Mps.Domain.Identity;

namespace Mps.Domain.Investigations;

/// <summary>
/// The evidence loop (Oct 17). Each run of the agents on a ticket is a round. A round that is waiting for a person
/// (a recommendation or an escalation, with no decision yet) takes one decision: approve or reject ends the loop;
/// adding evidence closes the round as <see cref="InvestigationStage.Superseded"/> and the next round re-runs the agents
/// with every record a person has added so far. A closed round is read-only history.
/// </summary>
public static class InvestigationRounds
{
    /// <summary>Prefix of the record id shown for user-provided evidence ("USER-R1").</summary>
    public const string ExternalRefPrefix = "USER-R";

    /// <summary>Longest evidence text a person can add, in characters.</summary>
    public const int MaxEvidenceLength = 4000;

    /// <summary>True while the round waits for a person and has no decision on file.</summary>
    public static bool IsOpenForDecision(InvestigationSnapshot round)
    {
        ArgumentNullException.ThrowIfNull(round);
        return round.Decision is null && round.Stage is
            InvestigationStage.RecommendationReady
            or InvestigationStage.AwaitingApproval
            or InvestigationStage.Escalated;
    }

    /// <summary>True when a person can add evidence to this round and open the next one.</summary>
    public static bool CanAddEvidence(InvestigationSnapshot round, int maxRounds) =>
        IsOpenForDecision(round) && round.Round < maxRounds;

    /// <summary>
    /// Closes the round because a person added evidence. The added record is kept on the closed round (its id says which
    /// round it closed) so the next round, or a later plain start, carries it. Throws when the round is not open or the cap is reached.
    /// </summary>
    public static InvestigationSnapshot Supersede(InvestigationSnapshot round, DecisionRecord decision, int maxRounds, EvidenceItem? added = null)
    {
        ArgumentNullException.ThrowIfNull(round);
        ArgumentNullException.ThrowIfNull(decision);
        if (added is not null && added.Source != EvidenceSource.UserProvided)
        {
            throw new DomainRuleException("Only user-provided evidence closes a round.");
        }

        if (!IsOpenForDecision(round))
        {
            throw new DomainRuleException($"Round {round.Round} is not waiting for a decision ({round.Stage}).");
        }

        if (round.Round >= maxRounds)
        {
            throw new DomainRuleException(CapReached(maxRounds));
        }

        var closed = round.With(InvestigationStage.Superseded, round.EscalationReason, decision);
        return added is null ? closed : closed.WithEvidence(closed.Evidence.Append(added).ToArray());
    }

    /// <summary>Plain words when the round cap stops another round.</summary>
    public static string CapReached(int maxRounds) =>
        "This ticket has reached " + maxRounds.ToString(CultureInfo.InvariantCulture)
        + " rounds, the most allowed. Approve, override or reject this round.";

    /// <summary>A record a person typed in. It is marked user-provided, names who added it and when, and which round it opened.</summary>
    public static EvidenceItem UserEvidence(string text, string suppliedBy, DateTimeOffset at, string? toolId, int closedRound)
    {
        var body = RequiredText.Value(text, "Type the evidence to add.");
        if (body.Length > MaxEvidenceLength)
        {
            throw new DomainRuleException("The evidence text is too long.");
        }

        var who = RequiredText.Value(suppliedBy, "User-provided evidence names who supplied it.");
        var next = closedRound + 1;
        return new EvidenceItem(
            EvidenceId.New(),
            EvidenceSource.UserProvided,
            "Added by " + who + " for round " + next.ToString(CultureInfo.InvariantCulture),
            body,
            at,
            toolId,
            ExternalRefPrefix + closedRound.ToString(CultureInfo.InvariantCulture),
            suppliedBy: who);
    }

    /// <summary>Every user-provided record on a round, carried into the next round unchanged.</summary>
    public static IReadOnlyList<EvidenceItem> CarriedUserEvidence(InvestigationSnapshot round)
    {
        ArgumentNullException.ThrowIfNull(round);
        return round.Evidence.Where(item => item.Source == EvidenceSource.UserProvided).ToArray();
    }
}
