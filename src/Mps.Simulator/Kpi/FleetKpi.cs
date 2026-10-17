// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Text;

using Mps.Simulator.Telemetry;

namespace Mps.Simulator.Kpi;

/// <summary>
/// One tool's seeded 90-day figures. Rates are while the tool is up. OEE folds in uptime.
/// </summary>
public sealed record ToolKpi(
    string ToolId,
    decimal RunRateWafersPerHour,
    decimal UptimePercent,
    decimal OeePercent,
    decimal FirstPassYieldPercent,
    decimal MtbfHours,
    decimal MttrHours,
    decimal OperatingHours,
    int FailureCount,
    decimal RepairHours);

/// <summary>
/// Fleet rollup. Run rate is the sum of the tool run rates. OEE uses the average run rate.
/// </summary>
public sealed record FleetKpiRollup(
    decimal RunRateWafersPerHour,
    decimal UptimePercent,
    decimal OeePercent,
    decimal FirstPassYieldPercent,
    decimal MtbfHours,
    decimal MttrHours,
    decimal OperatingHours,
    int FailureCount,
    decimal RepairHours,
    int ToolCount);

/// <summary>
/// The snapshot written to <c>data/kpis/fleet.yaml</c>.
/// </summary>
public sealed record FleetKpiSnapshot(
    int Seed,
    int PlannedHours,
    decimal IdealWafersPerHour,
    IReadOnlyList<ToolKpi> Tools,
    FleetKpiRollup Fleet);

/// <summary>
/// Seeded, reproducible tool and fleet figures for the demo window.
/// Draw order on each tool is part of the seed: performance, uptime, first-pass yield, failures, then repair time.
/// </summary>
public static class FleetKpiCalculator
{
    public const int DemoSeed = Demo.DemoDefaults.EnterpriseSeed;

    public const int PlannedHours = 2160;

    public const decimal IdealWafersPerHour = 12m;

    public static FleetKpiSnapshot Calculate(IEnumerable<string> toolIds, int seed = DemoSeed)
    {
        ArgumentNullException.ThrowIfNull(toolIds);
        var ids = toolIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0)
        {
            throw new ArgumentException("At least one tool id is required.", nameof(toolIds));
        }

        var tools = ids.Select(id => CalculateTool(id, seed)).ToArray();
        return new FleetKpiSnapshot(seed, PlannedHours, IdealWafersPerHour, tools, Rollup(tools));
    }

    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static ToolKpi CalculateTool(string toolId, int seed)
    {
        var rng = SeededRng.Create(seed, "kpi|" + toolId);
        var performance = Round2(0.90m + (0.08m * Unit(rng)));
        var runRate = Round2(IdealWafersPerHour * performance);
        var uptime = Round2(92m + (6m * Unit(rng)));
        var firstPassYield = Round2(94m + (5m * Unit(rng)));
        var oee = Round2((uptime / 100m) * (runRate / IdealWafersPerHour) * (firstPassYield / 100m) * 100m);
        var failures = 3 + (int)Math.Floor(Unit(rng) * 4m);
        var mttr = Round2(1.5m + (4.5m * Unit(rng)));
        var operating = Round2(PlannedHours * uptime / 100m);
        var mtbf = Round2(operating / failures);
        var repair = Round2(failures * mttr);
        return new ToolKpi(toolId, runRate, uptime, oee, firstPassYield, mtbf, mttr, operating, failures, repair);
    }

    private static FleetKpiRollup Rollup(ToolKpi[] tools)
    {
        var runRate = tools.Sum(tool => tool.RunRateWafersPerHour);
        var operating = tools.Sum(tool => tool.OperatingHours);
        var failures = tools.Sum(tool => tool.FailureCount);
        var repair = tools.Sum(tool => tool.RepairHours);
        var uptime = Round2(operating / (PlannedHours * tools.Length) * 100m);
        var firstPassYield = Round2(tools.Sum(tool => tool.FirstPassYieldPercent * tool.RunRateWafersPerHour) / runRate);
        var averageRunRate = runRate / tools.Length;
        var oee = Round2((uptime / 100m) * (averageRunRate / IdealWafersPerHour) * (firstPassYield / 100m) * 100m);
        var mtbf = Round2(operating / failures);
        var mttr = Round2(repair / failures);
        return new FleetKpiRollup(runRate, uptime, oee, firstPassYield, mtbf, mttr, operating, failures, repair, tools.Length);
    }

    private static decimal Unit(SeededRng rng) => (decimal)rng.NextUnit();
}

