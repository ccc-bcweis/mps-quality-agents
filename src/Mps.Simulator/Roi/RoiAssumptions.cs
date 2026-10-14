// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Simulator.Seed;

namespace Mps.Simulator.Roi;

/// <summary>
/// Illustrative, synthetic inputs for one fictional 3-plant year. The seed is stamped on the tables and is not a factor in the dollar formulas.
/// </summary>
public sealed record RoiAssumptions
{
    public int Seed { get; init; } = EnterpriseSeedGenerator.DemoSeed;

    public string Label { get; init; } = "illustrative, synthetic";

    public string Scope { get; init; } = "3-plant MPS";

    public decimal RevenueUsd { get; init; } = 1_200_000_000m;

    public decimal CopqRate { get; init; } = 0.08m;

    public int IncidentsPerYear { get; init; } = 1400;

    public decimal MttrDays { get; init; } = 9.5m;

    public decimal HypothesisDays { get; init; } = 4m;

    public decimal RepeatDefectRate { get; init; } = 0.18m;

    public decimal CapaCycleDays { get; init; } = 62m;

    public decimal EngineerUsdPerHour { get; init; } = 95m;

    public decimal HoursPerInvestigation { get; init; } = 22m;

    public decimal DowntimeUsdPerHour { get; init; } = 12_000m;

    /// <summary>Share of COPQ after investigation labor. The downtime bucket is whatever remains.</summary>
    public decimal ScrapShareOfRemainder { get; init; } = 0.50m;

    /// <summary>Share of COPQ after investigation labor. The downtime bucket is whatever remains.</summary>
    public decimal WarrantyShareOfRemainder { get; init; } = 0.22m;

    /// <summary>Lane assumption. PLAN names the category and does not set the price.</summary>
    public decimal AiUsdPerInvestigation { get; init; } = 4m;

    /// <summary>Lane assumption. PLAN names the category and does not set the price.</summary>
    public decimal PlatformUsdPerYear { get; init; } = 180_000m;

    /// <summary>Lane assumption. Share of downtime dollars that earlier detection can avoid.</summary>
    public decimal DowntimeCapture { get; init; } = 0.35m;

    public decimal ConservativeInvestigationReduction { get; init; } = 0.30m;

    public decimal ExpectedInvestigationReduction { get; init; } = 0.45m;

    public decimal ConservativeHypothesisReduction { get; init; } = 0.50m;

    public decimal ExpectedHypothesisReduction { get; init; } = 0.70m;

    public decimal ConservativeRepeatReduction { get; init; } = 0.15m;

    public decimal ExpectedRepeatReduction { get; init; } = 0.25m;

    public decimal ConservativeCapaReduction { get; init; } = 0.20m;

    public decimal ExpectedCapaReduction { get; init; } = 0.35m;
}

/// <summary>
/// Relative reductions applied to the baseline. Zero is the current state.
/// </summary>
public sealed record RoiLevers
{
    public decimal InvestigationReduction { get; init; }

    public decimal HypothesisReduction { get; init; }

    public decimal RepeatReduction { get; init; }

    public decimal CapaReduction { get; init; }

    public decimal DowntimeCapture { get; init; }
}
