// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Enterprise;

namespace Mps.Simulator.GroundTruth;

/// <summary>
/// Enterprise records a fault writer can add. This draft has no root-cause field. The label lives only in ground truth.
/// </summary>
public sealed class EnterpriseDraft
{
    public List<ProcessRun> Runs { get; } = [];

    public List<LotReference> Lots { get; } = [];

    public List<NcrRecord> Ncrs { get; } = [];

    public List<EcoRecord> Ecos { get; } = [];

    public List<ConfigRevision> Configs { get; } = [];

    public List<MaterialLot> MaterialLots { get; } = [];

    public List<SupplierNotice> Notices { get; } = [];

    public List<WorkOrder> WorkOrders { get; } = [];

    public List<CalibrationRecord> Calibrations { get; } = [];

    /// <summary>Which tools consumed a material lot. The ERP payload itself has no tool id.</summary>
    public List<MaterialUse> MaterialUses { get; } = [];

    public IEnumerable<string> RecordIds()
    {
        foreach (var run in Runs)
        {
            yield return run.RunId;
        }

        foreach (var lot in Lots)
        {
            yield return lot.LotId;
        }

        foreach (var ncr in Ncrs)
        {
            yield return ncr.NcrId;
        }

        foreach (var eco in Ecos)
        {
            yield return eco.EcoId;
        }

        foreach (var config in Configs)
        {
            yield return config.ConfigId;
        }

        foreach (var lot in MaterialLots)
        {
            yield return lot.LotId;
        }

        foreach (var notice in Notices)
        {
            yield return notice.NoticeId;
        }

        foreach (var order in WorkOrders)
        {
            yield return order.WorkOrderId;
        }

        foreach (var calibration in Calibrations)
        {
            yield return calibration.CalibrationId;
        }
    }

    public IEnumerable<string> VisibleText()
    {
        foreach (var run in Runs)
        {
            yield return run.RunId;
            yield return run.RecipeId;
            yield return run.RecipeRevision;
            yield return run.LotId;
        }

        foreach (var eco in Ecos)
        {
            yield return eco.EcoId;
            yield return eco.Title;
            yield return eco.ChangeSummary;
        }

        foreach (var config in Configs)
        {
            yield return config.ConfigId;
            yield return config.Revision;
            yield return config.PartName;
        }

        foreach (var lot in MaterialLots)
        {
            yield return lot.LotId;
            yield return lot.MaterialCode;
            yield return lot.SupplierName;
        }

        foreach (var notice in Notices)
        {
            yield return notice.NoticeId;
            yield return notice.Summary;
        }

        foreach (var order in WorkOrders)
        {
            yield return order.WorkOrderId;
            yield return order.Kind;
            yield return order.Summary;
        }

        foreach (var calibration in Calibrations)
        {
            yield return calibration.CalibrationId;
            yield return calibration.Instrument;
        }
    }
}

public readonly record struct MaterialUse(string LotId, string ToolId, string? SiteId = null);
