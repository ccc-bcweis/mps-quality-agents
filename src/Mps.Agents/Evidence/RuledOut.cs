// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Enterprise;

namespace Mps.Agents.Evidence;

/// <summary>
/// Records that say in their own words they cannot be the cause: a document-only change, a lot that matches its
/// specification, or an inspection where nothing was changed. The check reads only the record, never the answer key.
/// </summary>
public static class RuledOut
{
    public const string Prefix = "Ruled out: ";

    public static string? Reason(EcoRecord eco)
    {
        ArgumentNullException.ThrowIfNull(eco);
        var text = eco.Title + " " + eco.ChangeSummary;
        var documentOnly = Has(text, "document-only") || Has(text, "document only");
        var physical = Has(text, "replace") || Has(text, "install") || Has(text, "edge ring");
        return documentOnly && !physical ? eco.EcoId + " is a document-only change. Setpoints were not changed." : null;
    }

    public static string? Reason(SupplierNotice notice)
    {
        ArgumentNullException.ThrowIfNull(notice);
        return Has(notice.Summary, "matches the purchase specification") && !Has(notice.Summary, "out of spec")
            ? "Lot " + notice.LotId + " (" + notice.NoticeId + ") matches its specification."
            : null;
    }

    public static string? Reason(WorkOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return Has(order.Summary, "no parts changed") && Has(order.Summary, "no findings")
            ? "Work order " + order.WorkOrderId + " (" + order.Kind + ") was an inspection with no findings. Nothing was changed."
            : null;
    }

    public static string Title(string title, string? reason) =>
        reason is null || title.StartsWith(Prefix, StringComparison.Ordinal) ? title : Prefix + title;

    /// <summary>One sentence per ruled-out record in the pack, for the hypothesis counter-evidence.</summary>
    public static IReadOnlyList<string> All(EvidencePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return pack.Ecos.Select(Reason)
            .Concat(pack.SupplierNotices.Select(Reason))
            .Concat(pack.WorkOrders.Select(Reason))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool Has(string text, string phrase) => text.Contains(phrase, StringComparison.OrdinalIgnoreCase);
}
