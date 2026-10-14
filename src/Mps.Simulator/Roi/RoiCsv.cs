// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Text;

namespace Mps.Simulator.Roi;

/// <summary>
/// Writes the ROI screen tables. Dollars are whole, quantities use two decimals, and rates use four.
/// </summary>
public static class RoiCsv
{
    public const string BaselineFileName = "baseline.csv";

    public const string ProjectionFileName = "projection.csv";

    public const string SensitivityFileName = "sensitivity.csv";

    private static readonly char[] QuotedCharacters = [',', '"', '\n', '\r'];

    public static void Write(RoiDataset dataset, string directory)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        var baseline = dataset.Baseline;
        var assumptions = baseline.Assumptions;
        WriteFile(
            Path.Combine(directory, BaselineFileName),
            "seed,label,scope,revenueUsd,copqRate,copqUsd,laborUsd,scrapUsd,warrantyUsd,downtimeUsd,incidents,mttrDays,hypothesisDays,repeatRate,capaCycleDays,investigationHours,engineerUsdPerHour,downtimeUsdPerHour,downtimeHours,repeatIncidents,aiUsdPerInvestigation,platformUsdPerYear,downtimeCapture,runCostUsd",
            Row(
                Integer(assumptions.Seed),
                Cell(assumptions.Label),
                Cell(assumptions.Scope),
                Money(assumptions.RevenueUsd),
                Rate(assumptions.CopqRate),
                Money(baseline.CopqUsd),
                Money(baseline.LaborUsd),
                Money(baseline.ScrapUsd),
                Money(baseline.WarrantyUsd),
                Money(baseline.DowntimeUsd),
                Integer(assumptions.IncidentsPerYear),
                Quantity(assumptions.MttrDays),
                Quantity(assumptions.HypothesisDays),
                Rate(assumptions.RepeatDefectRate),
                Quantity(assumptions.CapaCycleDays),
                Quantity(assumptions.HoursPerInvestigation),
                Money(assumptions.EngineerUsdPerHour),
                Money(assumptions.DowntimeUsdPerHour),
                Quantity(baseline.DowntimeHours),
                Quantity(baseline.RepeatIncidents),
                Money(assumptions.AiUsdPerInvestigation),
                Money(assumptions.PlatformUsdPerYear),
                Rate(assumptions.DowntimeCapture),
                Money(baseline.RunCostUsd)));
        WriteFile(
            Path.Combine(directory, ProjectionFileName),
            "seed,scenario,investigationHours,hypothesisDays,repeatRate,capaCycleDays,mttrDays,laborSavingsUsd,repeatScrapSavingsUsd,downtimeSavingsUsd,grossSavingsUsd,runCostUsd,netSavingsUsd,paybackMonths,downtimeHoursAvoided",
            ScenarioRow(assumptions.Seed, dataset.BaselineCase),
            ScenarioRow(assumptions.Seed, dataset.Conservative),
            ScenarioRow(assumptions.Seed, dataset.Expected));
        var sensitivity = new string[dataset.Sensitivity.Length];
        for (var index = 0; index < dataset.Sensitivity.Length; index++)
        {
            var row = dataset.Sensitivity[index];
            sensitivity[index] = Row(
                Integer(assumptions.Seed),
                Cell(row.Lever),
                Quantity(row.Value),
                Money(row.GrossSavingsUsd),
                Money(row.NetSavingsUsd),
                Months(row.PaybackMonths));
        }

        WriteFile(
            Path.Combine(directory, SensitivityFileName),
            "seed,lever,value,grossSavingsUsd,netSavingsUsd,paybackMonths",
            sensitivity);
    }

    private static string ScenarioRow(int seed, RoiScenario scenario)
    {
        return Row(
            Integer(seed),
            Cell(scenario.Name),
            Quantity(scenario.InvestigationHours),
            Quantity(scenario.HypothesisDays),
            Rate(scenario.RepeatRate),
            Quantity(scenario.CapaCycleDays),
            Quantity(scenario.MttrDays),
            Money(scenario.LaborSavingsUsd),
            Money(scenario.RepeatScrapSavingsUsd),
            Money(scenario.DowntimeSavingsUsd),
            Money(scenario.GrossSavingsUsd),
            Money(scenario.RunCostUsd),
            Money(scenario.NetSavingsUsd),
            Months(scenario.PaybackMonths),
            Quantity(scenario.DowntimeHoursAvoided));
    }

    private static void WriteFile(string path, string header, params string[] rows)
    {
        var builder = new StringBuilder();
        builder.Append(header);
        builder.Append('\n');
        foreach (var row in rows)
        {
            builder.Append(row);
            builder.Append('\n');
        }

        var text = builder.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
        File.WriteAllText(path, text);
    }

    private static string Row(params string[] cells)
    {
        return string.Join(',', cells);
    }

    private static string Cell(string value)
    {
        if (value.IndexOfAny(QuotedCharacters) >= 0)
        {
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return value;
    }

    private static string Money(decimal value)
    {
        return decimal.Round(value, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
    }

    private static string Quantity(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string Rate(decimal value)
    {
        return decimal.Round(value, 4, MidpointRounding.AwayFromZero).ToString("0.0000", CultureInfo.InvariantCulture);
    }

    private static string Months(decimal? value)
    {
        return value is null ? string.Empty : Quantity(value.Value);
    }

    private static string Integer(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
