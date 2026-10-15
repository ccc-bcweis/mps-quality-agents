// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Application.Bus;

/// <summary>How far back a nominal-history seed should reach. The clock ends at the simulator's current instant.</summary>
public enum NominalWindow
{
    LastHour,
    LastDay,
    LastWeek,
}

/// <summary>
/// What a seed run stored, plus the full-rate totals that were calculated and not inserted.
/// Chart times are the 95th percentile of repeated reads for one tool and one parameter.
/// </summary>
public sealed record NominalSeedResult(
    NominalWindow Window,
    int ToolCount,
    int ParameterCount,
    long ReadingCount,
    double EventsPerSecond,
    double RowsPerSecond,
    double SeedSeconds,
    long DatabaseBytes,
    double ChartP95Milliseconds15Minutes,
    double ChartP95Milliseconds1Hour,
    double ChartP95Milliseconds7Days,
    string Policy,
    int RecentStrideSeconds,
    int OlderStrideSeconds,
    long ProjectedRows1Hour,
    long ProjectedRows1Day,
    long ProjectedRows7Days,
    double SampleEventsPerSecond);

/// <summary>Other lanes call this to fill the telemetry table with a healthy history.</summary>
public interface INominalHistorySeeder
{
    Task<NominalSeedResult> SeedAsync(NominalWindow window, CancellationToken cancellationToken = default);
}
