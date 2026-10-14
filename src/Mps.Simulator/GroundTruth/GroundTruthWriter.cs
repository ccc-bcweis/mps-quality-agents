// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

using Mps.Domain.Enterprise;
using Mps.Domain.Identity;
using Mps.Simulator.Faults;
using Mps.Simulator.Fleet;

namespace Mps.Simulator.GroundTruth;

/// <summary>
/// Copies a fault's evidence into an enterprise draft and returns the hidden answer. The draft never receives the category.
/// </summary>
public static class GroundTruthWriter
{
    public static GroundTruthDocument Apply(FaultDefinition fault, EnterpriseDraft draft)
    {
        ArgumentNullException.ThrowIfNull(fault);
        ArgumentNullException.ThrowIfNull(draft);
        var evidenceIds = new List<string>();
        var confounderIds = new List<string>();
        var missing = fault.Evidence.Missing.Select(record => record.RecordId).ToHashSet(StringComparer.Ordinal);
        var evidence = fault.Evidence;

        AddEco(draft, evidence.Eco, missing, evidenceIds);
        AddConfig(draft, evidence.Config, evidenceIds);
        AddCalibration(draft, evidence.Calibration, missing, evidenceIds);
        AddMaterial(draft, evidence.MaterialLot, evidence.MaterialToolIds, evidenceIds);
        AddNotice(draft, evidence.Notice, evidenceIds);
        AddWorkOrder(draft, evidence.WorkOrder, evidenceIds);
        AddRuns(draft, fault, evidence.Runs, evidenceIds);

        foreach (var confounder in evidence.Confounders)
        {
            switch (confounder)
            {
                case Confounder.PreventiveMaintenance pm:
                    draft.WorkOrders.Add(pm.Order);
                    confounderIds.Add(pm.Order.WorkOrderId);
                    break;
                case Confounder.SupplierLotChange lot:
                    draft.MaterialLots.Add(lot.Lot);
                    draft.Notices.Add(lot.Notice);
                    LinkMaterial(draft, lot.Lot, lot.ToolIds);
                    confounderIds.Add(lot.Lot.LotId);
                    confounderIds.Add(lot.Notice.NoticeId);
                    break;
                case Confounder.ChangeOrder change:
                    draft.Ecos.Add(change.Eco);
                    confounderIds.Add(change.Eco.EcoId);
                    break;
                default:
                    throw new InvalidOperationException("Unknown confounder.");
            }
        }

        foreach (var missingId in missing)
        {
            if (draft.RecordIds().Contains(missingId, StringComparer.Ordinal))
            {
                throw new FaultCatalogException([$"{fault.Id} wrote missing record {missingId}."]);
            }
        }

        if (evidenceIds.Intersect(confounderIds, StringComparer.Ordinal).Any())
        {
            throw new FaultCatalogException([$"{fault.Id} lists the same id as evidence and as a confounder."]);
        }

        return new GroundTruthDocument(
            fault.Id,
            fault.Scenario,
            fault.Category,
            fault.ExpectedBehavior,
            fault.AssertCategory,
            fault.Cause,
            fault.ToolIds,
            fault.Parameter,
            fault.ChamberId,
            evidenceIds,
            confounderIds,
            missing.Order(StringComparer.Ordinal).ToArray(),
            evidence.Contradiction?.Detail,
            fault.CounterEvidence,
            fault.ToSignals());
    }

    public static void WriteCatalog(string faultDirectory, FleetCatalog fleet, string outputDirectory)
    {
        var catalog = FaultCatalogLoader.Load(faultDirectory, fleet);
        Directory.CreateDirectory(outputDirectory);
        foreach (var fault in catalog.Faults)
        {
            var document = Apply(fault, new EnterpriseDraft());
            GroundTruthJson.Write(document, Path.Combine(outputDirectory, fault.Id + ".json"));
        }
    }

    private static void AddEco(EnterpriseDraft draft, EcoRecord? eco, HashSet<string> missing, List<string> evidenceIds)
    {
        if (eco is null || missing.Contains(eco.EcoId))
        {
            return;
        }

        draft.Ecos.Add(eco);
        evidenceIds.Add(eco.EcoId);
    }

    private static void AddConfig(EnterpriseDraft draft, ConfigRevision? config, List<string> evidenceIds)
    {
        if (config is null)
        {
            return;
        }

        draft.Configs.RemoveAll(existing => existing.ToolId.Value == config.ToolId.Value);
        draft.Configs.Add(config);
        evidenceIds.Add(config.ConfigId);
    }

    private static void AddCalibration(EnterpriseDraft draft, CalibrationRecord? calibration, HashSet<string> missing, List<string> evidenceIds)
    {
        if (calibration is null || missing.Contains(calibration.CalibrationId))
        {
            return;
        }

        draft.Calibrations.Add(calibration);
        evidenceIds.Add(calibration.CalibrationId);
    }

    private static void AddMaterial(EnterpriseDraft draft, MaterialLot? lot, IReadOnlyList<string> toolIds, List<string> evidenceIds)
    {
        if (lot is null)
        {
            return;
        }

        draft.MaterialLots.Add(lot);
        LinkMaterial(draft, lot, toolIds);
        evidenceIds.Add(lot.LotId);
    }

    private static void AddNotice(EnterpriseDraft draft, SupplierNotice? notice, List<string> evidenceIds)
    {
        if (notice is null)
        {
            return;
        }

        draft.Notices.Add(notice);
        evidenceIds.Add(notice.NoticeId);
    }

    private static void AddWorkOrder(EnterpriseDraft draft, WorkOrder? order, List<string> evidenceIds)
    {
        if (order is null)
        {
            return;
        }

        draft.WorkOrders.Add(order);
        evidenceIds.Add(order.WorkOrderId);
    }

    private static void AddRuns(EnterpriseDraft draft, FaultDefinition fault, FaultRuns? runs, List<string> evidenceIds)
    {
        if (runs is null)
        {
            return;
        }

        foreach (var toolId in fault.ToolIds)
        {
            var tool = new ToolId(toolId);
            if (!draft.Lots.Any(lot => lot.LotId == runs.LotId && lot.ToolId.Value == toolId))
            {
                draft.Lots.Add(new LotReference(runs.LotId, runs.MaterialCode, tool));
            }

            for (var index = 0; index < runs.Count; index++)
            {
                var runId = string.Create(CultureInfo.InvariantCulture, $"RUN-{fault.Id}-{toolId}-{index:D2}");
                draft.Runs.Add(new ProcessRun(runId, tool, runs.RecipeId, runs.RecipeRevision, runs.LotId, fault.Onset.AddHours(index * 2)));
                evidenceIds.Add(runId);
            }
        }
    }

    private static void LinkMaterial(EnterpriseDraft draft, MaterialLot lot, IReadOnlyList<string> toolIds)
    {
        foreach (var toolId in toolIds)
        {
            draft.MaterialUses.Add(new MaterialUse(lot.LotId, toolId));
            if (!draft.Lots.Any(existing => existing.LotId == lot.LotId && existing.ToolId.Value == toolId))
            {
                draft.Lots.Add(new LotReference(lot.LotId, lot.MaterialCode, new ToolId(toolId)));
            }
        }
    }
}
