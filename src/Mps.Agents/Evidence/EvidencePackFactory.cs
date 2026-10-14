// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

using Mps.Agents.Impact;
using Mps.Agents.Tools;
using Mps.Domain.Enterprise;
using Mps.Domain.Evidence;
using Mps.Domain.Incidents;

namespace Mps.Agents.Evidence;

/// <summary>
/// Builds an evidence pack from tool rows. A MES recipe id is treated as a part name so a header revision can contradict PLM.
/// </summary>
public static class EvidencePackFactory
{
    public static GatheredFacts Build(Incident incident, ToolHarvest harvest)
    {
        ArgumentNullException.ThrowIfNull(incident);
        ArgumentNullException.ThrowIfNull(harvest);
        var items = new List<EvidenceItem>();
        var claims = new List<ConfigClaim>();
        var observations = new List<ImpactObservation>();
        var events = new List<AlignedEvent>();

        foreach (var violation in harvest.Violations)
        {
            var reference = string.Join(
                ':',
                "spc",
                violation.ToolId.Value,
                violation.Parameter,
                violation.At.ToString("O", CultureInfo.InvariantCulture),
                violation.Rule);
            items.Add(Item(EvidenceSource.Spc, violation.Rule + " " + violation.Parameter, reference, violation.At, violation.ToolId.Value));
            observations.Add(new ImpactObservation(violation.ToolId.Value, violation.ChamberId, null, null));
        }

        if (harvest.Telemetry is not null)
        {
            var window = harvest.Telemetry;
            var reference = string.Join(
                ':',
                "telemetry",
                window.ToolId.Value,
                window.Parameter,
                window.From.ToString("O", CultureInfo.InvariantCulture));
            items.Add(Item(EvidenceSource.Telemetry, window.Parameter + " window", reference, window.To, window.ToolId.Value));
        }

        foreach (var eco in harvest.Ecos)
        {
            items.Add(Item(EvidenceSource.Plm, eco.Title, eco.EcoId, eco.EffectiveAt, eco.ToolId));
            events.Add(new AlignedEvent("eco", eco.EcoId, eco.EffectiveAt));
        }

        if (harvest.Config is not null)
        {
            var config = harvest.Config;
            items.Add(Item(EvidenceSource.Plm, config.PartName + " rev " + config.Revision, config.ConfigId, incident.OpenedAt, config.ToolId.Value));
            claims.Add(new ConfigClaim(EvidenceSource.Plm, config.PartName, config.Revision, config.ConfigId));
        }

        foreach (var run in harvest.Runs)
        {
            items.Add(Item(EvidenceSource.Mes, run.RecipeId + " rev " + run.RecipeRevision, run.RunId, run.StartedAt, run.ToolId.Value));
            claims.Add(new ConfigClaim(EvidenceSource.Mes, run.RecipeId, run.RecipeRevision, run.RunId));
            observations.Add(new ImpactObservation(run.ToolId.Value, null, run.RecipeId, run.LotId));
        }

        foreach (var lot in harvest.Lots)
        {
            items.Add(Item(EvidenceSource.Mes, lot.MaterialCode, lot.LotId, incident.OpenedAt, lot.ToolId.Value));
            observations.Add(new ImpactObservation(lot.ToolId.Value, null, null, lot.LotId));
        }

        foreach (var ncr in harvest.Ncrs)
        {
            items.Add(Item(EvidenceSource.Qms, ncr.Title, ncr.NcrId, ncr.OpenedAt, ncr.ToolId));
        }

        foreach (var material in harvest.MaterialLots)
        {
            items.Add(Item(EvidenceSource.Erp, material.MaterialCode + " " + material.LotId, material.LotId, material.ReceivedAt, incident.ToolId.Value));
            events.Add(new AlignedEvent("lot", material.LotId, material.ReceivedAt));
            observations.Add(new ImpactObservation(incident.ToolId.Value, null, null, material.LotId));
        }

        foreach (var notice in harvest.Notices)
        {
            items.Add(Item(EvidenceSource.Erp, notice.Summary, notice.NoticeId, notice.IssuedAt, null));
            events.Add(new AlignedEvent("notice", notice.NoticeId, notice.IssuedAt));
        }

        foreach (var order in harvest.WorkOrders)
        {
            items.Add(Item(EvidenceSource.Cmms, order.Summary, order.WorkOrderId, order.PerformedAt, order.ToolId.Value));
            events.Add(new AlignedEvent(order.Kind, order.WorkOrderId, order.PerformedAt));
        }

        foreach (var calibration in harvest.Calibrations)
        {
            var at = calibration.CompletedAt ?? calibration.DueAt;
            items.Add(Item(EvidenceSource.Cmms, calibration.Instrument, calibration.CalibrationId, at, calibration.ToolId.Value));
            if (calibration.IsOverdue)
            {
                events.Add(new AlignedEvent("calibration", calibration.CalibrationId, calibration.DueAt));
            }
        }

        foreach (var document in harvest.Documents)
        {
            items.Add(Item(EvidenceSource.Document, document.Title, document.DocumentId, incident.OpenedAt, null));
        }

        if (observations.Count == 0)
        {
            observations.Add(new ImpactObservation(incident.ToolId.Value, incident.ChamberId, null, null));
        }

        var shape = harvest.Telemetry is null
            ? SignalShape.Unknown
            : SignalShapeClassifier.Classify(harvest.Telemetry.Points);
        var pack = new EvidencePack(
            items,
            claims,
            harvest.Ecos,
            harvest.WorkOrders,
            harvest.Calibrations,
            harvest.MaterialLots,
            harvest.Notices,
            harvest.CalibrationLookupCompleted,
            shape);
        return new GatheredFacts(pack, observations, events, shape);
    }

    private static EvidenceItem Item(EvidenceSource source, string title, string externalRef, DateTimeOffset observedAt, string? toolId)
    {
        return new EvidenceItem(EvidenceIds.Create(source, externalRef), source, title, title, observedAt, toolId, externalRef);
    }
}
