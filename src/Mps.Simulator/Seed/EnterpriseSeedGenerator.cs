// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Enterprise;
using Mps.Domain.Equipment;
using Mps.Domain.Identity;
using Mps.Simulator.Faults;
using Mps.Simulator.Fleet;
using Mps.Simulator.GroundTruth;

namespace Mps.Simulator.Seed;

/// <summary>
/// Builds the enterprise seed from the fleet and the fault catalog. Fault evidence is copied as written. Background history fills the 90-day window and does not carry a root-cause label.
/// </summary>
public static class EnterpriseSeedGenerator
{
    public const int DemoSeed = 20261014;

    public const int HistoryDays = 90;

    public const int Weeks = 12;

    public const int WafersPerWeek = 4;

    public static readonly DateTimeOffset WindowEnd = new(2026, 10, 14, 0, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset WindowStart = WindowEnd.AddDays(-HistoryDays);

    public static EnterpriseSeed Generate(FleetCatalog fleet, FaultCatalog catalog, int seed)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(catalog);
        if (fleet.Tools.Count != 8)
        {
            throw new InvalidOperationException("The enterprise seed expects the eight-tool fleet.");
        }

        var draft = new EnterpriseDraft();
        foreach (var fault in catalog.Faults)
        {
            _ = GroundTruthWriter.Apply(fault, draft);
        }

        var tools = fleet.Tools.OrderBy(tool => tool.Id, StringComparer.Ordinal).ToArray();
        AddBaseline(draft, tools);
        var faultRuns = draft.Runs.ToArray();
        AddHistory(draft, tools, faultRuns, seed);
        return new EnterpriseSeed(
            seed,
            WindowStart,
            WindowEnd,
            draft.Runs.OrderBy(run => run.StartedAt).ThenBy(run => run.RunId, StringComparer.Ordinal).ToArray(),
            DistinctLots(draft.Lots),
            draft.Ncrs.OrderBy(ncr => ncr.NcrId, StringComparer.Ordinal).ToArray(),
            draft.Ecos.OrderBy(eco => eco.EcoId, StringComparer.Ordinal).ToArray(),
            draft.Configs.OrderBy(config => config.ToolId.Value, StringComparer.Ordinal).ToArray(),
            draft.MaterialLots.OrderBy(lot => lot.LotId, StringComparer.Ordinal).ToArray(),
            DistinctUses(draft.MaterialUses),
            draft.Notices.OrderBy(notice => notice.NoticeId, StringComparer.Ordinal).ToArray(),
            draft.WorkOrders.OrderBy(order => order.WorkOrderId, StringComparer.Ordinal).ToArray(),
            draft.Calibrations.OrderBy(record => record.CalibrationId, StringComparer.Ordinal).ToArray());
    }

