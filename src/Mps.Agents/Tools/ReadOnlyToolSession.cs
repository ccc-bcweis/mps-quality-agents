// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Tools;
using Mps.Domain.Enterprise;
using Mps.Domain.Incidents;

namespace Mps.Agents.Tools;

/// <summary>
/// Binds allowlisted tool names to the read-only application ports.
/// </summary>
public sealed class ReadOnlyToolSession
{
    private readonly ITelemetryTool _telemetry;
    private readonly ISpcTool _spc;
    private readonly IMesTool _mes;
    private readonly IQmsQueryTool _qms;
    private readonly IPlmTool _plm;
    private readonly IErpTool _erp;
    private readonly ICmmsTool _cmms;
    private readonly IDocumentSearchTool _documents;
    private readonly IImpactScopeTool _impact;

    public ReadOnlyToolSession(
        ITelemetryTool telemetry,
        ISpcTool spc,
        IMesTool mes,
        IQmsQueryTool qms,
        IPlmTool plm,
        IErpTool erp,
        ICmmsTool cmms,
        IDocumentSearchTool documents,
        IImpactScopeTool impact)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        ArgumentNullException.ThrowIfNull(spc);
        ArgumentNullException.ThrowIfNull(mes);
        ArgumentNullException.ThrowIfNull(qms);
        ArgumentNullException.ThrowIfNull(plm);
        ArgumentNullException.ThrowIfNull(erp);
        ArgumentNullException.ThrowIfNull(cmms);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(impact);
        _telemetry = telemetry;
        _spc = spc;
        _mes = mes;
        _qms = qms;
        _plm = plm;
        _erp = erp;
        _cmms = cmms;
        _documents = documents;
        _impact = impact;
    }

    public async Task<string> ExecuteAsync(
        string name,
        Incident incident,
        DateTimeOffset from,
        DateTimeOffset until,
        ToolHarvest harvest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incident);
        ArgumentNullException.ThrowIfNull(harvest);
        if (!AgentToolNames.IsAllowed(name))
        {
            throw new InvalidOperationException("Disallowed tool calls are not executed.");
        }

        switch (name)
        {
            case AgentToolNames.SpcGetViolations:
                var violations = await _spc.GetViolationsAsync(incident.ToolId, incident.ChamberId, incident.Parameter, from, until, cancellationToken).ConfigureAwait(false);
                harvest.Violations.AddRange(violations);
                return "spc:" + violations.Count;
            case AgentToolNames.TelemetryGetWindow:
                var window = await _telemetry.GetWindowAsync(incident.ToolId, incident.ChamberId, incident.Parameter, from, until, cancellationToken).ConfigureAwait(false);
                harvest.Telemetry = window;
                return "telemetry:" + window.Points.Count;
            case AgentToolNames.ImpactScope:
                var scope = await _impact.GetScopeAsync(incident.ToolId, incident.Parameter, from, until, cancellationToken).ConfigureAwait(false);
                harvest.ReportedScope = scope;
                return "scope:" + scope.Kind + ":" + scope.Key;
            case AgentToolNames.PlmGetEcos:
                var ecos = await _plm.GetEcosAsync(incident.ToolId, from, until, cancellationToken).ConfigureAwait(false);
                harvest.Ecos.AddRange(ecos);
                return "ecos:" + ecos.Count;
            case AgentToolNames.PlmGetConfig:
                var config = await _plm.GetConfigAsync(incident.ToolId, cancellationToken).ConfigureAwait(false);
                harvest.Config = config;
                return "config:" + config.PartName + ":" + config.Revision;
            case AgentToolNames.MesGetRuns:
                var runs = await _mes.GetRunsAsync(incident.ToolId, from, until, cancellationToken).ConfigureAwait(false);
                harvest.Runs.AddRange(runs);
                return "runs:" + runs.Count;
            case AgentToolNames.MesGetLots:
                var lots = await _mes.GetLotsAsync(incident.ToolId, from, until, cancellationToken).ConfigureAwait(false);
                harvest.Lots.AddRange(lots);
                return "lots:" + lots.Count;
            case AgentToolNames.QmsSearchNcrs:
                var ncrs = await _qms.SearchNcrsAsync(incident.ToolId, from, until, cancellationToken).ConfigureAwait(false);
                harvest.Ncrs.AddRange(ncrs);
                return "ncrs:" + ncrs.Count;
            case AgentToolNames.CmmsGetCalibrations:
                var calibrations = await _cmms.GetCalibrationsAsync(incident.ToolId, cancellationToken).ConfigureAwait(false);
                harvest.Calibrations.AddRange(calibrations);
                harvest.CalibrationLookupCompleted = true;
                return "calibrations:" + calibrations.Count;
            case AgentToolNames.CmmsGetWorkOrders:
                var orders = await _cmms.GetWorkOrdersAsync(incident.ToolId, from, until, cancellationToken).ConfigureAwait(false);
                harvest.WorkOrders.AddRange(orders);
                return "workOrders:" + orders.Count;
            case AgentToolNames.ErpGetMaterialLots:
                var materials = await _erp.GetMaterialLotsAsync(incident.ToolId, from, until, cancellationToken).ConfigureAwait(false);
                AddLots(harvest, materials);
                return "materialLots:" + materials.Count;
            case AgentToolNames.ErpGetSupplierNotices:
                // Notices only for materials this tool used. A tool with no material lots gets none: an unfiltered
                // lookup once handed every notice in the plant to tools that never used that material (SC-8, Oct 18).
                if (harvest.MaterialLots.Count == 0)
                {
                    AddLots(harvest, await _erp.GetMaterialLotsAsync(incident.ToolId, from, until, cancellationToken).ConfigureAwait(false));
                }

                var noticeCount = 0;
                foreach (var code in harvest.MaterialLots.Select(lot => lot.MaterialCode).Where(code => !string.IsNullOrWhiteSpace(code)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var notices = await _erp.GetSupplierNoticesAsync(code, from, until, cancellationToken).ConfigureAwait(false);
                    var fresh = notices.Where(notice => harvest.Notices.All(known => known.NoticeId != notice.NoticeId)).ToArray();
                    harvest.Notices.AddRange(fresh);
                    noticeCount += fresh.Length;
                }

                return "notices:" + noticeCount;
            case AgentToolNames.DocsSearch:
                var hits = await _documents.SearchAsync(incident.Parameter, null, cancellationToken).ConfigureAwait(false);
                harvest.Documents.AddRange(hits);
                return "docs:" + hits.Count;
            default:
                throw new InvalidOperationException("Disallowed tool calls are not executed.");
        }
    }

    /// <summary>Lots once each, whether the agent asked for lots or the notice lookup needed them first.</summary>
    private static void AddLots(ToolHarvest harvest, IReadOnlyList<MaterialLot> lots) =>
        harvest.MaterialLots.AddRange(lots.Where(lot => harvest.MaterialLots.All(known => known.LotId != lot.LotId)));
}
