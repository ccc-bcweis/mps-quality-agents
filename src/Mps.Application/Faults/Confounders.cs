// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Collections.Concurrent;
using System.Globalization;

using Mps.Domain.Enterprise;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Application.Faults;

/// <summary>
/// Misleading business records written with an injected fault when "Add misleading records (to rule out)" is on
/// (SPEC 2.1, 2.2, 2.4). They follow the "Typical confounder" column for the chosen root-cause type, are dated the
/// same week, say in their own words why they cannot be the cause, and never touch sensor readings.
/// </summary>
public sealed record InjectedConfounders(
    string FaultId,
    string ToolId,
    IReadOnlyList<EcoRecord> Ecos,
    IReadOnlyList<WorkOrder> WorkOrders,
    IReadOnlyList<MaterialLot> MaterialLots,
    IReadOnlyList<SupplierNotice> Notices)
{
    /// <summary>The record ids, marked as the wrong answer for scoring.</summary>
    public IReadOnlyList<string> RecordIds =>
        Ecos.Select(eco => eco.EcoId)
            .Concat(WorkOrders.Select(order => order.WorkOrderId))
            .Concat(MaterialLots.Select(lot => lot.LotId))
            .Concat(Notices.Select(notice => notice.NoticeId))
            .ToArray();
}

public static class ConfounderFactory
{
    /// <summary>1-2 records that fit the §2.4 "Typical confounder" for <paramref name="category"/>.</summary>
    public static InjectedConfounders For(RootCauseCategory category, ToolId toolId, string? chamberId, Guid faultId, DateTimeOffset now)
    {
        var tool = toolId.Value;
        var number = (int)(BitConverter.ToUInt32(faultId.ToByteArray(), 0) % 9000) + 1000;
        var n = number.ToString(CultureInfo.InvariantCulture);
        var fault = faultId.ToString("N");
        var weekAgo = now.AddDays(-2);
        switch (category)
        {
            case RootCauseCategory.EngineeringChange:
                // Typical confounder: a PM done the same week.
                return new(fault, tool, [], [new WorkOrder("WO-7" + n, toolId, chamberId, "preventive maintenance", weekAgo,
                    "Routine preventive maintenance the same week. Inspection only: no parts changed and no settings changed. No findings.")], [], []);
            case RootCauseCategory.EquipmentCalibrationOrAlignment:
                // Typical confounder: a supplier lot change that week.
                var lotId = "LOT-AR-" + n;
                var noticeId = "SN-AR-" + n;
                return new(fault, tool, [], [],
                    [new MaterialLot(lotId, "GAS-AR", "Halvern Industrial Gases", weekAgo, noticeId)],
                    [new SupplierNotice(noticeId, "Halvern Industrial Gases", "GAS-AR", lotId,
                        "Routine lot change notice for argon lot " + lotId + " on " + tool + ". The certificate of analysis matches the purchase specification.", weekAgo)]);
            case RootCauseCategory.SupplyChainMaterial:
                // Typical confounder: an ECO on one of those tools.
                return new(fault, tool, [new EcoRecord("ECO-6" + n, "Operating note update on " + tool, tool, null, now.AddDays(-3),
                    "Document-only update to the " + tool + " operating note. Setpoints were not changed. Other tools are outside this note.")], [], [], []);
            case RootCauseCategory.ProcessDrift:
                // Typical confounder: normal noise for the time of year.
                return new(fault, tool, [], [new WorkOrder("WO-8" + n, toolId, chamberId, "facilities check", weekAgo,
                    "Quarterly facilities check the same week. Room temperature and humidity were in the normal range for the time of year. Inspection only: no parts changed. No findings.")], [], []);
            default:
                return new(fault, tool, [], [], [], []);
        }
    }
}

/// <summary>Runtime confounder records, served by the enterprise tools next to the seed. Demo reset clears them.</summary>
public sealed class ConfounderBoard
{
    private readonly ConcurrentDictionary<string, InjectedConfounders> _byFault = new(StringComparer.Ordinal);

    public void Add(InjectedConfounders records)
    {
        ArgumentNullException.ThrowIfNull(records);
        _byFault[records.FaultId] = records;
    }

    public IReadOnlyList<InjectedConfounders> All => _byFault.Values.ToArray();

    /// <summary>Ground truth for scoring: these ids are never the cause of their fault.</summary>
    public IReadOnlyList<string> ConfounderIds(Guid faultId) =>
        _byFault.TryGetValue(faultId.ToString("N"), out var records) ? records.RecordIds : [];

    public IEnumerable<EcoRecord> Ecos(string toolId) => For(toolId).SelectMany(item => item.Ecos);

    public IEnumerable<WorkOrder> WorkOrders(string toolId) => For(toolId).SelectMany(item => item.WorkOrders);

    public IEnumerable<MaterialLot> MaterialLots(string toolId) => For(toolId).SelectMany(item => item.MaterialLots);

    public IEnumerable<SupplierNotice> Notices => _byFault.Values.SelectMany(item => item.Notices);

    public void Clear() => _byFault.Clear();

    private IEnumerable<InjectedConfounders> For(string toolId) =>
        _byFault.Values.Where(item => string.Equals(item.ToolId, toolId, StringComparison.Ordinal));
}