    private static void AddBaseline(EnterpriseDraft draft, IReadOnlyList<ToolSpec> tools)
    {
        foreach (var tool in tools)
        {
            var toolId = new ToolId(tool.Id);
            if (!draft.Configs.Exists(config => config.ToolId.Value == tool.Id))
            {
                draft.Configs.Add(new ConfigRevision($"CFG-{tool.Id}", toolId, "A", PartName(tool.Family)));
            }

            if (!draft.Calibrations.Exists(record => record.ToolId.Value == tool.Id))
            {
                draft.Calibrations.Add(new CalibrationRecord(
                    $"CAL-{tool.Id}-INST",
                    toolId,
                    Instrument(tool.Family),
                    WindowEnd.AddDays(30),
                    WindowStart.AddDays(20),
                    false));
            }

            var chamber = tool.Chambers.Count == 0 ? null : tool.Chambers[0].Id;
            for (var index = 0; index < 3; index++)
            {
                var id = $"WO-PM-{tool.Id}-{index:D2}";
                if (draft.WorkOrders.Exists(order => order.WorkOrderId == id))
                {
                    continue;
                }

                draft.WorkOrders.Add(new WorkOrder(
                    id,
                    toolId,
                    chamber,
                    "pm",
                    WindowStart.AddDays(7 + (index * 28)),
                    $"Scheduled preventive maintenance on {tool.Id}."));
            }
        }

        AddLot(draft, "WAF-14", "WAF-SI-300", "Northline Process Materials", WindowStart.AddDays(5), null, tools.Select(tool => tool.Id).ToArray());
        AddLot(draft, "SIH4-088", "GAS-SIH4", "Cinderwell Specialty Gases", WindowStart.AddDays(12), "SN-SIH4-088", ["D-01", "D-02"]);
        AddLot(draft, "CF4-014", "GAS-CF4", "Cinderwell Specialty Gases", WindowStart.AddDays(18), null, ["E-01", "E-02"]);
        AddLot(draft, "SC1-006", "CHEM-SC1", "Northline Process Materials", WindowStart.AddDays(9), null, ["C-01", "C-02"]);
        AddLot(draft, "QR-TANK-003", "QT-BATH", "Bramble Quartz Components", WindowStart.AddDays(15), null, ["C-01", "C-02"]);
        if (!draft.Notices.Exists(notice => notice.NoticeId == "SN-SIH4-088"))
        {
            draft.Notices.Add(new SupplierNotice(
                "SN-SIH4-088",
                "Cinderwell Specialty Gases",
                "GAS-SIH4",
                "SIH4-088",
                "Silane lot SIH4-088 matches the purchase specification.",
                WindowStart.AddDays(12).AddHours(4)));
        }

        draft.Ncrs.Add(new NcrRecord("NCR-C-01-104", "C-01", "Particle count above the bay limit during a bath change.", new DateTimeOffset(2026, 8, 4, 15, 0, 0, TimeSpan.Zero), "LOT-C-01-W02"));
        draft.Ncrs.Add(new NcrRecord("NCR-D-02-077", "D-02", "Thickness uniformity check requested by the bay lead.", new DateTimeOffset(2026, 8, 18, 11, 0, 0, TimeSpan.Zero), null));
        draft.Ncrs.Add(new NcrRecord("NCR-S-01-052", "S-01", "Ash endpoint delayed on one cassette.", new DateTimeOffset(2026, 9, 2, 9, 30, 0, TimeSpan.Zero), "LOT-S-01-W06"));
    }

    private static void AddHistory(EnterpriseDraft draft, IReadOnlyList<ToolSpec> tools, IReadOnlyList<ProcessRun> faultRuns, int seed)
    {
        var salt = seed % 7;
        if (salt < 0)
        {
            salt += 7;
        }

        for (var toolIndex = 0; toolIndex < tools.Count; toolIndex++)
        {
            var tool = tools[toolIndex];
            if (tool.Recipes.Count == 0)
            {
                throw new InvalidOperationException($"Tool {tool.Id} has no recipes.");
            }

            var toolId = new ToolId(tool.Id);
            for (var week = 0; week < Weeks; week++)
            {
                var recipe = tool.Recipes[(week + toolIndex + salt) % tool.Recipes.Count];
                var lotId = $"LOT-{tool.Id}-W{week:D2}";
                var start = WindowStart.AddDays(week * 7).AddHours(toolIndex);
                if (!draft.Lots.Exists(lot => lot.LotId == lotId && lot.ToolId.Value == tool.Id))
                {
                    draft.Lots.Add(new LotReference(lotId, "WAF-SI-300", toolId));
                }

                for (var wafer = 0; wafer < WafersPerWeek; wafer++)
                {
                    var at = start.AddHours(wafer * 2);
                    if (at > WindowEnd)
                    {
                        continue;
                    }

                    draft.Runs.Add(new ProcessRun(
                        $"RUN-BG-{seed}-{tool.Id}-W{week:D2}-{wafer:D2}",
                        toolId,
                        recipe.Id,
                        RevisionFor(faultRuns, draft, tool.Id, at),
                        lotId,
                        at));
                }
            }
        }
    }

