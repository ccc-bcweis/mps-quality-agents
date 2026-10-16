// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Live;
using Mps.Simulator.Fleet;
using Mps.Simulator.Sites;
using Mps.Simulator.Telemetry;

namespace Mps.Api.Http;

/// <summary>Shapes the decimated simulator log into the payloads the console polls.</summary>
public static class LiveResponses
{
    public static FleetLiveResponse Fleet(PlantSimulation plant, IReadOnlyDictionary<string, string>? alertStatus = null)
    {
        ArgumentNullException.ThrowIfNull(plant);
        var readings = plant.Log.Snapshot();
        if (readings.Count == 0)
        {
            return new FleetLiveResponse(plant.SimulatedNow, []);
        }

        var tools = readings
            .GroupBy(reading => reading.ToolId, StringComparer.Ordinal)
            .Select(group => ToolRow(plant, group.Key, group.ToArray(), alertStatus))
            .OrderBy(tool => tool.ToolId, StringComparer.Ordinal)
            .ToArray();
        return new FleetLiveResponse(plant.SimulatedNow, tools);
    }

    public static ToolLiveResponse? Tool(PlantSimulation plant, string toolId, IReadOnlyDictionary<string, string>? alertStatus = null)
    {
        ArgumentNullException.ThrowIfNull(plant);
        if (string.IsNullOrWhiteSpace(toolId) || plant.FindTool(toolId) is null)
        {
            return null;
        }

        var readings = plant.Log.SnapshotForTool(toolId);
        var mapped = MapAll(plant, readings);
        var series = readings
            .OrderBy(reading => reading.Parameter, StringComparer.Ordinal)
            .ThenBy(reading => reading.ChamberId, StringComparer.Ordinal)
            .Select(reading =>
            {
                var window = IsCounter(reading.Parameter, reading.SensorId) ? null : plant.WindowAt(toolId, reading.Parameter, reading.At);
                return new LiveSeriesResponse(
                    reading.Parameter,
                    reading.ChamberId,
                    plant.Log.Window(toolId, reading.ChamberId, reading.Parameter)
                        .Select(point => new LivePointResponse(point.At, point.Value))
                        .ToArray(),
                    window?.Nominal,
                    window is null ? null : Math.Max(window.Sigma, 1e-6));
            })
            .ToArray();
        return new ToolLiveResponse(toolId, SiteOf(plant, toolId), plant.SimulatedNow, alertStatus is null ? mapped.Status : ToolAlertStatus.Of(alertStatus, toolId), mapped.Readings, series);
    }

    private static FleetLiveToolResponse ToolRow(PlantSimulation plant, string toolId, IReadOnlyList<LiveReading> readings, IReadOnlyDictionary<string, string>? alertStatus)
    {
        var mapped = MapAll(plant, readings);
        return new FleetLiveToolResponse(toolId, SiteOf(plant, toolId), alertStatus is null ? mapped.Status : ToolAlertStatus.Of(alertStatus, toolId), mapped.Readings);
    }

    private static (string Status, LiveReadingResponse[] Readings) MapAll(PlantSimulation plant, IReadOnlyList<LiveReading> readings)
    {
        var mapped = readings
            .OrderBy(reading => reading.Parameter, StringComparer.Ordinal)
            .ThenBy(reading => reading.ChamberId, StringComparer.Ordinal)
            .Select(reading => Map(plant, reading))
            .ToArray();
        var status = mapped.Length == 0 ? "nominal" : mapped.Select(reading => reading.Status).Aggregate(Worse);
        return (status, mapped);
    }

    public static string SiteOf(PlantSimulation plant, string toolId) =>
        plant.FindTool(toolId)?.SiteId ?? SiteIds.ForTool(toolId);

    private static LiveReadingResponse Map(PlantSimulation plant, LiveReading reading)
    {
        var counter = IsCounter(reading.Parameter, reading.SensorId);
        var window = counter ? null : plant.WindowAt(reading.ToolId, reading.Parameter, reading.At);
        double? sigma = window is null ? null : Math.Max(window.Sigma, 1e-6);
        double? controlLow = window is null ? null : window.Nominal - (3d * sigma!.Value);
        double? controlHigh = window is null ? null : window.Nominal + (3d * sigma!.Value);
        return new LiveReadingResponse(
            reading.SensorId,
            reading.Parameter,
            reading.ChamberId,
            reading.Unit,
            reading.At,
            reading.Value,
            counter ? "nominal" : StatusOf(reading.Value, window),
            window?.Minimum,
            window?.Maximum,
            window?.Nominal,
            controlLow,
            controlHigh,
            window is null ? null : Mps.Domain.Evidence.LimitState.Of(reading.Value, window.Minimum, window.Maximum, controlLow, controlHigh));
    }

    /// <summary>The wafer output counter climbs all hour by design, so it never sets a watch or alarm.</summary>
    private static bool IsCounter(string parameter, string? sensorId) => Mps.Api.Live.SpcCoverage.IsCumulative(parameter, sensorId);

    private static string StatusOf(double value, OperatingWindow? window)
    {
        if (window is null || !double.IsFinite(value))
        {
            return "nominal";
        }

        if (value < window.Minimum || value > window.Maximum)
        {
            return "alarm";
        }

        var sigma = Math.Max(window.Sigma, 1e-6);
        return Math.Abs(value - window.Nominal) > (2d * sigma) ? "watch" : "nominal";
    }

    private static string Worse(string left, string right)
    {
        return Rank(left) >= Rank(right) ? left : right;
    }

    private static int Rank(string status) => status switch
    {
        "alarm" => 2,
        "watch" => 1,
        _ => 0,
    };
}
