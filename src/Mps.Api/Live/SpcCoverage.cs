// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Simulator.Fleet;

namespace Mps.Api.Live;

/// <summary>
/// Catalog helpers for charts and the console. Cumulative counters are not SPC series.
/// <see cref="LimitSigma"/> and <see cref="RecentReadings"/> are the numbers the catalog shows.
/// The rule engine does not use them as a gate; it evaluates every enabled reading from the bus.
/// </summary>
public static class SpcCoverage
{
    /// <summary>Control-limit sigma from spc-rules.yaml, shown with the chart. Not an evaluation pre-filter.</summary>
    public static double LimitSigma => Mps.Application.Spc.SpcRuleSettings.Shared.ControlLimitSigma;

    /// <summary>Catalog figure for how many recent readings a limit check described (spc-rules.yaml limitCheck.recentReadings).</summary>
    public static int RecentReadings => Mps.Application.Spc.SpcRuleSettings.Shared.LimitCheck.RecentReadings;

    public static bool IsCumulative(string parameter, string? sensorId) =>
        string.Equals(parameter, SensorCatalog.Output, StringComparison.OrdinalIgnoreCase)
        || parameter.EndsWith("-OUTPUT", StringComparison.OrdinalIgnoreCase)
        || (sensorId?.EndsWith("-OUTPUT", StringComparison.OrdinalIgnoreCase) ?? false);

    public static bool IsCumulative(SensorSpec sensor)
    {
        ArgumentNullException.ThrowIfNull(sensor);
        return sensor.IsCumulative || IsCumulative(sensor.Parameter ?? sensor.Id, sensor.Id);
    }

    /// <summary>True when the series is charted with limits, and so is checked by the rules engine.</summary>
    public static bool IsEvaluated(string parameter, string? sensorId) => !IsCumulative(parameter, sensorId);
}