    private static string RevisionFor(IReadOnlyList<ProcessRun> faultRuns, EnterpriseDraft draft, string toolId, DateTimeOffset startedAt)
    {
        var letters = new List<string>();
        foreach (var run in faultRuns)
        {
            if (run.ToolId.Value == toolId && !letters.Contains(run.RecipeRevision))
            {
                letters.Add(run.RecipeRevision);
            }
        }

        if (letters.Count == 0)
        {
            return "A";
        }

        if (letters.Count == 1)
        {
            return letters[0];
        }

        var config = draft.Configs.FirstOrDefault(item => item.ToolId.Value == toolId);
        EcoRecord? eco = null;
        foreach (var candidate in draft.Ecos)
        {
            if (string.Equals(candidate.ToolId, toolId, StringComparison.Ordinal) && (eco is null || candidate.EffectiveAt > eco.EffectiveAt))
            {
                eco = candidate;
            }
        }

        if (config is null || eco is null)
        {
            return letters[0];
        }

        if (startedAt < eco.EffectiveAt)
        {
            return letters.FirstOrDefault(letter => !string.Equals(letter, config.Revision, StringComparison.Ordinal)) ?? letters[0];
        }

        return config.Revision;
    }

    private static void AddLot(
        EnterpriseDraft draft,
        string lotId,
        string materialCode,
        string supplier,
        DateTimeOffset receivedAt,
        string? noticeId,
        IReadOnlyList<string> toolIds)
    {
        if (draft.MaterialLots.Exists(lot => lot.LotId == lotId))
        {
            return;
        }

        var lot = new MaterialLot(lotId, materialCode, supplier, receivedAt, noticeId);
        draft.MaterialLots.Add(lot);
        foreach (var toolId in toolIds)
        {
            draft.MaterialUses.Add(new MaterialUse(lotId, toolId));
            if (!draft.Lots.Exists(existing => existing.LotId == lotId && existing.ToolId.Value == toolId))
            {
                draft.Lots.Add(new LotReference(lotId, materialCode, new ToolId(toolId)));
            }
        }
    }

    private static LotReference[] DistinctLots(List<LotReference> lots)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var distinct = new List<LotReference>();
        foreach (var lot in lots.OrderBy(lot => lot.ToolId.Value, StringComparer.Ordinal).ThenBy(lot => lot.LotId, StringComparer.Ordinal))
        {
            if (seen.Add(lot.ToolId.Value + "|" + lot.LotId))
            {
                distinct.Add(lot);
            }
        }

        return distinct.ToArray();
    }

    private static MaterialUse[] DistinctUses(List<MaterialUse> uses)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var distinct = new List<MaterialUse>();
        foreach (var use in uses.OrderBy(use => use.LotId, StringComparer.Ordinal).ThenBy(use => use.ToolId, StringComparer.Ordinal))
        {
            if (seen.Add(use.LotId + "|" + use.ToolId))
            {
                distinct.Add(use);
            }
        }

        return distinct.ToArray();
    }

    private static string PartName(ToolFamily family)
    {
        return family switch
        {
            ToolFamily.Deposition => "Showerhead",
            ToolFamily.Etch => "Edge ring",
            ToolFamily.Strip => "Plasma tube",
            ToolFamily.Clean => "Bath vessel",
            _ => throw new InvalidOperationException("Unknown tool family."),
        };
    }

    private static string Instrument(ToolFamily family)
    {
        return family switch
        {
            ToolFamily.Deposition => "Heater thermocouple",
            ToolFamily.Etch => "RF generator",
            ToolFamily.Strip => "Plasma sensor",
            ToolFamily.Clean => "Bath thermocouple",
            _ => throw new InvalidOperationException("Unknown tool family."),
        };
    }
}
