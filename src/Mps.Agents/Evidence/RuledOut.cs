// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Enterprise;

namespace Mps.Agents.Evidence;

/// <summary>
/// Records the plant data already rules out. The phrases and the reason template live in data/metadata/ruled-out.yaml.
/// The check reads only the record, never the answer key.
/// </summary>
public static class RuledOut
{
    public const string Prefix = "Ruled out: ";

    public static string? Reason(EcoRecord eco)
    {
        ArgumentNullException.ThrowIfNull(eco);
        return Fill("eco", eco.Title + " " + eco.ChangeSummary, eco.EcoId, null, null, null, null);
    }

    public static string? Reason(SupplierNotice notice)
    {
        ArgumentNullException.ThrowIfNull(notice);
        return Fill("supplierNotice", notice.Summary, null, notice.LotId, notice.NoticeId, null, null);
    }

    public static string? Reason(WorkOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return Fill("workOrder", order.Summary, null, null, null, order.WorkOrderId, order.Kind);
    }

    /// <summary>The record's own title. Ruled-out records are named on the typed list, not by a title prefix.</summary>
    public static string Title(string title, string? reason) => title;

    /// <summary>One sentence per ruled-out record in the pack, for the hypothesis counter-evidence.</summary>
    public static IReadOnlyList<string> All(EvidencePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        if (pack.RuledOut.Count > 0)
        {
            return pack.RuledOut.Select(record => record.Reason).Distinct(StringComparer.Ordinal).ToArray();
        }

        return pack.Ecos.Select(Reason)
            .Concat(pack.SupplierNotices.Select(Reason))
            .Concat(pack.WorkOrders.Select(Reason))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string? Fill(string source, string text, string? ecoId, string? lotId, string? noticeId, string? workOrderId, string? kind)
    {
        var rule = RuledOutRules.Shared.Match(source, text);
        return rule?.Fill(ecoId, lotId, noticeId, workOrderId, kind);
    }
}
