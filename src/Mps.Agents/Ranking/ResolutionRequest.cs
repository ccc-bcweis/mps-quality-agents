// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Evidence;
using Mps.Agents.Explain;
using Mps.Agents.Impact;
using Mps.Domain.Enterprise;
using Mps.Domain.Evidence;
using Mps.Domain.Impact;

namespace Mps.Agents.Ranking;

/// <summary>
/// Features and citations the ranker uses. The resolution prompt carries this JSON.
/// </summary>
public sealed class ResolutionRequest
{
    public string ScopeKind { get; set; } = "tool";

    public string ScopeKey { get; set; } = string.Empty;

    public string SignalShape { get; set; } = "unknown";

    /// <summary>The shape ids the reply's "shape" field may use, from the signal shape catalog.</summary>
    public string[] ShapeOptions { get; set; } = [];

    public string[] EcoIds { get; set; } = [];

    public string[] EcoEvidenceIds { get; set; } = [];

    public string[] PmSummaries { get; set; } = [];

    public string[] OverdueInstruments { get; set; } = [];

    public string[] OverdueEvidenceIds { get; set; } = [];

    public string[] AlignmentSummaries { get; set; } = [];

    public string[] AlignmentEvidenceIds { get; set; } = [];

    public string[] LotIds { get; set; } = [];

    public string[] LotEvidenceIds { get; set; } = [];

    public string[] NoticeIds { get; set; } = [];

    public string[] SignalEvidenceIds { get; set; } = [];

    public string[] PmEvidenceIds { get; set; } = [];

    public EvidenceNote[] Notes { get; set; } = [];

    /// <summary>Set only on a retry: what was wrong with the previous answer for this same pack.</summary>
    public string? Feedback { get; set; }

    public static ResolutionRequest From(EvidencePack pack, ImpactFeatures features)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(features);
        var catalog = WhyItMattersCatalog.Shared;
        return new ResolutionRequest
        {
            ScopeKind = ScopeName(features.Kind),
            ScopeKey = features.Key,
            SignalShape = SignalShapeCatalog.Shared.Of(features.Shape).Id,
            ShapeOptions = [.. SignalShapeCatalog.Shared.Ids],
            EcoIds = pack.Ecos.Select(eco => eco.EcoId).ToArray(),
            EcoEvidenceIds = Ids(pack, pack.Ecos.Select(eco => eco.EcoId)),
            PmSummaries = pack.WorkOrders.Where(order => !IsAlignment(order)).Select(DescribeOrder).ToArray(),
            OverdueInstruments = pack.Calibrations.Where(record => record.IsOverdue).Select(record => record.Instrument).ToArray(),
            OverdueEvidenceIds = Ids(pack, pack.Calibrations.Where(record => record.IsOverdue).Select(record => record.CalibrationId)),
            AlignmentSummaries = pack.WorkOrders.Where(IsAlignment).Select(DescribeOrder).ToArray(),
            AlignmentEvidenceIds = Ids(pack, pack.WorkOrders.Where(IsAlignment).Select(order => order.WorkOrderId)),
            LotIds = pack.MaterialLots.Select(lot => lot.LotId).ToArray(),
            LotEvidenceIds = Ids(pack, pack.MaterialLots.Select(lot => lot.LotId)),
            NoticeIds = pack.SupplierNotices.Select(notice => notice.NoticeId).ToArray(),
            SignalEvidenceIds = pack.Items
                .Where(item => item.Source is EvidenceSource.Spc or EvidenceSource.Telemetry)
                .Select(item => item.Id.ToString())
                .ToArray(),
            PmEvidenceIds = Ids(pack, pack.WorkOrders.Where(order => !IsAlignment(order)).Select(order => order.WorkOrderId)),
            Notes = pack.Items.Select(item => new EvidenceNote
            {
                Id = item.Id.ToString(),
                Source = SourceName(item.Source),
                Title = item.Title,
                Detail = item.Excerpt,
                Parameter = WhyItMattersCatalog.ParameterOf(item),
                SignalId = catalog.SignalIdFor(item),
                ExternalRef = item.ExternalRef,
            }).ToArray(),
        };
    }

    private static string SourceName(EvidenceSource source)
    {
        return source switch
        {
            EvidenceSource.Telemetry => "telemetry",
            EvidenceSource.Spc => "spc",
            EvidenceSource.Mes => "mes",
            EvidenceSource.Qms => "qms",
            EvidenceSource.Plm => "plm",
            EvidenceSource.Erp => "erp",
            EvidenceSource.Cmms => "cmms",
            EvidenceSource.Ticketing => "ticketing",
            EvidenceSource.ManualMeasurement => "manualMeasurement",
            _ => "document",
        };
    }

    private static string[] Ids(EvidencePack pack, IEnumerable<string> externalRefs)
    {
        var wanted = new HashSet<string>(externalRefs, StringComparer.OrdinalIgnoreCase);
        return pack.Items
            .Where(item => item.ExternalRef is not null && wanted.Contains(item.ExternalRef))
            .Select(item => item.Id.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsAlignment(WorkOrder order)
    {
        return order.Kind.Contains("align", StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeOrder(WorkOrder order)
    {
        return order.WorkOrderId + " " + order.Kind + ": " + order.Summary;
    }

    private static string ScopeName(ImpactScopeKind kind)
    {
        return kind switch
        {
            ImpactScopeKind.Chamber => "chamber",
            ImpactScopeKind.Recipe => "recipe",
            ImpactScopeKind.MaterialLot => "materialLot",
            _ => "tool",
        };
    }
}

/// <summary>
/// One evidence row the resolution prompt can cite. The model does not invent ids.
/// </summary>
public sealed class EvidenceNote
{
    public string Id { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>Full record text: values, dates and change details. The model reasons from this, not the title.</summary>
    public string? Detail { get; set; }

    public string? Parameter { get; set; }

    public string? SignalId { get; set; }

    public string? ExternalRef { get; set; }
}
