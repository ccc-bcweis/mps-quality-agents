// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Evidence;
using Mps.Agents.Impact;
using Mps.Agents.Ranking;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Agents.Explain;

/// <summary>
/// One stance for each root-cause class. Every evidence id comes from the pack or the request.
/// </summary>
public static class EvidenceMatrixBuilder
{
    private static readonly RootCauseCategory[] Categories =
    [
        RootCauseCategory.EngineeringChange,
        RootCauseCategory.EquipmentCalibrationOrAlignment,
        RootCauseCategory.SupplyChainMaterial,
        RootCauseCategory.ProcessDrift,
    ];

    public static IReadOnlyList<EvidenceMatrixEntry> FromPack(EvidencePack pack, RootCauseCategory? chosen, WhyItMattersCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return FromItems(pack.Items, pack.SignalShape, chosen, catalog);
    }

    public static IReadOnlyList<EvidenceMatrixEntry> FromItems(
        IReadOnlyList<EvidenceItem> items,
        SignalShape shape,
        RootCauseCategory? chosen,
        WhyItMattersCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(catalog);
        if (items.Count == 0)
        {
            return [];
        }

        var engineering = Ids(items, item => item.Source == EvidenceSource.Plm);
        var equipment = Ids(items, item => item.Source == EvidenceSource.Cmms);
        var material = Ids(items, item => item.Source == EvidenceSource.Erp);
        var signal = Ids(items, item => item.Source is EvidenceSource.Telemetry or EvidenceSource.Spc);
        var fallback = items[0].Id;
        var entries = new List<EvidenceMatrixEntry>(Categories.Length);
        foreach (var category in Categories)
        {
            var own = Own(category, engineering, equipment, material, signal, shape == SignalShape.Ramp, chosen);
            var id = Pick(category, own, signal, fallback);
            var stance = Stance(category, chosen, own);
            var why = stance == EvidenceStance.Supports
                ? catalog.ExplainItem(items.First(item => item.Id == id)).WhyItMatters
                : StanceText(stance, category);
            entries.Add(new EvidenceMatrixEntry(category, id, stance, why));
        }

        return entries;
    }

    public static IReadOnlyList<EvidenceMatrixEntry> FromRequest(
        ResolutionRequest request,
        RootCauseCategory chosen,
        IReadOnlyList<EvidenceId> cited,
        WhyItMattersCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(cited);
        ArgumentNullException.ThrowIfNull(catalog);
        if (cited.Count == 0)
        {
            return [];
        }

        var engineering = Parse(request.EcoEvidenceIds);
        var equipment = Parse(request.OverdueEvidenceIds);
        equipment.AddRange(Parse(request.AlignmentEvidenceIds));
        equipment.AddRange(Parse(request.PmEvidenceIds));
        var material = Parse(request.LotEvidenceIds);
        var signal = Parse(request.SignalEvidenceIds);
        var ramp = string.Equals(request.SignalShape, "ramp", StringComparison.OrdinalIgnoreCase);
        var fallback = cited[0];
        var entries = new List<EvidenceMatrixEntry>(Categories.Length);
        foreach (var category in Categories)
        {
            var own = Own(category, engineering, equipment, material, signal, ramp, chosen);
            var id = Pick(category, own, signal, fallback);
            var stance = Stance(category, chosen, own);
            string why;
            if (stance == EvidenceStance.Supports)
            {
                var explained = CitedEvidenceFactory.ForRequest(request, [id], catalog);
                why = explained.Count == 0 ? WhyItMattersCatalog.GenericSentence : explained[0].WhyItMatters;
            }
            else
            {
                why = StanceText(stance, category);
            }

            entries.Add(new EvidenceMatrixEntry(category, id, stance, why));
        }

        return entries;
    }

    public static string StanceText(EvidenceStance stance, RootCauseCategory category)
    {
        return (stance, category) switch
        {
            (EvidenceStance.Neutral, RootCauseCategory.EngineeringChange) =>
                "An engineering change is in the window, but it is not the selected cause.",
            (EvidenceStance.Neutral, RootCauseCategory.EquipmentCalibrationOrAlignment) =>
                "Maintenance or calibration is in the window, but it is not the selected cause.",
            (EvidenceStance.Neutral, RootCauseCategory.SupplyChainMaterial) =>
                "A material change is in the window, but it is not the selected cause.",
            (EvidenceStance.Neutral, RootCauseCategory.ProcessDrift) =>
                "A slow drift is in the window, but it is not the selected cause.",
            (EvidenceStance.RulesOut, RootCauseCategory.EngineeringChange) =>
                "No engineering change record is in this evidence, so that class does not explain the incident.",
            (EvidenceStance.RulesOut, RootCauseCategory.EquipmentCalibrationOrAlignment) =>
                "No calibration or alignment record is in this evidence, so that class does not explain the incident.",
            (EvidenceStance.RulesOut, RootCauseCategory.SupplyChainMaterial) =>
                "No material lot or supplier notice is in this evidence, so that class does not explain the incident.",
            (EvidenceStance.RulesOut, RootCauseCategory.ProcessDrift) =>
                "The signal is not a slow drift, so process drift does not explain the incident.",
            _ => WhyItMattersCatalog.GenericSentence,
        };
    }

    private static List<EvidenceId> Own(
        RootCauseCategory category,
        IReadOnlyList<EvidenceId> engineering,
        IReadOnlyList<EvidenceId> equipment,
        IReadOnlyList<EvidenceId> material,
        IReadOnlyList<EvidenceId> signal,
        bool ramp,
        RootCauseCategory? chosen)
    {
        return category switch
        {
            RootCauseCategory.EngineeringChange => engineering.ToList(),
            RootCauseCategory.EquipmentCalibrationOrAlignment => equipment.ToList(),
            RootCauseCategory.SupplyChainMaterial => material.ToList(),
            RootCauseCategory.ProcessDrift when ramp || chosen == RootCauseCategory.ProcessDrift => signal.ToList(),
            _ => [],
        };
    }

    private static EvidenceId Pick(
        RootCauseCategory category,
        List<EvidenceId> own,
        List<EvidenceId> signal,
        EvidenceId fallback)
    {
        if (own.Count > 0)
        {
            return own[0];
        }

        if (category == RootCauseCategory.ProcessDrift && signal.Count > 0)
        {
            return signal[0];
        }

        return fallback;
    }

    private static EvidenceStance Stance(RootCauseCategory category, RootCauseCategory? chosen, List<EvidenceId> own)
    {
        if (chosen == category)
        {
            return EvidenceStance.Supports;
        }

        return own.Count > 0 ? EvidenceStance.Neutral : EvidenceStance.RulesOut;
    }

    private static List<EvidenceId> Ids(IReadOnlyList<EvidenceItem> items, Func<EvidenceItem, bool> predicate)
    {
        return items.Where(predicate).Select(item => item.Id).Distinct().ToList();
    }

    private static List<EvidenceId> Parse(IReadOnlyList<string>? values)
    {
        var ids = new List<EvidenceId>();
        if (values is null)
        {
            return ids;
        }

        foreach (var value in values)
        {
            if (Guid.TryParse(value, out var parsed) && parsed != Guid.Empty)
            {
                ids.Add(new EvidenceId(parsed));
            }
        }

        return ids;
    }
}
