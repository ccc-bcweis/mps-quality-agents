// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;
using System.Text.Json.Serialization;

using Mps.Api.Demo;
using Mps.Application.Faults;
using Mps.Application.Tools;
using Mps.Domain.Enterprise;
using Mps.Domain.Identity;

namespace Mps.Api.Live;

/// <summary>
/// Read-only MES, QMS, PLM, ERP and CMMS tools served in process from the synthetic enterprise seed.
/// The hosted demo runs one container, so the agent reads the same seed the mock enterprise API serves locally.
/// </summary>
public sealed class SeedEnterpriseTools : IMesTool, IQmsQueryTool, IPlmTool, IErpTool, ICmmsTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new ToolIdConverter() },
    };

    /// <summary>
    /// Changes (ECOs, work orders, NCRs) are read further back than the signal window, because a change made
    /// weeks before onset is still a candidate cause. The seed is anchored to mid-October 2026.
    /// </summary>
    public static readonly TimeSpan ChangeLookback = TimeSpan.FromDays(30);

    private readonly Seed _seed;
    private readonly Dictionary<string, HashSet<string>> _materialTools = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Seed> _overlays = new(StringComparer.OrdinalIgnoreCase);
    private readonly ScenarioEvidenceBoard? _board;
    private Mps.Application.Faults.ConfounderBoard? _confounders;

    /// <summary>Serves misleading records written by the fault injector next to the seed.</summary>
    public SeedEnterpriseTools WithConfounders(Mps.Application.Faults.ConfounderBoard? confounders)
    {
        _confounders = confounders;
        return this;
    }

    public SeedEnterpriseTools(string seedPath, string? scenarioEvidencePath = null, ScenarioEvidenceBoard? board = null)
        : this(seedPath)
    {
        _board = board;
        if (!string.IsNullOrWhiteSpace(scenarioEvidencePath) && File.Exists(scenarioEvidencePath))
        {
            var overlays = JsonSerializer.Deserialize<Dictionary<string, Seed>>(Mps.Simulator.Telemetry.RecordDates.Rebase(File.ReadAllText(scenarioEvidencePath)), JsonOptions) ?? [];
            foreach (var pair in overlays)
            {
                _overlays[pair.Key] = pair.Value;
            }
        }
    }

    public SeedEnterpriseTools(string seedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seedPath);
        _seed = JsonSerializer.Deserialize<Seed>(Mps.Simulator.Telemetry.RecordDates.Rebase(File.ReadAllText(seedPath)), JsonOptions)
            ?? throw new InvalidDataException("Empty enterprise seed " + seedPath + ".");
        foreach (var use in _seed.MaterialUses ?? [])
        {
            if (string.IsNullOrWhiteSpace(use.LotId) || string.IsNullOrWhiteSpace(use.ToolId))
            {
                continue;
            }

            if (!_materialTools.TryGetValue(use.LotId, out var tools))
            {
                tools = new HashSet<string>(StringComparer.Ordinal);
                _materialTools.Add(use.LotId, tools);
            }

            tools.Add(use.ToolId);
        }
    }

    public Task<IReadOnlyList<ProcessRun>> GetRunsAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken) =>
        Done<ProcessRun>((_seed.Runs ?? []).Where(run => run.ToolId == toolId && In(run.StartedAt, from, until)));

    public Task<IReadOnlyList<LotReference>> GetLotsAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken) =>
        Done<LotReference>((_seed.Lots ?? []).Where(lot => lot.ToolId == toolId));

    public Task<IReadOnlyList<NcrRecord>> SearchNcrsAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken) =>
        Done(Merge(
            (_seed.Ncrs ?? []).Where(ncr => ncr.ToolId == toolId.Value && In(ncr.OpenedAt, from.Add(-ChangeLookback), until)),
            (Overlay?.Ncrs ?? []).Where(ncr => ncr.ToolId == toolId.Value),
            ncr => ncr.NcrId));

    public Task<IReadOnlyList<EcoRecord>> GetEcosAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken) =>
        Done(Merge(
            (_seed.Ecos ?? []).Where(eco => eco.ToolId == toolId.Value && In(eco.EffectiveAt, from.Add(-ChangeLookback), until)),
            (Overlay?.Ecos ?? []).Where(eco => eco.ToolId == toolId.Value).Concat(_confounders?.Ecos(toolId.Value) ?? []),
            eco => eco.EcoId));

    public Task<ConfigRevision> GetConfigAsync(ToolId toolId, CancellationToken cancellationToken)
    {
        // Tools added with the second and third plants have no seeded PLM row. Report a baseline revision
        // so the investigation keeps going instead of failing on a missing record.
        var config = (_seed.Configs ?? []).FirstOrDefault(item => item.ToolId == toolId)
            ?? new ConfigRevision("CFG-" + toolId.Value, toolId, "A", "Baseline chamber kit, no change on record");
        return Task.FromResult(config);
    }

    public Task<IReadOnlyList<MaterialLot>> GetMaterialLotsAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken) =>
        Done(Merge(
            (_seed.MaterialLots ?? []).Where(lot =>
                _materialTools.TryGetValue(lot.LotId, out var tools) && tools.Contains(toolId.Value) && In(lot.ReceivedAt, from.AddDays(-60), until)),
            (Overlay?.MaterialLots ?? []).Where(lot => (Overlay?.MaterialUses ?? []).Any(use => use.LotId == lot.LotId && use.ToolId == toolId.Value))
                .Concat(_confounders?.MaterialLots(toolId.Value) ?? []),
            lot => lot.LotId));

    public Task<IReadOnlyList<SupplierNotice>> GetSupplierNoticesAsync(string? materialCode, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken) =>
        Done(Merge(
            (_seed.Notices ?? []).Where(notice =>
                (string.IsNullOrWhiteSpace(materialCode) || notice.MaterialCode == materialCode) && In(notice.IssuedAt, from.AddDays(-60), until)),
            (Overlay?.Notices ?? []).Concat(_confounders?.Notices ?? []).Where(notice => string.IsNullOrWhiteSpace(materialCode) || notice.MaterialCode == materialCode),
            notice => notice.NoticeId));

    public Task<IReadOnlyList<WorkOrder>> GetWorkOrdersAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken) =>
        Done(Merge(
            (_seed.WorkOrders ?? []).Where(order => order.ToolId == toolId && In(order.PerformedAt, from.Add(-ChangeLookback), until)),
            (Overlay?.WorkOrders ?? []).Where(order => order.ToolId == toolId).Concat(_confounders?.WorkOrders(toolId.Value) ?? []),
            order => order.WorkOrderId));

    public Task<IReadOnlyList<CalibrationRecord>> GetCalibrationsAsync(ToolId toolId, CancellationToken cancellationToken) =>
        Done(Merge(
            (_seed.Calibrations ?? []).Where(record => record.ToolId == toolId),
            (Overlay?.Calibrations ?? []).Where(record => record.ToolId == toolId).Concat(_confounders?.Calibrations(toolId.Value) ?? []),
            record => record.CalibrationId));

    /// <summary>
    /// Records for the correlation bound on this async flow. Nothing when the caller did not bind one,
    /// so one fault never sees another's records. Dates keep their spacing from the anchor, shifted to registration.
    /// </summary>
    private Seed? Overlay
    {
        get
        {
            var board = _board;
            if (board?.CurrentCorrelation is not { } correlation)
            {
                return null;
            }

            var registration = board.Find(correlation);
            if (registration is null)
            {
                return null;
            }

            Seed? scenario = null;
            if (registration.ScenarioId is { } id && _overlays.TryGetValue(id, out var authored))
            {
                scenario = ShiftedTo(authored, registration.ActivatedAt);
            }

            var records = registration.Records is { IsEmpty: false } attachment
                ? ShiftedTo(FromRecords(attachment), registration.ActivatedAt)
                : null;
            return Combine(scenario, records);
        }
    }

    private static Seed ShiftedTo(Seed overlay, DateTimeOffset? activatedAt) =>
        overlay.AnchorAt is { } anchor && activatedAt is { } staged ? overlay.Shifted(staged - anchor) : overlay;

    private static Seed FromRecords(FaultEvidenceAttachment records) => new()
    {
        AnchorAt = records.AnchorAt,
        Ncrs = OrNull(records.Ncrs),
        Ecos = OrNull(records.Ecos),
        MaterialLots = OrNull(records.MaterialLots),
        Notices = OrNull(records.Notices),
        WorkOrders = OrNull(records.WorkOrders),
        Calibrations = OrNull(records.Calibrations),
        MaterialUses = records.MaterialUses.Count == 0
            ? null
            : records.MaterialUses.Select(use => new MaterialUse { LotId = use.LotId, ToolId = use.ToolId }).ToArray(),
    };

    private static T[]? OrNull<T>(IReadOnlyList<T> rows) => rows.Count == 0 ? null : rows.ToArray();

    private static Seed? Combine(Seed? scenario, Seed? records)
    {
        if (scenario is null)
        {
            return records;
        }

        if (records is null)
        {
            return scenario;
        }

        return new Seed
        {
            Ncrs = Join(scenario.Ncrs, records.Ncrs),
            Ecos = Join(scenario.Ecos, records.Ecos),
            MaterialLots = Join(scenario.MaterialLots, records.MaterialLots),
            MaterialUses = Join(scenario.MaterialUses, records.MaterialUses),
            Notices = Join(scenario.Notices, records.Notices),
            WorkOrders = Join(scenario.WorkOrders, records.WorkOrders),
            Calibrations = Join(scenario.Calibrations, records.Calibrations),
        };
    }

    private static T[]? Join<T>(T[]? left, T[]? right)
    {
        if (left is null || left.Length == 0)
        {
            return right is null || right.Length == 0 ? null : right;
        }

        if (right is null || right.Length == 0)
        {
            return left;
        }

        return [.. left, .. right];
    }

    /// <summary>Scenario rows win over seed rows with the same id, and are not date-filtered: the scenario scopes them.</summary>
    private static IEnumerable<T> Merge<T>(IEnumerable<T> seed, IEnumerable<T> overlay, Func<T, string> key)
    {
        var extra = overlay.ToArray();
        var ids = new HashSet<string>(extra.Select(key), StringComparer.Ordinal);
        return seed.Where(row => !ids.Contains(key(row))).Concat(extra);
    }

    private static bool In(DateTimeOffset at, DateTimeOffset from, DateTimeOffset until) => at >= from && at <= until;

    private static Task<IReadOnlyList<T>> Done<T>(IEnumerable<T> rows) => Task.FromResult<IReadOnlyList<T>>(rows.ToArray());

    private sealed class Seed
    {
        /// <summary>Scenario onset the record dates were written against. Present only on scenario overlays.</summary>
        public DateTimeOffset? AnchorAt { get; set; }

        /// <summary>The same records moved by one offset, so a scenario keeps its timeline relative to when it was staged.</summary>
        public Seed Shifted(TimeSpan by) => new()
        {
            Runs = Runs,
            Lots = Lots,
            Configs = Configs,
            MaterialUses = MaterialUses,
            Ncrs = Ncrs?.Select(row => new NcrRecord(row.NcrId, row.ToolId, row.Title, row.OpenedAt + by, row.LotId)).ToArray(),
            Ecos = Ecos?.Select(row => new EcoRecord(row.EcoId, row.Title, row.ToolId, row.RecipeId, row.EffectiveAt + by, row.ChangeSummary)).ToArray(),
            MaterialLots = MaterialLots?.Select(row => new MaterialLot(row.LotId, row.MaterialCode, row.SupplierName, row.ReceivedAt + by, row.NoticeId)).ToArray(),
            Notices = Notices?.Select(row => new SupplierNotice(row.NoticeId, row.SupplierName, row.MaterialCode, row.LotId, row.Summary, row.IssuedAt + by)).ToArray(),
            WorkOrders = WorkOrders?.Select(row => new WorkOrder(row.WorkOrderId, row.ToolId, row.ChamberId, row.Kind, row.PerformedAt + by, row.Summary)).ToArray(),
            Calibrations = Calibrations?.Select(row => new CalibrationRecord(row.CalibrationId, row.ToolId, row.Instrument, row.DueAt + by, row.CompletedAt + by, row.IsOverdue)).ToArray(),
        };

        public ProcessRun[]? Runs { get; set; }

        public LotReference[]? Lots { get; set; }

        public NcrRecord[]? Ncrs { get; set; }

        public EcoRecord[]? Ecos { get; set; }

        public ConfigRevision[]? Configs { get; set; }

        public MaterialLot[]? MaterialLots { get; set; }

        public MaterialUse[]? MaterialUses { get; set; }

        public SupplierNotice[]? Notices { get; set; }

        public WorkOrder[]? WorkOrders { get; set; }

        public CalibrationRecord[]? Calibrations { get; set; }
    }

    private sealed class MaterialUse
    {
        public string? LotId { get; set; }

        public string? ToolId { get; set; }
    }

    private sealed class ToolIdConverter : JsonConverter<ToolId>
    {
        public override ToolId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(reader.GetString() ?? throw new JsonException("Tool id is required."));

        public override void Write(Utf8JsonWriter writer, ToolId value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
    }
}
