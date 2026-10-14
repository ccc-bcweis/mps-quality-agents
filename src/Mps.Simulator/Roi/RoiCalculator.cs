// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Simulator.Roi;

/// <summary>
/// Turns plan inputs into a reproducible ROI dataset. Dollar formulas ignore the seed.
/// </summary>
public static class RoiCalculator
{
    public static RoiDataset Calculate(RoiAssumptions assumptions)
    {
        ArgumentNullException.ThrowIfNull(assumptions);
        var baseline = BuildBaseline(assumptions);
        var expectedLevers = ExpectedLevers(assumptions);
        return new RoiDataset(
            baseline,
            Project(baseline, "baseline", new RoiLevers()),
            Project(baseline, "conservative", ConservativeLevers(assumptions)),
            Project(baseline, "expected", expectedLevers),
            Sensitivity(baseline, expectedLevers));
    }

    public static RoiScenario Project(RoiAssumptions assumptions, string name, RoiLevers levers)
    {
        ArgumentNullException.ThrowIfNull(assumptions);
        return Project(BuildBaseline(assumptions), name, levers);
    }

    private static RoiBaseline BuildBaseline(RoiAssumptions assumptions)
    {
        if (string.IsNullOrWhiteSpace(assumptions.Label) || string.IsNullOrWhiteSpace(assumptions.Scope))
        {
            throw new InvalidOperationException("The ROI label and scope are required.");
        }
        if (assumptions.IncidentsPerYear <= 0)
        {
            throw new InvalidOperationException("Incidents per year must be positive.");
        }

        RequirePositive(assumptions.RevenueUsd, "Revenue");
        RequireRange(assumptions.CopqRate, 0m, 1m, "COPQ rate", lowerExclusive: true);
        RequireNonNegative(assumptions.MttrDays, "MTTR-Q");
        RequireNonNegative(assumptions.HypothesisDays, "Hypothesis days");
        RequireRange(assumptions.RepeatDefectRate, 0m, 1m, "Repeat-defect rate", lowerExclusive: false);
        RequireNonNegative(assumptions.CapaCycleDays, "CAPA cycle");
        RequireNonNegative(assumptions.EngineerUsdPerHour, "Engineer rate");
        RequireNonNegative(assumptions.HoursPerInvestigation, "Hours per investigation");
        RequirePositive(assumptions.DowntimeUsdPerHour, "Downtime rate");
        RequireRange(assumptions.ScrapShareOfRemainder, 0m, 1m, "Scrap share", lowerExclusive: false);
        RequireRange(assumptions.WarrantyShareOfRemainder, 0m, 1m, "Warranty share", lowerExclusive: false);
        if (assumptions.ScrapShareOfRemainder + assumptions.WarrantyShareOfRemainder > 1m)
        {
            throw new InvalidOperationException("Scrap and warranty shares exceed the remainder.");
        }

        RequireNonNegative(assumptions.AiUsdPerInvestigation, "AI cost per investigation");
        RequireNonNegative(assumptions.PlatformUsdPerYear, "Platform cost");
        RequireRange(assumptions.DowntimeCapture, 0m, 1m, "Downtime capture", lowerExclusive: false);
        RequireRange(assumptions.ConservativeInvestigationReduction, 0m, 1m, "Conservative investigation reduction", lowerExclusive: false);
        RequireRange(assumptions.ExpectedInvestigationReduction, 0m, 1m, "Expected investigation reduction", lowerExclusive: false);
        RequireRange(assumptions.ConservativeHypothesisReduction, 0m, 1m, "Conservative hypothesis reduction", lowerExclusive: false);
        RequireRange(assumptions.ExpectedHypothesisReduction, 0m, 1m, "Expected hypothesis reduction", lowerExclusive: false);
        RequireRange(assumptions.ConservativeRepeatReduction, 0m, 1m, "Conservative repeat reduction", lowerExclusive: false);
        RequireRange(assumptions.ExpectedRepeatReduction, 0m, 1m, "Expected repeat reduction", lowerExclusive: false);
        RequireRange(assumptions.ConservativeCapaReduction, 0m, 1m, "Conservative CAPA reduction", lowerExclusive: false);
        RequireRange(assumptions.ExpectedCapaReduction, 0m, 1m, "Expected CAPA reduction", lowerExclusive: false);

        var copq = RoundUsd(assumptions.RevenueUsd * assumptions.CopqRate);
        var labor = RoundUsd(assumptions.IncidentsPerYear * assumptions.HoursPerInvestigation * assumptions.EngineerUsdPerHour);
        if (labor > copq)
        {
            throw new InvalidOperationException("Investigation labor exceeds COPQ.");
        }

        var remainder = copq - labor;
        var scrap = decimal.Truncate(remainder * assumptions.ScrapShareOfRemainder);
        var warranty = decimal.Truncate(remainder * assumptions.WarrantyShareOfRemainder);
        var downtime = remainder - scrap - warranty;
        var runCost = RoundUsd((assumptions.IncidentsPerYear * assumptions.AiUsdPerInvestigation) + assumptions.PlatformUsdPerYear);
        return new RoiBaseline(
            assumptions,
            copq,
            labor,
            scrap,
            warranty,
            downtime,
            downtime / assumptions.DowntimeUsdPerHour,
            assumptions.IncidentsPerYear * assumptions.RepeatDefectRate,
            runCost);
    }

