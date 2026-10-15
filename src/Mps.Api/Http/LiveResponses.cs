// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Live;
using Mps.Simulator.Fleet;
using Mps.Simulator.Sites;
using Mps.Simulator.Telemetry;

namespace Mps.Api.Http;

/// <summary>Shapes the decimated simulator log into the payloads the console polls.</summary>
public static class LiveResponses
{
    public static FleetLiveResponse Fleet(PlantSimulation plant)
    {
        ArgumentNullException.ThrowIfNull(plant);
        var readings = plant.Log.Snapshot();
        if (readings.Count == 0)
        {
            return new FleetLiveResponse(plant.SimulatedNow, []);
        }

        var tools = readings
            .GroupBy(reading => reading.ToolId, StringComparer.Ordinal)
            .Select(group => ToolRow(plant, group.Key, group.ToArray()))
            .OrderBy(tool => tool.ToolId, StringComparer.Ordinal)
            .ToArray();
        return new FleetLiveResponse(plant.SimulatedNow, tools);
    }

    public static ToolLiveResponse? Tool(PlantSimulation plant, string toolId)
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
            .Select(reading => new LiveSeriesResponse(
                reading.Parameter,
                reading.ChamberId,
                plant.Log.Window(toolId, reading.ChamberId, reading.Parameter)
                    .Select(point => new LivePointResponse(point.At, point.Value))
                    .ToArray()))
            .ToArray();
        return new ToolLiveResponse(toolId, SiteOf(plant, toolId), plant.SimulatedNow, mapped.Status, mapped.Readings, series);
    }

    private static FleetLiveToolResponse ToolRow(PlantSimulation plant, string toolId, IReadOnlyList<LiveReading> readings)
    {
        var mapped = MapAll(plant, readings);
        return new FleetLiveToolResponse(toolId, SiteOf(plant, toolId), mapped.Status, mapped.Readings);
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

    private static string SiteOf(PlantSimulation plant, string toolId) =>
        plant.FindTool(toolId)?.SiteId ?? SiteIds.ForTool(toolId);

    private static LiveReadingResponse Map(PlantSimulation plant, LiveReading reading)
    {
        var window = plant.WindowAt(reading.ToolId, reading.Parameter, reading.At);
        return new LiveReadingResponse(
            reading.SensorId,
            reading.Parameter,
            reading.ChamberId,
            reading.Unit,
            reading.At,
            reading.Value,
            StatusOf(reading.Value, window));
    }

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
