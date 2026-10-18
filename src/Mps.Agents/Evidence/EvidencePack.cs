// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Explain;
using Mps.Agents.Impact;
using Mps.Domain.Enterprise;
using Mps.Domain.Evidence;
using Mps.Domain.Identity;

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
        IReadOnlyList<CitedEvidence>? explanations = null,
        IReadOnlyList<Candidate>? candidates = null,
        IReadOnlyList<RuledOutRecord>? ruledOut = null)
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
        Candidates = candidates?.ToArray() ?? [];
        RuledOut = ruledOut?.ToArray() ?? [];
        if (Candidates.Count > 0 && RuledOut.Count > 0)
        {
            var candidateIds = new HashSet<EvidenceId>(Candidates.Select(candidate => candidate.Id));
            foreach (var record in RuledOut)
            {
                if (candidateIds.Contains(record.Id))
                {
                    throw new ArgumentException(
                        "A record id is in at most one of candidates and ruled out.",
                        nameof(ruledOut));
                }
            }
        }
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

    public IReadOnlyList<Candidate> Candidates { get; }

    public IReadOnlyList<RuledOutRecord> RuledOut { get; }

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
        var (addedCandidates, addedRuledOut) = EvidenceLists.Partition([], [], [], [], [], extra);
        var candidates = Candidates.ToList();
        var ruledOut = RuledOut.ToList();
        foreach (var candidate in addedCandidates)
        {
            if (candidates.All(existing => existing.Id != candidate.Id) && ruledOut.All(existing => existing.Id != candidate.Id))
            {
                candidates.Add(candidate);
            }
        }

        foreach (var record in addedRuledOut)
        {
            if (candidates.All(existing => existing.Id != record.Id) && ruledOut.All(existing => existing.Id != record.Id))
            {
                ruledOut.Add(record);
            }
        }

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
            CitedEvidenceFactory.ForItems(items, catalog),
            candidates,
            ruledOut);
    }
}
