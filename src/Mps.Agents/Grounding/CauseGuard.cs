// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Evidence;
using Mps.Agents.Impact;

namespace Mps.Agents.Grounding;

/// <summary>
/// Checks a proposed cause against the evidence pack after grounding. The cause may not name a record that rules
/// itself out, and the shape the agent reports in its structured "shape" field must be the shape measured from the
/// readings. The cause prose is not scanned for shape words. A failed check escalates to a person.
/// </summary>
public static class CauseGuard
{
    /// <summary>Null when the claim passes, otherwise a plain escalation reason.</summary>
    public static string? Check(ResolutionClaim claim, EvidencePack pack, SignalShapeCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(pack);
        if (claim.Cause is { } cause)
        {
            foreach (var id in RuledOutIds(pack))
            {
                if (cause.Contains(id, StringComparison.OrdinalIgnoreCase))
                {
                    return "The AI named " + id + " as the cause, but that record says in its own words it is not the cause. A person should review it.";
                }
            }
        }

        return ShapeMismatch(claim.Shape, pack, catalog);
    }

    /// <summary>Null when the reported shape is the measured shape, otherwise a plain escalation reason.</summary>
    public static string? ShapeMismatch(string? reported, EvidencePack pack, SignalShapeCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(pack);
        catalog ??= SignalShapeCatalog.Shared;
        var measured = catalog.Of(pack.SignalShape);
        if (string.IsNullOrWhiteSpace(reported))
        {
            return "The AI did not report the signal shape; the readings show " + measured.Description + ".";
        }

        if (!catalog.TryParse(reported, out var entry))
        {
            return "The AI reported the signal shape as \"" + reported.Trim() + "\", which is not a known shape; the readings show " + measured.Description + ".";
        }

        if (entry.Shape == pack.SignalShape)
        {
            return null;
        }

        return pack.SignalShape == SignalShape.Unknown
            ? "The AI described the signal as " + entry.Description + ", but the readings do not show a clear shape yet. Request more evidence once more readings are in."
            : "The AI described the signal as " + entry.Description + ", but the readings show " + measured.Description + ".";
    }

    /// <summary>The note sent back to the model once when its reported shape is not the measured shape.</summary>
    public static string ShapeFeedback(string? reported, EvidencePack pack, SignalShapeCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(pack);
        catalog ??= SignalShapeCatalog.Shared;
        var measured = catalog.Of(pack.SignalShape);
        var previous = string.IsNullOrWhiteSpace(reported)
            ? "Your previous answer did not set shape"
            : "Your previous answer set shape to \"" + reported.Trim() + "\"";
        var forbidden = string.Join(", ", catalog.Entries.Where(entry => entry.Shape != pack.SignalShape).SelectMany(entry => entry.Words));
        var avoid = forbidden.Length == 0
            ? string.Empty
            : " In cause, do not use any of these words, not even to say the signal is not like that: " + forbidden
                + ". The class name processDrift may still be ranked, but write it as \"process drift\".";
        return pack.SignalShape == SignalShape.Unknown
            ? previous + ", but the readings do not show a clear shape yet. Set shape to \"" + measured.Id + "\" and do not describe a shape; rank the cause from the records, or escalate." + avoid
            : previous + ", but the measured shape of the readings since the first rule break is " + measured.Description
                + ". Set shape to \"" + measured.Id + "\", describe the signal only as " + measured.Description + " and re-rank the cause with that shape, or escalate if no record fits it." + avoid;
    }

    private static IEnumerable<string> RuledOutIds(EvidencePack pack) =>
        pack.Ecos.Where(eco => RuledOut.Reason(eco) is not null).Select(eco => eco.EcoId)
            .Concat(pack.SupplierNotices.Where(notice => RuledOut.Reason(notice) is not null).SelectMany(notice => new[] { notice.NoticeId, notice.LotId }))
            .Concat(pack.WorkOrders.Where(order => RuledOut.Reason(order) is not null).Select(order => order.WorkOrderId));
}
