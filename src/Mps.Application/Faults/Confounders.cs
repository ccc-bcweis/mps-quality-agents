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
    /// <summary>Calibration records; only cause records use them (an overdue calibration on the affected instrument).</summary>
    public IReadOnlyList<CalibrationRecord> Calibrations { get; init; } = [];

    /// <summary>The record ids, marked as the wrong answer for scoring.</summary>
    public IReadOnlyList<string> RecordIds =>
        Ecos.Select(eco => eco.EcoId)
            .Concat(WorkOrders.Select(order => order.WorkOrderId))
            .Concat(MaterialLots.Select(lot => lot.LotId))
            .Concat(Notices.Select(notice => notice.NoticeId))
            .Concat(Calibrations.Select(calibration => calibration.CalibrationId))
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

/// <summary>
/// The real cause of a free-form injected fault, written as ordinary business records (SPEC 2.1): an ECO, an overdue
/// calibration, an out-of-spec lot with its supplier notice, or a clean record. They are consistent with the chosen
/// root-cause type, dated before onset, never touch sensor readings, and are the only place the answer lives.
/// </summary>
public static class GroundTruthFactory
{
    public static InjectedConfounders For(RootCauseCategory category, ToolId toolId, string? chamberId, string parameter, Guid faultId, DateTimeOffset now)
    {
        var tool = toolId.Value;
        var chamber = chamberId ?? tool;
        var number = (int)(BitConverter.ToUInt32(faultId.ToByteArray(), 4) % 9000) + 1000;
        var n = number.ToString(CultureInfo.InvariantCulture);
        var fault = faultId.ToString("N");
        var display = Mps.Domain.Telemetry.ParameterNames.Display(parameter);
        var lower = display.ToLowerInvariant();
        switch (category)
        {
            case RootCauseCategory.EngineeringChange:
                return new(fault, tool, [new EcoRecord("ECO-5" + n, "Control loop tuning change on " + chamber, tool, null, now.AddDays(-1),
                    "Changed the " + lower + " control loop gain on " + chamber + " to shorten settling time. Hardware and other setpoints are unchanged. "
                    + "The qualification run noted the loop can overshoot and swing at the new gain.")], [], [], []);
            case RootCauseCategory.EquipmentCalibrationOrAlignment:
                return new(fault, tool, [], [], [], [])
                {
                    Calibrations = [new CalibrationRecord("CAL-" + n, toolId, display + " sensor on " + chamber, now.AddDays(-12), null, true)],
                };
            case RootCauseCategory.SupplyChainMaterial:
                var lotId = "LOT-Q" + n;
                var noticeId = "SN-Q" + n;
                return new(fault, tool, [], [],
                    [new MaterialLot(lotId, "GAS-PROC-" + tool, "Corvale Specialty Gases", now.AddDays(-1), noticeId)],
                    [new SupplierNotice(noticeId, "Corvale Specialty Gases", "GAS-PROC-" + tool, lotId,
                        "Lot " + lotId + " shipped to " + tool + " is outside the purchase specification for purity; the supplier advises quarantine.", now.AddHours(-12))]);
            case RootCauseCategory.ProcessDrift:
                return new(fault, tool, [], [new WorkOrder("WO-C" + n, toolId, chamberId, "chamber clean", now.AddDays(-21),
                    "Last wet clean on " + chamber + ". Radio-frequency hours since this clean are now past the usual clean interval. No parts or settings changed since.")], [], []);
            default:
                return new(fault, tool, [], [], [], []);
        }
    }
}

/// <summary>Runtime confounder and cause records, served by the enterprise tools next to the seed. Demo reset clears them.</summary>
public sealed class ConfounderBoard
{
    private readonly ConcurrentDictionary<string, InjectedConfounders> _byFault = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, InjectedConfounders> _causes = new(StringComparer.Ordinal);

    /// <summary>Stores the ground-truth cause records of a free-form fault.</summary>
    public void AddCause(InjectedConfounders records)
    {
        ArgumentNullException.ThrowIfNull(records);
        _causes[records.FaultId] = records;
    }

    /// <summary>Ground truth for scoring: these ids are the cause of their fault.</summary>
    public IReadOnlyList<string> CauseIds(Guid faultId) =>
        _causes.TryGetValue(faultId.ToString("N"), out var records) ? records.RecordIds : [];

    public IEnumerable<CalibrationRecord> Calibrations(string toolId) => For(toolId).SelectMany(item => item.Calibrations);

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

    public IEnumerable<SupplierNotice> Notices => _byFault.Values.Concat(_causes.Values).SelectMany(item => item.Notices);

    public void Clear()
    {
        _byFault.Clear();
        _causes.Clear();
    }

    private IEnumerable<InjectedConfounders> For(string toolId) =>
        _byFault.Values.Concat(_causes.Values).Where(item => string.Equals(item.ToolId, toolId, StringComparison.Ordinal));
}
