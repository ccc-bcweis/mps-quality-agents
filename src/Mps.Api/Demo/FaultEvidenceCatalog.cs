// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Faults;
using Mps.Domain.Enterprise;
using Mps.Domain.Identity;
using Mps.Simulator.Faults;
using Mps.Simulator.Fleet;

namespace Mps.Api.Demo;

/// <summary>The fault catalog's enterprise records, ready to register under an injected fault's correlation.</summary>
public sealed class FaultEvidenceCatalog : IFaultEvidenceCatalog
{
    private readonly Dictionary<string, FaultEvidenceAttachment> _byId;

    private FaultEvidenceCatalog(Dictionary<string, FaultEvidenceAttachment> byId)
    {
        _byId = byId;
    }

    public static FaultEvidenceCatalog Load(string directory, FleetCatalog fleet)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(fleet);
        var catalog = FaultCatalogLoader.Load(directory, fleet);
        var byId = new Dictionary<string, FaultEvidenceAttachment>(catalog.Faults.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var fault in catalog.Faults)
        {
            byId[fault.Id] = From(fault);
        }

        return new FaultEvidenceCatalog(byId);
    }

    public bool TryGet(string faultId, out FaultEvidenceAttachment records)
    {
        if (string.IsNullOrWhiteSpace(faultId))
        {
            records = null!;
            return false;
        }

        return _byId.TryGetValue(faultId.Trim(), out records!);
    }

    private static FaultEvidenceAttachment From(FaultDefinition fault)
    {
        var ecos = new List<EcoRecord>();
        var orders = new List<WorkOrder>();
        var lots = new List<MaterialLot>();
        var notices = new List<SupplierNotice>();
        var calibrations = new List<CalibrationRecord>();
        var uses = new List<MaterialUseLink>();
        var evidence = fault.Evidence;
        if (evidence.Eco is { } eco)
        {
            ecos.Add(eco);
        }

        if (evidence.WorkOrder is { } order)
        {
            orders.Add(order);
        }

        if (evidence.MaterialLot is { } lot)
        {
            lots.Add(lot);
            foreach (var toolId in evidence.MaterialToolIds)
            {
                uses.Add(new MaterialUseLink(lot.LotId, toolId));
            }
        }

        if (evidence.Notice is { } notice)
        {
            notices.Add(notice);
        }

        if (evidence.Calibration is { } calibration)
        {
            calibrations.Add(calibration);
        }

        foreach (var confounder in evidence.Confounders)
        {
            switch (confounder)
            {
                case Confounder.PreventiveMaintenance maintenance:
                    orders.Add(maintenance.Order);
                    break;
                case Confounder.ChangeOrder change:
                    ecos.Add(change.Eco);
                    break;
                case Confounder.SupplierLotChange supply:
                    lots.Add(supply.Lot);
                    notices.Add(supply.Notice);
                    foreach (var toolId in supply.ToolIds)
                    {
                        uses.Add(new MaterialUseLink(supply.Lot.LotId, toolId));
                    }

                    break;
            }
        }

        return new FaultEvidenceAttachment(fault.Onset, ecos, [], orders, calibrations, lots, notices, uses);
    }
}
