// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Agents.Tools;

/// <summary>
/// Read-only tool names the investigation agent may call. Anything else is disallowed and is not executed.
/// </summary>
public static class AgentToolNames
{
    public const string TelemetryGetWindow = "Telemetry.GetWindow";

    public const string SpcGetViolations = "Spc.GetViolations";

    public const string MesGetRuns = "Mes.GetRuns";

    public const string MesGetLots = "Mes.GetLots";

    public const string QmsSearchNcrs = "Qms.SearchNcrs";

    public const string PlmGetEcos = "Plm.GetEcos";

    public const string PlmGetConfig = "Plm.GetConfig";

    public const string ErpGetMaterialLots = "Erp.GetMaterialLots";

    public const string ErpGetSupplierNotices = "Erp.GetSupplierNotices";

    public const string CmmsGetWorkOrders = "Cmms.GetWorkOrders";

    public const string CmmsGetCalibrations = "Cmms.GetCalibrations";

    public const string DocsSearch = "Docs.Search";

    public const string ImpactScope = "Impact.Scope";

    public static readonly IReadOnlyList<string> DefaultPlan =
    [
        SpcGetViolations,
        TelemetryGetWindow,
        ImpactScope,
        PlmGetEcos,
        PlmGetConfig,
        MesGetRuns,
        CmmsGetCalibrations,
        CmmsGetWorkOrders,
        ErpGetMaterialLots,
        ErpGetSupplierNotices,
    ];

    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        TelemetryGetWindow,
        SpcGetViolations,
        MesGetRuns,
        MesGetLots,
        QmsSearchNcrs,
        PlmGetEcos,
        PlmGetConfig,
        ErpGetMaterialLots,
        ErpGetSupplierNotices,
        CmmsGetWorkOrders,
        CmmsGetCalibrations,
        DocsSearch,
        ImpactScope,
    };

    public static bool IsAllowed(string? name)
    {
        return name is not null && Allowed.Contains(name);
    }
}
