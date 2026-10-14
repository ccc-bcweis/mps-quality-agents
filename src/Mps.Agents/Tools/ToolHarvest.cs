// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Enterprise;
using Mps.Domain.Impact;
using Mps.Domain.Telemetry;

namespace Mps.Agents.Tools;

/// <summary>
/// Typed rows collected from read-only tools. The evidence pack is built from these rows.
/// </summary>
public sealed class ToolHarvest
{
    internal List<SpcViolation> Violations { get; } = [];

    internal TelemetryWindow? Telemetry { get; set; }

    internal List<ProcessRun> Runs { get; } = [];

    internal List<LotReference> Lots { get; } = [];

    internal List<NcrRecord> Ncrs { get; } = [];

    internal List<EcoRecord> Ecos { get; } = [];

    internal ConfigRevision? Config { get; set; }

    internal List<MaterialLot> MaterialLots { get; } = [];

    internal List<SupplierNotice> Notices { get; } = [];

    internal List<WorkOrder> WorkOrders { get; } = [];

    internal List<CalibrationRecord> Calibrations { get; } = [];

    internal bool CalibrationLookupCompleted { get; set; }

    internal List<DocumentHit> Documents { get; } = [];

    internal ImpactScope? ReportedScope { get; set; }
}