    private static RoiScenario Project(RoiBaseline baseline, string name, RoiLevers levers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(levers);
        var assumptions = baseline.Assumptions;
        RequireRange(levers.InvestigationReduction, 0m, 1m, "Investigation reduction", lowerExclusive: false);
        RequireRange(levers.HypothesisReduction, 0m, 1m, "Hypothesis reduction", lowerExclusive: false);
        RequireRange(levers.RepeatReduction, 0m, 1m, "Repeat reduction", lowerExclusive: false);
        RequireRange(levers.CapaReduction, 0m, 1m, "CAPA reduction", lowerExclusive: false);
        RequireRange(levers.DowntimeCapture, 0m, 1m, "Downtime capture", lowerExclusive: false);
        if (assumptions.HypothesisDays * levers.HypothesisReduction > assumptions.MttrDays)
        {
            throw new InvalidOperationException("The hypothesis reduction removes more days than MTTR-Q contains.");
        }

        var laborSavings = RoundUsd(baseline.LaborUsd * levers.InvestigationReduction);
        var repeatScrap = RoundUsd(baseline.RepeatIncidents * (baseline.ScrapUsd / assumptions.IncidentsPerYear) * levers.RepeatReduction);
        var downtimeSavings = RoundUsd(baseline.DowntimeUsd * levers.HypothesisReduction * levers.DowntimeCapture);
        var gross = laborSavings + repeatScrap + downtimeSavings;
        var runCost = gross == 0m ? 0m : baseline.RunCostUsd;
        decimal? payback = gross == 0m ? null : (baseline.RunCostUsd * 12m) / gross;
        return new RoiScenario(
            name,
            assumptions.HoursPerInvestigation * (1m - levers.InvestigationReduction),
            assumptions.HypothesisDays * (1m - levers.HypothesisReduction),
            assumptions.RepeatDefectRate * (1m - levers.RepeatReduction),
            assumptions.CapaCycleDays * (1m - levers.CapaReduction),
            assumptions.MttrDays - (assumptions.HypothesisDays * levers.HypothesisReduction),
            laborSavings,
            repeatScrap,
            downtimeSavings,
            gross,
            runCost,
            gross - runCost,
            payback,
            baseline.DowntimeHours * levers.HypothesisReduction * levers.DowntimeCapture);
    }

    private static RoiLevers ConservativeLevers(RoiAssumptions assumptions)
    {
        return new RoiLevers
        {
            InvestigationReduction = assumptions.ConservativeInvestigationReduction,
            HypothesisReduction = assumptions.ConservativeHypothesisReduction,
            RepeatReduction = assumptions.ConservativeRepeatReduction,
            CapaReduction = assumptions.ConservativeCapaReduction,
            DowntimeCapture = assumptions.DowntimeCapture,
        };
    }

    private static RoiLevers ExpectedLevers(RoiAssumptions assumptions)
    {
        return new RoiLevers
        {
            InvestigationReduction = assumptions.ExpectedInvestigationReduction,
            HypothesisReduction = assumptions.ExpectedHypothesisReduction,
            RepeatReduction = assumptions.ExpectedRepeatReduction,
            CapaReduction = assumptions.ExpectedCapaReduction,
            DowntimeCapture = assumptions.DowntimeCapture,
        };
    }

    private static RoiSensitivityRow[] Sensitivity(RoiBaseline baseline, RoiLevers expected)
    {
        var rows = new RoiSensitivityRow[SensitivityGrid.Length];
        for (var index = 0; index < SensitivityGrid.Length; index++)
        {
            var point = SensitivityGrid[index];
            var scenario = Project(baseline, point.Lever, With(expected, point.Lever, point.Value));
            rows[index] = new RoiSensitivityRow(point.Lever, point.Value, scenario.GrossSavingsUsd, scenario.NetSavingsUsd, scenario.PaybackMonths);
        }

        return rows;
    }

    private static RoiLevers With(RoiLevers levers, string lever, decimal value)
    {
        return lever switch
        {
            "investigation" => levers with { InvestigationReduction = value },
            "hypothesis" => levers with { HypothesisReduction = value },
            "repeat" => levers with { RepeatReduction = value },
            "capture" => levers with { DowntimeCapture = value },
            _ => throw new InvalidOperationException($"Unknown sensitivity lever {lever}."),
        };
    }

    private static decimal RoundUsd(decimal value)
    {
        return decimal.Round(value, 0, MidpointRounding.AwayFromZero);
    }

    private static void RequirePositive(decimal value, string label)
    {
        if (value <= 0m)
        {
            throw new InvalidOperationException($"{label} must be positive.");
        }
    }

    private static void RequireNonNegative(decimal value, string label)
    {
        if (value < 0m)
        {
            throw new InvalidOperationException($"{label} must be zero or positive.");
        }
    }

    private static void RequireRange(decimal value, decimal min, decimal max, string label, bool lowerExclusive)
    {
        var below = lowerExclusive ? value <= min : value < min;
        if (below || value > max)
        {
            throw new InvalidOperationException($"{label} must be between 0 and 1.");
        }
    }

    private readonly record struct SensitivityPoint(string Lever, decimal Value);

    private static readonly SensitivityPoint[] SensitivityGrid =
    [
        new("investigation", 0.30m),
        new("investigation", 0.45m),
        new("investigation", 0.60m),
        new("hypothesis", 0.50m),
        new("hypothesis", 0.70m),
        new("hypothesis", 0.80m),
        new("repeat", 0.15m),
        new("repeat", 0.25m),
        new("repeat", 0.35m),
        new("capture", 0.25m),
        new("capture", 0.35m),
        new("capture", 0.45m),
    ];
}
