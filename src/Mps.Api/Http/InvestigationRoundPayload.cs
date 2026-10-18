// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json.Serialization;

using Mps.Application.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Investigations;

namespace Mps.Api.Http;

/// <summary>One record considered and dismissed, as the lifecycle event carries it. The id field is id, not evidenceId.</summary>
public sealed record RuledOutEvent(Guid Id, string Reason, string Source);

/// <summary>
/// The investigation step written when a round recommends or escalates.
/// ruledOut sits beside the cited ids. A guard hit names the offending id and the list it was in.
/// </summary>
public sealed record InvestigationRoundPayload(
    string Kind,
    Guid InvestigationId,
    IReadOnlyList<Guid> EvidenceIds,
    IReadOnlyList<RuledOutEvent> RuledOut,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? OffendingId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? List = null)
{
    public static InvestigationRoundPayload For(InvestigationSnapshot snapshot, InvestigationNarrative narrative)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(narrative);
        return snapshot.Stage == InvestigationStage.Escalated
            ? Escalated(snapshot, narrative)
            : Recommendation(snapshot);
    }

    private static InvestigationRoundPayload Recommendation(InvestigationSnapshot snapshot)
    {
        var evidenceIds = new List<Guid>();
        var seenEvidence = new HashSet<Guid>();
        var ruledOut = new List<RuledOutEvent>();
        var seenRuledOut = new HashSet<Guid>();
        foreach (var hypothesis in snapshot.Hypotheses)
        {
            foreach (var id in hypothesis.EvidenceIds)
            {
                if (seenEvidence.Add(id.Value))
                {
                    evidenceIds.Add(id.Value);
                }
            }

            foreach (var entry in hypothesis.RuledOut)
            {
                if (seenRuledOut.Add(entry.Id.Value))
                {
                    ruledOut.Add(new RuledOutEvent(entry.Id.Value, entry.Reason, SourceName(entry.Source)));
                }
            }
        }

        return new InvestigationRoundPayload("recommendation", snapshot.Id.Value, evidenceIds, ruledOut);
    }

    private static InvestigationRoundPayload Escalated(InvestigationSnapshot snapshot, InvestigationNarrative narrative)
    {
        var ruledOut = new List<RuledOutEvent>();
        var seen = new HashSet<Guid>();
        foreach (var entry in narrative.RecordRuledOut)
        {
            if (seen.Add(entry.Id.Value))
            {
                ruledOut.Add(new RuledOutEvent(entry.Id.Value, entry.Reason, "record"));
            }
        }

        if (narrative.Rejected is { } rejected)
        {
            foreach (var item in rejected.RuledOutIds)
            {
                if (seen.Add(item.EvidenceId))
                {
                    ruledOut.Add(new RuledOutEvent(item.EvidenceId, item.Reason, "agent"));
                }
            }
        }

        var hit = narrative.GuardHit;
        return new InvestigationRoundPayload(
            "escalated",
            snapshot.Id.Value,
            narrative.Rejected?.EvidenceIds.ToArray() ?? [],
            ruledOut,
            hit?.EvidenceId,
            hit?.List);
    }

    private static string SourceName(RuledOutSource source) => source switch
    {
        RuledOutSource.Record => "record",
        RuledOutSource.Agent => "agent",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Ruled-out source is record or agent."),
    };
}
