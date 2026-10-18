// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Telemetry;
using Mps.Simulator.Fleet;

namespace Mps.Simulator.Telemetry;

/// <summary>
/// On-demand series for one parameter. Replays from the simulator origin so a window matches the live stream.
/// </summary>
public static class FleetTelemetry
{
    public static IReadOnlyList<TelemetryPoint> Generate(
        ToolSpec tool,
        string chamberId,
        string parameter,
        int seed,
        DateTimeOffset origin,
        DateTimeOffset from,
        DateTimeOffset until,
        IReadOnlyList<FaultSignal>? faults = null)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (string.IsNullOrWhiteSpace(chamberId))
        {
            throw new ArgumentException("Chamber id is required.", nameof(chamberId));
        }

        if (string.IsNullOrWhiteSpace(parameter))
        {
            throw new ArgumentException("Parameter is required.", nameof(parameter));
        }

        if (until < from)
        {
            throw new ArgumentOutOfRangeException(nameof(until), "Window end is before its start.");
        }

        if (from < origin)
        {
            throw new ArgumentOutOfRangeException(nameof(from), "Window starts before the simulator origin.");
        }

        var applied = faults ?? [];
        var cursor = new SeriesCursor(seed, TelemetryMath.SeriesKey(tool.Id, chamberId.Trim(), parameter.Trim()));
        var points = new List<TelemetryPoint>();
        var elapsed = 0;
        var at = origin;
        while (at <= until)
        {
            var schedule = RecipeSchedule.Locate(tool, elapsed);
            if (schedule.Step.WindowFor(parameter.Trim()) is { } window)
            {
                var value = cursor.Next(window, at, applied, tool.Id, chamberId.Trim(), parameter.Trim());
                if (at >= from)
                {
                    points.Add(new TelemetryPoint(at, value));
                }
            }

            elapsed++;
            at = origin.AddSeconds(elapsed);
        }

        return points;
    }
}
