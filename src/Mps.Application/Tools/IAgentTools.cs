// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Enterprise;
using Mps.Domain.Identity;
using Mps.Domain.Impact;
using Mps.Domain.Telemetry;

namespace Mps.Application.Tools;

/// <summary>
/// Read-only tools the investigation agent may call. Write operations are not in this namespace.
/// </summary>
public interface ITelemetryTool
{
    Task<TelemetryWindow> GetWindowAsync(
        ToolId toolId,
        string parameter,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken);
}

public interface ISpcTool
{
    Task<IReadOnlyList<SpcViolation>> GetViolationsAsync(
        ToolId toolId,
        string parameter,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken);
}

public interface IMesTool
{
    Task<IReadOnlyList<ProcessRun>> GetRunsAsync(
        ToolId toolId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LotReference>> GetLotsAsync(
        ToolId toolId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken);
}

public interface IQmsQueryTool
{
    Task<IReadOnlyList<NcrRecord>> SearchNcrsAsync(
        ToolId toolId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken);
}

public interface IPlmTool
{
    Task<IReadOnlyList<EcoRecord>> GetEcosAsync(
        ToolId toolId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken);

    Task<ConfigRevision> GetConfigAsync(ToolId toolId, CancellationToken cancellationToken);
}

public interface IErpTool
{
    Task<IReadOnlyList<MaterialLot>> GetMaterialLotsAsync(
        ToolId toolId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SupplierNotice>> GetSupplierNoticesAsync(
        string? materialCode,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken);
}

public interface ICmmsTool
{
    Task<IReadOnlyList<WorkOrder>> GetWorkOrdersAsync(
        ToolId toolId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CalibrationRecord>> GetCalibrationsAsync(ToolId toolId, CancellationToken cancellationToken);
}

public interface IDocumentSearchTool
{
    Task<IReadOnlyList<DocumentHit>> SearchAsync(
        string query,
        string? toolFamily,
        CancellationToken cancellationToken);
}

public interface IImpactScopeTool
{
    Task<ImpactScope> GetScopeAsync(
        ToolId toolId,
        string parameter,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken);
}
