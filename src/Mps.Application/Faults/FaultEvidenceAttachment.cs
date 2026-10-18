// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Enterprise;

namespace Mps.Application.Faults;

/// <summary>One lot tied to one tool, so a material record is visible only from the tools that used it.</summary>
public readonly record struct MaterialUseLink(string LotId, string ToolId);

/// <summary>
/// Enterprise records registered under one fault correlation. Dates stay spaced from <see cref="AnchorAt"/>
/// and are shifted to the moment the fault was registered.
/// </summary>
public sealed class FaultEvidenceAttachment
{
    public FaultEvidenceAttachment(
        DateTimeOffset? anchorAt = null,
        IReadOnlyList<EcoRecord>? ecos = null,
        IReadOnlyList<NcrRecord>? ncrs = null,
        IReadOnlyList<WorkOrder>? workOrders = null,
        IReadOnlyList<CalibrationRecord>? calibrations = null,
        IReadOnlyList<MaterialLot>? materialLots = null,
        IReadOnlyList<SupplierNotice>? notices = null,
        IReadOnlyList<MaterialUseLink>? materialUses = null)
    {
        AnchorAt = anchorAt;
        Ecos = Copy(ecos);
        Ncrs = Copy(ncrs);
        WorkOrders = Copy(workOrders);
        Calibrations = Copy(calibrations);
        MaterialLots = Copy(materialLots);
        Notices = Copy(notices);
        MaterialUses = Copy(materialUses);
    }

    public DateTimeOffset? AnchorAt { get; }

    public IReadOnlyList<EcoRecord> Ecos { get; }

    public IReadOnlyList<NcrRecord> Ncrs { get; }

    public IReadOnlyList<WorkOrder> WorkOrders { get; }

    public IReadOnlyList<CalibrationRecord> Calibrations { get; }

    public IReadOnlyList<MaterialLot> MaterialLots { get; }

    public IReadOnlyList<SupplierNotice> Notices { get; }

    public IReadOnlyList<MaterialUseLink> MaterialUses { get; }

    public bool IsEmpty =>
        Ecos.Count == 0
        && Ncrs.Count == 0
        && WorkOrders.Count == 0
        && Calibrations.Count == 0
        && MaterialLots.Count == 0
        && Notices.Count == 0
        && MaterialUses.Count == 0;

    /// <summary>Catalog records first, then records sent with the fault. The request's anchor wins when it has one.</summary>
    public static FaultEvidenceAttachment Merge(FaultEvidenceAttachment catalog, FaultEvidenceAttachment request)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);
        return new FaultEvidenceAttachment(
            request.AnchorAt ?? catalog.AnchorAt,
            catalog.Ecos.Concat(request.Ecos).ToArray(),
            catalog.Ncrs.Concat(request.Ncrs).ToArray(),
            catalog.WorkOrders.Concat(request.WorkOrders).ToArray(),
            catalog.Calibrations.Concat(request.Calibrations).ToArray(),
            catalog.MaterialLots.Concat(request.MaterialLots).ToArray(),
            catalog.Notices.Concat(request.Notices).ToArray(),
            catalog.MaterialUses.Concat(request.MaterialUses).ToArray());
    }

    private static T[] Copy<T>(IReadOnlyList<T>? rows) => rows is null || rows.Count == 0 ? [] : rows.ToArray();
}

/// <summary>Looks up the enterprise records authored for one fault-catalog id.</summary>
public interface IFaultEvidenceCatalog
{
    bool TryGet(string faultId, out FaultEvidenceAttachment records);
}

/// <summary>
/// Evidence served to enterprise tools, keyed by the fault correlation the cascade stamps on the fault.
/// </summary>
public interface IFaultEvidenceBoard
{
    void RegisterScenario(string correlationId, string scenarioId, DateTimeOffset? activatedAt);

    void RegisterRecords(string correlationId, FaultEvidenceAttachment records, DateTimeOffset? activatedAt);

    void Remove(string correlationId);

    void RemoveScenario(string scenarioId);

    void Clear();
}
