// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Simulator.Roi;

/// <summary>
/// One synthetic year before any projection lever is applied.
/// </summary>
/// <param name="Assumptions">Inputs that produced the buckets.</param>
/// <param name="CopqUsd">Revenue times the COPQ rate, rounded to the nearest dollar.</param>
/// <param name="LaborUsd">Investigation labor. This is the only COPQ bucket with a direct hour rate in the plan.</param>
/// <param name="ScrapUsd">Truncated share of the remainder.</param>
/// <param name="WarrantyUsd">Truncated share of the remainder. This model does not reduce it.</param>
/// <param name="DowntimeUsd">Remainder after labor, scrap, and warranty, so the four buckets sum to COPQ.</param>
/// <param name="DowntimeHours">Downtime dollars divided by the hourly downtime rate. Unrounded.</param>
/// <param name="RepeatIncidents">Incidents times the repeat-defect rate. Unrounded.</param>
/// <param name="RunCostUsd">Annual cost to run after adoption. The current-state scenario does not pay it.</param>
public sealed record RoiBaseline(
    RoiAssumptions Assumptions,
    decimal CopqUsd,
    decimal LaborUsd,
    decimal ScrapUsd,
    decimal WarrantyUsd,
    decimal DowntimeUsd,
    decimal DowntimeHours,
    decimal RepeatIncidents,
    decimal RunCostUsd);

/// <summary>
/// Current state or one projected case. Dollar fields are already rounded. Day, hour, and rate fields are exact.
/// </summary>
/// <param name="Name">baseline, conservative, or expected.</param>
/// <param name="InvestigationHours">Hours per investigation after the lever.</param>
/// <param name="HypothesisDays">Days to a root-cause hypothesis after the lever.</param>
/// <param name="RepeatRate">Repeat-defect rate after the relative lever.</param>
/// <param name="CapaCycleDays">CAPA cycle after the lever. It does not change the dollar savings.</param>
/// <param name="MttrDays">MTTR-Q after removing the shortened hypothesis interval.</param>
/// <param name="LaborSavingsUsd">Rounded labor dollars avoided.</param>
/// <param name="RepeatScrapSavingsUsd">Rounded scrap dollars avoided on repeat incidents.</param>
/// <param name="DowntimeSavingsUsd">Rounded downtime dollars avoided.</param>
/// <param name="GrossSavingsUsd">Sum of the three rounded savings terms.</param>
/// <param name="RunCostUsd">Zero when gross savings are zero. Otherwise the annual adoption cost.</param>
/// <param name="NetSavingsUsd">Gross savings minus the run cost on this row.</param>
/// <param name="PaybackMonths">Run cost times 12 divided by gross. Null when gross is zero.</param>
/// <param name="DowntimeHoursAvoided">Unrounded hours. The dollar column is rounded separately.</param>
public sealed record RoiScenario(
    string Name,
    decimal InvestigationHours,
    decimal HypothesisDays,
    decimal RepeatRate,
    decimal CapaCycleDays,
    decimal MttrDays,
    decimal LaborSavingsUsd,
    decimal RepeatScrapSavingsUsd,
    decimal DowntimeSavingsUsd,
    decimal GrossSavingsUsd,
    decimal RunCostUsd,
    decimal NetSavingsUsd,
    decimal? PaybackMonths,
    decimal DowntimeHoursAvoided);

/// <summary>
/// One expected-case lever moved, with the other levers held at the expected case.
/// </summary>
/// <param name="Lever">investigation, hypothesis, repeat, or capture.</param>
/// <param name="Value">The lever value used for this row.</param>
/// <param name="GrossSavingsUsd">Rounded gross savings.</param>
/// <param name="NetSavingsUsd">Gross minus the annual run cost.</param>
/// <param name="PaybackMonths">Null when gross savings are zero.</param>
public sealed record RoiSensitivityRow(
    string Lever,
    decimal Value,
    decimal GrossSavingsUsd,
    decimal NetSavingsUsd,
    decimal? PaybackMonths);

/// <summary>
/// Baseline buckets, the three cases, and the sensitivity grid.
/// </summary>
/// <param name="Baseline">COPQ buckets for the year.</param>
/// <param name="BaselineCase">Current state. Savings and run cost are zero.</param>
/// <param name="Conservative">Conservative levers.</param>
/// <param name="Expected">Expected levers.</param>
/// <param name="Sensitivity">Twelve rows. The expected value of each lever matches <paramref name="Expected"/>.</param>
public sealed record RoiDataset(
    RoiBaseline Baseline,
    RoiScenario BaselineCase,
    RoiScenario Conservative,
    RoiScenario Expected,
    RoiSensitivityRow[] Sensitivity);
