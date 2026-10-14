// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Enterprise;
using Mps.Simulator.GroundTruth;

namespace Mps.Simulator.Seed;

/// <summary>
/// Synthetic MES, QMS, PLM, ERP, and CMMS records for one seed. Material uses stay in this file so the mock can filter lots; they are not an API field.
/// </summary>
public sealed class EnterpriseSeed
{
    public EnterpriseSeed(
        int seed,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        ProcessRun[] runs,
        LotReference[] lots,
        NcrRecord[] ncrs,
        EcoRecord[] ecos,
        ConfigRevision[] configs,
        MaterialLot[] materialLots,
        MaterialUse[] materialUses,
        SupplierNotice[] notices,
        WorkOrder[] workOrders,
        CalibrationRecord[] calibrations)
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(lots);
        ArgumentNullException.ThrowIfNull(ncrs);
        ArgumentNullException.ThrowIfNull(ecos);
        ArgumentNullException.ThrowIfNull(configs);
        ArgumentNullException.ThrowIfNull(materialLots);
        ArgumentNullException.ThrowIfNull(materialUses);
        ArgumentNullException.ThrowIfNull(notices);
        ArgumentNullException.ThrowIfNull(workOrders);
        ArgumentNullException.ThrowIfNull(calibrations);
        Seed = seed;
        WindowStart = windowStart;
        WindowEnd = windowEnd;
        Runs = runs;
        Lots = lots;
        Ncrs = ncrs;
        Ecos = ecos;
        Configs = configs;
        MaterialLots = materialLots;
        MaterialUses = materialUses;
        Notices = notices;
        WorkOrders = workOrders;
        Calibrations = calibrations;
    }

    public int Seed { get; }

    public DateTimeOffset WindowStart { get; }

    public DateTimeOffset WindowEnd { get; }

    public ProcessRun[] Runs { get; }

    public LotReference[] Lots { get; }

    public NcrRecord[] Ncrs { get; }

    public EcoRecord[] Ecos { get; }

    public ConfigRevision[] Configs { get; }

    public MaterialLot[] MaterialLots { get; }

    public MaterialUse[] MaterialUses { get; }

    public SupplierNotice[] Notices { get; }

    public WorkOrder[] WorkOrders { get; }

    public CalibrationRecord[] Calibrations { get; }
}
