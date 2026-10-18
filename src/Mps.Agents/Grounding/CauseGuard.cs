// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Evidence;
using Mps.Agents.Impact;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Agents.Grounding;

/// <summary>
/// Checks a proposed cause against the evidence pack after grounding. A record that rules itself out may not be cited
/// as support or named as the cause of the class it would imply (naming it to dismiss a rival is fine), and the shape
/// the agent reports in its structured "shape" field must be the shape measured from the readings. The cause prose is not scanned for shape words. A failed check escalates to a person.
/// </summary>
public static class CauseGuard
{
    /// <summary>Null when the claim passes, otherwise a plain escalation reason.</summary>
    public static string? Check(ResolutionClaim claim, EvidencePack pack, SignalShapeCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(pack);
        foreach (var record in RuledOutRecords(pack))
        {
            if (CitedAsSupport(claim, pack, record) || NamedAsTheChosenClass(claim, record))
            {
                return "The AI named " + record.Label + " as the cause, but that record says in its own words it is not the cause. A person should review it.";
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

    /// <summary>A pack ruled-out record: its evidence id, the label a person would recognise, and the names it goes by.</summary>
    private sealed record RuledOutHit(EvidenceId EvidenceId, string Label, IReadOnlyList<string> Names, RootCauseCategory Class);

    private static IEnumerable<RuledOutHit> RuledOutRecords(EvidencePack pack)
    {
        foreach (var record in pack.RuledOut)
        {
            var names = Names(pack, record);
            var label = names.Count > 0 ? names[0] : record.Id.ToString();
            yield return new RuledOutHit(record.Id, label, names, record.Class);
        }
    }

    private static List<string> Names(EvidencePack pack, RuledOutRecord record)
    {
        var names = new List<string>();
        var item = pack.Items.FirstOrDefault(candidate => candidate.Id == record.Id);
        Add(names, item?.ExternalRef);
        foreach (var notice in pack.SupplierNotices)
        {
            if (!Linked(record.Id, item?.ExternalRef, EvidenceIds.Create(EvidenceSource.Erp, notice.NoticeId), notice.NoticeId))
            {
                continue;
            }

            Add(names, notice.NoticeId);
            Add(names, notice.LotId);
        }

        foreach (var eco in pack.Ecos)
        {
            if (Linked(record.Id, item?.ExternalRef, EvidenceIds.Create(EvidenceSource.Plm, eco.EcoId), eco.EcoId))
            {
                Add(names, eco.EcoId);
            }
        }

        foreach (var order in pack.WorkOrders)
        {
            if (Linked(record.Id, item?.ExternalRef, EvidenceIds.Create(EvidenceSource.Cmms, order.WorkOrderId), order.WorkOrderId))
            {
                Add(names, order.WorkOrderId);
            }
        }

        foreach (var calibration in pack.Calibrations)
        {
            if (Linked(record.Id, item?.ExternalRef, EvidenceIds.Create(EvidenceSource.Cmms, calibration.CalibrationId), calibration.CalibrationId))
            {
                Add(names, calibration.CalibrationId);
            }
        }

        foreach (var lot in pack.MaterialLots)
        {
            if (Linked(record.Id, item?.ExternalRef, EvidenceIds.Create(EvidenceSource.Erp, lot.LotId), lot.LotId))
            {
                Add(names, lot.LotId);
            }
        }

        if (names.Count == 0)
        {
            Add(names, item?.Title);
        }

        return names;
    }

    private static bool Linked(EvidenceId id, string? externalRef, EvidenceId recordId, string businessId) =>
        id == recordId || string.Equals(externalRef, businessId, StringComparison.OrdinalIgnoreCase);

    private static void Add(List<string> names, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var trimmed = name.Trim();
        if (names.Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        names.Add(trimmed);
    }

    /// <summary>The record's evidence id is in the answer's supporting ids, or a cited item goes by one of its names.</summary>
    private static bool CitedAsSupport(ResolutionClaim claim, EvidencePack pack, RuledOutHit record)
    {
        if (claim.EvidenceIds.Contains(record.EvidenceId))
        {
            return true;
        }

        var cited = new HashSet<EvidenceId>(claim.EvidenceIds);
        return pack.Items.Any(item => cited.Contains(item.Id)
            && item.ExternalRef is { } reference
            && record.Names.Any(name => string.Equals(reference, name, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// The answer chose the class this record would imply and its cause names the record. Naming a ruled-out record
    /// while ranking another class is the answer dismissing a rival, which the prompt asks for, so it passes.
    /// </summary>
    private static bool NamedAsTheChosenClass(ResolutionClaim claim, RuledOutHit record) =>
        claim.Category == record.Class
        && claim.Cause is { } cause
        && record.Names.Any(name => cause.Contains(name, StringComparison.OrdinalIgnoreCase));
}
