// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Tools;
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
                var violations = await _spc.GetViolationsAsync(incident.ToolId, incident.Parameter, from, until, cancellationToken).ConfigureAwait(false);
                harvest.Violations.AddRange(violations);
                return "spc:" + violations.Count;
            case AgentToolNames.TelemetryGetWindow:
                var window = await _telemetry.GetWindowAsync(incident.ToolId, incident.Parameter, from, until, cancellationToken).ConfigureAwait(false);
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
                harvest.MaterialLots.AddRange(materials);
                return "materialLots:" + materials.Count;
            case AgentToolNames.ErpGetSupplierNotices:
                var materialCode = harvest.MaterialLots.FirstOrDefault()?.MaterialCode;
                var notices = await _erp.GetSupplierNoticesAsync(materialCode, from, until, cancellationToken).ConfigureAwait(false);
                harvest.Notices.AddRange(notices);
                return "notices:" + notices.Count;
            case AgentToolNames.DocsSearch:
                var hits = await _documents.SearchAsync(incident.Parameter, null, cancellationToken).ConfigureAwait(false);
                harvest.Documents.AddRange(hits);
                return "docs:" + hits.Count;
            default:
                throw new InvalidOperationException("Disallowed tool calls are not executed.");
        }
    }
}