/// <summary>
/// Writes the KPI snapshot as LF YAML so the committed file can be byte-compared.
/// </summary>
public static class FleetKpiYaml
{
    public const string FileName = "fleet.yaml";

    public static void Write(FleetKpiSnapshot snapshot, string directory)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, FileName), Render(snapshot));
    }

    public static string Render(FleetKpiSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var text = new StringBuilder();
        text.Append("# Copyright (c) 2026 Bryan C. Weis and Common Consulting Company\n");
        text.Append("# Seeded 90-day tool and fleet figures. The formula is in README.md. All figures are synthetic.\n");
        text.Append(CultureInfo.InvariantCulture, $"seed: {snapshot.Seed}\n");
        text.Append(CultureInfo.InvariantCulture, $"plannedHours: {snapshot.PlannedHours}\n");
        text.Append(CultureInfo.InvariantCulture, $"idealWafersPerHour: {Number(snapshot.IdealWafersPerHour)}\n");
        text.Append("tools:\n");
        foreach (var tool in snapshot.Tools)
        {
            text.Append(CultureInfo.InvariantCulture, $"  - toolId: {tool.ToolId}\n");
            text.Append(CultureInfo.InvariantCulture, $"    runRateWafersPerHour: {Number(tool.RunRateWafersPerHour)}\n");
            text.Append(CultureInfo.InvariantCulture, $"    uptimePercent: {Number(tool.UptimePercent)}\n");
            text.Append(CultureInfo.InvariantCulture, $"    oeePercent: {Number(tool.OeePercent)}\n");
            text.Append(CultureInfo.InvariantCulture, $"    firstPassYieldPercent: {Number(tool.FirstPassYieldPercent)}\n");
            text.Append(CultureInfo.InvariantCulture, $"    mtbfHours: {Number(tool.MtbfHours)}\n");
            text.Append(CultureInfo.InvariantCulture, $"    mttrHours: {Number(tool.MttrHours)}\n");
            text.Append(CultureInfo.InvariantCulture, $"    operatingHours: {Number(tool.OperatingHours)}\n");
            text.Append(CultureInfo.InvariantCulture, $"    failureCount: {tool.FailureCount}\n");
            text.Append(CultureInfo.InvariantCulture, $"    repairHours: {Number(tool.RepairHours)}\n");
        }

        var fleet = snapshot.Fleet;
        text.Append("fleet:\n");
        text.Append(CultureInfo.InvariantCulture, $"  runRateWafersPerHour: {Number(fleet.RunRateWafersPerHour)}\n");
        text.Append(CultureInfo.InvariantCulture, $"  uptimePercent: {Number(fleet.UptimePercent)}\n");
        text.Append(CultureInfo.InvariantCulture, $"  oeePercent: {Number(fleet.OeePercent)}\n");
        text.Append(CultureInfo.InvariantCulture, $"  firstPassYieldPercent: {Number(fleet.FirstPassYieldPercent)}\n");
        text.Append(CultureInfo.InvariantCulture, $"  mtbfHours: {Number(fleet.MtbfHours)}\n");
        text.Append(CultureInfo.InvariantCulture, $"  mttrHours: {Number(fleet.MttrHours)}\n");
        text.Append(CultureInfo.InvariantCulture, $"  operatingHours: {Number(fleet.OperatingHours)}\n");
        text.Append(CultureInfo.InvariantCulture, $"  failureCount: {fleet.FailureCount}\n");
        text.Append(CultureInfo.InvariantCulture, $"  repairHours: {Number(fleet.RepairHours)}\n");
        text.Append(CultureInfo.InvariantCulture, $"  toolCount: {fleet.ToolCount}\n");
        return text.ToString();
    }

    private static string Number(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
