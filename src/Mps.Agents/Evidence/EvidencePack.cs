// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Explain;
using Mps.Agents.Impact;
using Mps.Domain.Enterprise;
using Mps.Domain.Evidence;

namespace Mps.Agents.Evidence;

/// <summary>
/// Typed evidence the gate and the resolution agent share. Prose from the model is not a source.
/// </summary>
public sealed class EvidencePack
{
    public EvidencePack(
        IReadOnlyList<EvidenceItem> items,
        IReadOnlyList<ConfigClaim> configClaims,
        IReadOnlyList<EcoRecord> ecos,
        IReadOnlyList<WorkOrder> workOrders,
        IReadOnlyList<CalibrationRecord> calibrations,
        IReadOnlyList<MaterialLot> materialLots,
        IReadOnlyList<SupplierNotice> supplierNotices,
        bool calibrationLookupCompleted,
        SignalShape signalShape,
        IReadOnlyList<CitedEvidence>? explanations = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(configClaims);
        ArgumentNullException.ThrowIfNull(ecos);
        ArgumentNullException.ThrowIfNull(workOrders);
        ArgumentNullException.ThrowIfNull(calibrations);
        ArgumentNullException.ThrowIfNull(materialLots);
        ArgumentNullException.ThrowIfNull(supplierNotices);

        Items = items.ToArray();
        ConfigClaims = configClaims.ToArray();
        Ecos = ecos.ToArray();
        WorkOrders = workOrders.ToArray();
        Calibrations = calibrations.ToArray();
        MaterialLots = materialLots.ToArray();
        SupplierNotices = supplierNotices.ToArray();
        CalibrationLookupCompleted = calibrationLookupCompleted;
        SignalShape = signalShape;
        Explanations = explanations?.ToArray() ?? [];
    }

    public IReadOnlyList<EvidenceItem> Items { get; }

    public IReadOnlyList<ConfigClaim> ConfigClaims { get; }

    public IReadOnlyList<EcoRecord> Ecos { get; }

    public IReadOnlyList<WorkOrder> WorkOrders { get; }

    public IReadOnlyList<CalibrationRecord> Calibrations { get; }

    public IReadOnlyList<MaterialLot> MaterialLots { get; }

    public IReadOnlyList<SupplierNotice> SupplierNotices { get; }

    public bool CalibrationLookupCompleted { get; }

    public SignalShape SignalShape { get; }

    /// <summary>
    /// whyItMatters for every item in the pack. Sensor items also carry signalId.
    /// </summary>
    public IReadOnlyList<CitedEvidence> Explanations { get; }

    /// <summary>Appends bench readings and rebuilds whyItMatters for the whole pack.</summary>
    public EvidencePack Including(IReadOnlyList<EvidenceItem> extra, WhyItMattersCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(extra);
        ArgumentNullException.ThrowIfNull(catalog);
        if (extra.Count == 0)
        {
            return this;
        }

        var items = Items.Concat(extra).ToArray();
        return new EvidencePack(
            items,
            ConfigClaims,
            Ecos,
            WorkOrders,
            Calibrations,
            MaterialLots,
            SupplierNotices,
            CalibrationLookupCompleted,
            SignalShape,
            CitedEvidenceFactory.ForItems(items, catalog));
    }
}
