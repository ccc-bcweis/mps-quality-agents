// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.RegularExpressions;

using Mps.Agents.Evidence;
using Mps.Agents.Impact;

namespace Mps.Agents.Grounding;

/// <summary>
/// Checks a proposed cause against the evidence pack after grounding. The cause may not name a record that rules
/// itself out, and may not describe a signal shape the readings do not show. A failed check escalates to a person.
/// </summary>
public static class CauseGuard
{
    private static readonly (string Pattern, SignalShape Shape)[] ShapeWords =
    [
        (@"\boscillat\w*|\bswing\w*", SignalShape.Oscillation),
        (@"\bstep(?:s|ped)?\b|\bsudden\b", SignalShape.Step),
        (@"\bgradual\w*|(?<!\bprocess[\s-])\bdrift\w*|\bramp\w*|\bris(?:e|ing)\b|\bcreep\w*", SignalShape.Ramp),
        (@"\bspike\w*|\bintermittent\w*", SignalShape.Intermittent),
    ];

    /// <summary>The words the cause check reads as each shape, for the retry note.</summary>
    private static readonly (SignalShape Shape, string Words)[] ShapeVocabulary =
    [
        (SignalShape.Oscillation, "oscillation, oscillating, swing"),
        (SignalShape.Step, "step, stepped, sudden"),
        (SignalShape.Ramp, "gradual, drift, drifting, ramp, rise, rising, creep"),
        (SignalShape.Intermittent, "spike, spikes, intermittent"),
    ];

    /// <summary>Null when the cause passes, otherwise a plain escalation reason.</summary>
    public static string? Check(string cause, EvidencePack pack)
    {
        ArgumentNullException.ThrowIfNull(cause);
        ArgumentNullException.ThrowIfNull(pack);
        foreach (var id in RuledOutIds(pack))
        {
            if (cause.Contains(id, StringComparison.OrdinalIgnoreCase))
            {
                return "The AI named " + id + " as the cause, but that record says in its own words it is not the cause. A person should review it.";
            }
        }

        return ShapeConflict(cause, pack) is { } word
            ? pack.SignalShape == SignalShape.Unknown
                ? "The AI described the signal as \"" + word + "\", but the readings do not show a clear shape yet. Request more evidence once more readings are in."
                : "The AI described the signal as \"" + word + "\", but the readings show " + EvidencePackFactory.ShapeWords(pack.SignalShape) + "."
            : null;
    }

    /// <summary>The first shape word in the cause that the measured shape contradicts, or null.</summary>
    public static string? ShapeConflict(string cause, EvidencePack pack)
    {
        ArgumentNullException.ThrowIfNull(cause);
        ArgumentNullException.ThrowIfNull(pack);
        foreach (var (pattern, shape) in ShapeWords)
        {
            var match = Regex.Match(cause, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success && shape != pack.SignalShape)
            {
                return match.Value;
            }
        }

        return null;
    }

    /// <summary>The note sent back to the model once when its cause names the wrong shape.</summary>
    public static string ShapeFeedback(string word, EvidencePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var forbidden = string.Join(", ", ShapeVocabulary.Where(entry => entry.Shape != pack.SignalShape).Select(entry => entry.Words));
        var avoid = " In cause, do not use any of these words, not even to say the signal is not like that: " + forbidden
            + ". The class name processDrift may still be ranked, but write it as \"process drift\".";
        return pack.SignalShape == SignalShape.Unknown
            ? "Your previous cause described the signal as \"" + word + "\", but the readings do not show a clear shape yet. Do not describe a shape; rank the cause from the records, or escalate." + avoid
            : "Your previous cause described the signal as \"" + word + "\", but the measured shape of the readings since the first rule break is " + EvidencePackFactory.ShapeWords(pack.SignalShape)
                + ". Describe the signal only as " + EvidencePackFactory.ShapeWords(pack.SignalShape) + " and re-rank the cause with that shape, or escalate if no record fits it." + avoid;
    }

    private static IEnumerable<string> RuledOutIds(EvidencePack pack) =>
        pack.Ecos.Where(eco => RuledOut.Reason(eco) is not null).Select(eco => eco.EcoId)
            .Concat(pack.SupplierNotices.Where(notice => RuledOut.Reason(notice) is not null).SelectMany(notice => new[] { notice.NoticeId, notice.LotId }))
            .Concat(pack.WorkOrders.Where(order => RuledOut.Reason(order) is not null).Select(order => order.WorkOrderId));
}
