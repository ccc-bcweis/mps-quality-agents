// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;
using System.Text.Json.Serialization;

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

    private readonly Seed _seed;
    private readonly Dictionary<string, HashSet<string>> _materialTools = new(StringComparer.Ordinal);

    public SeedEnterpriseTools(string seedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seedPath);
        _seed = JsonSerializer.Deserialize<Seed>(File.ReadAllText(seedPath), JsonOptions)
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
        Done<NcrRecord>((_seed.Ncrs ?? []).Where(ncr => ncr.ToolId == toolId.Value && In(ncr.OpenedAt, from, until)));

    public Task<IReadOnlyList<EcoRecord>> GetEcosAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken) =>
        Done<EcoRecord>((_seed.Ecos ?? []).Where(eco => eco.ToolId == toolId.Value && In(eco.EffectiveAt, from, until)));

    public Task<ConfigRevision> GetConfigAsync(ToolId toolId, CancellationToken cancellationToken)
    {
        var config = (_seed.Configs ?? []).FirstOrDefault(item => item.ToolId == toolId)
            ?? throw new KeyNotFoundException("No configuration revision for " + toolId.Value + ".");
        return Task.FromResult(config);
    }

    public Task<IReadOnlyList<MaterialLot>> GetMaterialLotsAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken) =>
        Done<MaterialLot>((_seed.MaterialLots ?? []).Where(lot =>
            _materialTools.TryGetValue(lot.LotId, out var tools) && tools.Contains(toolId.Value) && In(lot.ReceivedAt, from.AddDays(-60), until)));

    public Task<IReadOnlyList<SupplierNotice>> GetSupplierNoticesAsync(string? materialCode, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken) =>
        Done<SupplierNotice>((_seed.Notices ?? []).Where(notice =>
            (string.IsNullOrWhiteSpace(materialCode) || notice.MaterialCode == materialCode) && In(notice.IssuedAt, from.AddDays(-60), until)));

    public Task<IReadOnlyList<WorkOrder>> GetWorkOrdersAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken) =>
        Done<WorkOrder>((_seed.WorkOrders ?? []).Where(order => order.ToolId == toolId && In(order.PerformedAt, from, until)));

    public Task<IReadOnlyList<CalibrationRecord>> GetCalibrationsAsync(ToolId toolId, CancellationToken cancellationToken) =>
        Done<CalibrationRecord>((_seed.Calibrations ?? []).Where(record => record.ToolId == toolId));

    private static bool In(DateTimeOffset at, DateTimeOffset from, DateTimeOffset until) => at >= from && at <= until;

    private static Task<IReadOnlyList<T>> Done<T>(IEnumerable<T> rows) => Task.FromResult<IReadOnlyList<T>>(rows.ToArray());

    private sealed class Seed
    {
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
