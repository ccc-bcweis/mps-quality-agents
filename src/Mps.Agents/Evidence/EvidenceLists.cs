// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Grounding;
using Mps.Agents.Ranking;
using Mps.Domain.Enterprise;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Agents.Evidence;

/// <summary>
/// Puts each eco, work order, calibration, material lot, supplier notice, alignment, and user-evidence record in
/// exactly one of candidates or ruled out. Signal items are left out of both.
/// </summary>
internal static class EvidenceLists
{
    public static (Candidate[] Candidates, RuledOutRecord[] RuledOut) Partition(
        IReadOnlyList<EcoRecord> ecos,
        IReadOnlyList<WorkOrder> workOrders,
        IReadOnlyList<CalibrationRecord> calibrations,
        IReadOnlyList<MaterialLot> materialLots,
        IReadOnlyList<SupplierNotice> notices,
        IReadOnlyList<EvidenceItem> userEvidence)
    {
        var candidates = new List<Candidate>();
        var ruledOut = new List<RuledOutRecord>();
        var rules = RuledOutRules.Shared;
        foreach (var eco in ecos)
        {
            Place(
                candidates,
                ruledOut,
                rules,
                EvidenceIds.Create(EvidenceSource.Plm, eco.EcoId),
                "eco",
                "plm",
                eco.Title + " " + eco.ChangeSummary,
                eco.EcoId,
                null,
                null,
                null,
                null);
        }

        foreach (var order in workOrders)
        {
            var alignment = IsAlignment(order);
            Place(
                candidates,
                ruledOut,
                rules,
                EvidenceIds.Create(EvidenceSource.Cmms, order.WorkOrderId),
                alignment ? "alignment" : "workOrder",
                "cmms",
                order.Summary,
                null,
                null,
                null,
                order.WorkOrderId,
                order.Kind,
                ruleSource: "workOrder");
        }

        foreach (var calibration in calibrations)
        {
            var text = "Calibration " + calibration.CalibrationId + " for " + calibration.Instrument + " on " + calibration.ToolId.Value
                + (calibration.IsOverdue ? ", overdue." : ", in date.");
            Place(
                candidates,
                ruledOut,
                rules,
                EvidenceIds.Create(EvidenceSource.Cmms, calibration.CalibrationId),
                "calibration",
                "cmms",
                text,
                null,
                null,
                null,
                calibration.CalibrationId,
                "calibration");
        }

        foreach (var lot in materialLots)
        {
            Place(
                candidates,
                ruledOut,
                rules,
                EvidenceIds.Create(EvidenceSource.Erp, lot.LotId),
                "materialLot",
                "erp",
                lot.MaterialCode + " " + lot.LotId + " " + lot.SupplierName + " " + (lot.NoticeId ?? string.Empty),
                null,
                lot.LotId,
                lot.NoticeId,
                null,
                null);
        }

        foreach (var notice in notices)
        {
            Place(
                candidates,
                ruledOut,
                rules,
                EvidenceIds.Create(EvidenceSource.Erp, notice.NoticeId),
                "supplierNotice",
                "erp",
                notice.Summary,
                null,
                notice.LotId,
                notice.NoticeId,
                null,
                null);
        }

        foreach (var item in userEvidence)
        {
            if (item.Source != EvidenceSource.UserProvided)
            {
                continue;
            }

            if (candidates.Any(candidate => candidate.Id == item.Id) || ruledOut.Any(record => record.Id == item.Id))
            {
                continue;
            }

            Place(
                candidates,
                ruledOut,
                rules,
                item.Id,
                "userEvidence",
                "userProvided",
                item.Title + " " + item.Excerpt,
                null,
                null,
                null,
                null,
                null);
        }

        return (candidates.ToArray(), ruledOut.ToArray());
    }

    private static void Place(
        List<Candidate> candidates,
        List<RuledOutRecord> ruledOut,
        RuledOutRules rules,
        EvidenceId id,
        string kind,
        string noteSource,
        string text,
        string? ecoId,
        string? lotId,
        string? noticeId,
        string? workOrderId,
        string? kindToken,
        string? ruleSource = null)
    {
        var rule = rules.Match(ruleSource ?? kind, text);
        if (rule is not null)
        {
            ruledOut.Add(new RuledOutRecord(id, rule.RulesOut, rule.Fill(ecoId, lotId, noticeId, workOrderId, kindToken), rule.Id));
            return;
        }

        candidates.Add(new Candidate(id, ClassFor(kind, noteSource, text), kind));
    }

    private static RootCauseCategory ClassFor(string kind, string noteSource, string text)
    {
        var note = new EvidenceNote { Source = noteSource, Title = text, Detail = text };
        foreach (var rule in CategoryEvidenceRules.Shared.Rules)
        {
            if (rule.Matches(note))
            {
                return rule.Implies;
            }
        }

        if (string.Equals(kind, "userEvidence", StringComparison.Ordinal))
        {
            foreach (var rule in CategoryEvidenceRules.Shared.Rules)
            {
                if (rule.Sources.Count == 0)
                {
                    continue;
                }

                var probe = new EvidenceNote { Source = rule.Sources[0], Title = text, Detail = text };
                if (rule.Matches(probe))
                {
                    return rule.Implies;
                }
            }

            return RootCauseCategory.ProcessDrift;
        }

        return kind switch
        {
            "eco" => RootCauseCategory.EngineeringChange,
            "supplierNotice" => RootCauseCategory.SupplyChainMaterial,
            "materialLot" => RootCauseCategory.SupplyChainMaterial,
            _ => RootCauseCategory.EquipmentCalibrationOrAlignment,
        };
    }

    private static bool IsAlignment(WorkOrder order) =>
        order.Kind.Contains("align", StringComparison.OrdinalIgnoreCase);
}
