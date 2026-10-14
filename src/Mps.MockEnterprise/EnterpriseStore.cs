// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Mps.Domain.Enterprise;

namespace Mps.MockEnterprise;

public sealed class EnterpriseStore
{
    private readonly object _gate = new();
    private readonly HashSet<string> _capaIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _capaIncidents = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _ticketIncidents = new(StringComparer.OrdinalIgnoreCase);
    private int _ticketSequence = 1000;

    public EnterpriseStore(IOptions<MockOptions> options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        if (options.Value.TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Mock:TimeoutSeconds must be positive.");
        }

        var path = ResolveSeedPath(options.Value.SeedPath, environment.ContentRootPath);
        var document = JsonSerializer.Deserialize<SeedDocument>(File.ReadAllText(path), EnterpriseJson.Options)
            ?? throw new InvalidDataException($"Empty enterprise seed {path}.");
        Runs = document.Runs ?? [];
        Lots = document.Lots ?? [];
        Ncrs = document.Ncrs ?? [];
        Ecos = document.Ecos ?? [];
        Configs = document.Configs ?? [];
        MaterialLots = document.MaterialLots ?? [];
        Notices = document.Notices ?? [];
        WorkOrders = document.WorkOrders ?? [];
        Calibrations = document.Calibrations ?? [];
        var uses = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var use in document.MaterialUses ?? [])
        {
            if (string.IsNullOrWhiteSpace(use.LotId) || string.IsNullOrWhiteSpace(use.ToolId))
            {
                continue;
            }

            if (!uses.TryGetValue(use.LotId, out var tools))
            {
                tools = [];
                uses.Add(use.LotId, tools);
            }

            tools.Add(use.ToolId);
        }

        MaterialTools = uses;
    }

    public ProcessRun[] Runs { get; }

    public LotReference[] Lots { get; }

    public NcrRecord[] Ncrs { get; }

    public EcoRecord[] Ecos { get; }

    public ConfigRevision[] Configs { get; }

    public MaterialLot[] MaterialLots { get; }

    public SupplierNotice[] Notices { get; }

    public WorkOrder[] WorkOrders { get; }

    public CalibrationRecord[] Calibrations { get; }

    private Dictionary<string, List<string>> MaterialTools { get; }

    public IReadOnlyList<ProcessRun> RunsFor(string toolId, DateTimeOffset? from, DateTimeOffset? until)
    {
        return Runs.Where(run => run.ToolId.Value == toolId && InWindow(run.StartedAt, from, until)).ToArray();
    }

    public IReadOnlyList<LotReference> LotsFor(string toolId)
    {
        return Lots.Where(lot => lot.ToolId.Value == toolId).ToArray();
    }

    public IReadOnlyList<NcrRecord> NcrsFor(string toolId)
    {
        return Ncrs.Where(ncr => ncr.ToolId == toolId).ToArray();
    }

    public IReadOnlyList<EcoRecord> EcosFor(string toolId)
    {
        return Ecos.Where(eco => eco.ToolId == toolId).ToArray();
    }

    public ConfigRevision? ConfigFor(string toolId)
    {
        return Configs.FirstOrDefault(config => config.ToolId.Value == toolId);
    }

    public IReadOnlyList<MaterialLot> MaterialsFor(string toolId)
    {
        return MaterialLots.Where(lot => MaterialTools.TryGetValue(lot.LotId, out var tools) && tools.Contains(toolId)).ToArray();
    }

    public IReadOnlyList<SupplierNotice> NoticesFor(string? materialCode)
    {
        if (string.IsNullOrWhiteSpace(materialCode))
        {
            return Notices;
        }

        return Notices.Where(notice => notice.MaterialCode == materialCode).ToArray();
    }

    public IReadOnlyList<WorkOrder> WorkOrdersFor(string toolId)
    {
        return WorkOrders.Where(order => order.ToolId.Value == toolId).ToArray();
    }

    public IReadOnlyList<CalibrationRecord> CalibrationsFor(string toolId)
    {
        return Calibrations.Where(record => record.ToolId.Value == toolId).ToArray();
    }

    public WriteOutcome AddCapa(string capaId, string incidentId)
    {
        lock (_gate)
        {
            if (_capaIds.Contains(capaId) || _capaIncidents.Contains(incidentId))
            {
                return WriteOutcome.Conflict;
            }

            _capaIds.Add(capaId);
            _capaIncidents.Add(incidentId);
            return WriteOutcome.Created("QMS-" + capaId);
        }
    }

    public WriteOutcome AddTicket(string incidentId)
    {
        lock (_gate)
        {
            if (_ticketIncidents.Contains(incidentId))
            {
                return WriteOutcome.Conflict;
            }

            _ticketIncidents.Add(incidentId);
            _ticketSequence++;
            return WriteOutcome.Created(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"TKT-{_ticketSequence:D4}"));
        }
    }

    public static string ResolveSeedPath(string? configured, string contentRoot)
    {
        if (!string.IsNullOrWhiteSpace(configured) && Path.IsPathRooted(configured))
        {
            if (!File.Exists(configured))
            {
                throw new FileNotFoundException("Enterprise seed file was not found.", configured);
            }

            return configured;
        }

        var relative = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine("data", "seed", "enterprise.json")
            : configured;
        var dir = new DirectoryInfo(contentRoot);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Enterprise seed file was not found.", relative);
    }

    private static bool InWindow(DateTimeOffset stamp, DateTimeOffset? from, DateTimeOffset? until)
    {
        if (from is not null && stamp < from.Value)
        {
            return false;
        }

        return until is null || stamp <= until.Value;
    }

    private sealed class SeedDocument
    {
        public ProcessRun[]? Runs { get; set; }

        public LotReference[]? Lots { get; set; }

        public NcrRecord[]? Ncrs { get; set; }

        public EcoRecord[]? Ecos { get; set; }

        public ConfigRevision[]? Configs { get; set; }

        public MaterialLot[]? MaterialLots { get; set; }

        public MaterialLink[]? MaterialUses { get; set; }

        public SupplierNotice[]? Notices { get; set; }

        public WorkOrder[]? WorkOrders { get; set; }

        public CalibrationRecord[]? Calibrations { get; set; }
    }

    private sealed class MaterialLink
    {
        public string? LotId { get; set; }

        public string? ToolId { get; set; }
    }
}

public sealed class WriteOutcome
{
    private WriteOutcome(int statusCode, string? id)
    {
        StatusCode = statusCode;
        Id = id;
    }

    public int StatusCode { get; }

    public string? Id { get; }

    public static WriteOutcome Conflict { get; } = new(StatusCodes.Status409Conflict, null);

    public static WriteOutcome Created(string id)
    {
        return new WriteOutcome(StatusCodes.Status201Created, id);
    }
}
