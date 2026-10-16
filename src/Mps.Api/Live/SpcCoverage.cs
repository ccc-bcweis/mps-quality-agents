// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Simulator.Fleet;

namespace Mps.Api.Live;

/// <summary>
/// Which live series get SPC limits and rule checks. Periodic series do; cumulative counters (a running count of
/// wafers made) do not, because they climb by design. Anything charted with limits must be in <see cref="IsEvaluated"/>.
/// </summary>
public static class SpcCoverage
{
    /// <summary>Limit rules applied to every periodic series, faulted or not.</summary>
    public const double LimitSigma = 3d;

    /// <summary>How many recent readings a limit check looks at.</summary>
    public const int RecentReadings = 20;

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
