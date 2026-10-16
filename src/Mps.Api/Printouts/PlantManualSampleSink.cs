// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Live;
using Mps.Application.Printouts;
using Mps.Simulator.Telemetry;

namespace Mps.Api.Printouts;

/// <summary>Turns a bench reading into a telemetry frame tagged manual and hands it to the plant's one ingest path.</summary>
public sealed class PlantManualSampleSink : IManualSampleSink
{
    private readonly PlantSimulation _plant;

    public PlantManualSampleSink(PlantSimulation plant)
    {
        ArgumentNullException.ThrowIfNull(plant);
        _plant = plant;
    }

    public bool Ingest(ManualSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        var frame = ToFrame(_plant, sample);
        if (frame is null)
        {
            return false;
        }

        _plant.Ingest([frame]);
        return true;
    }

    public static TelemetryFrame? ToFrame(PlantSimulation plant, ManualSample sample)
    {
        ArgumentNullException.ThrowIfNull(plant);
        ArgumentNullException.ThrowIfNull(sample);
        var placed = plant.World.FindTool(sample.ToolId);
        if (placed is null)
        {
            return null;
        }

        var sensor = placed.Spec.Sensors.FirstOrDefault(item =>
                string.Equals(item.Parameter, sample.Parameter, StringComparison.Ordinal)
                && (sample.ChamberId is null || string.Equals(item.ChamberId, sample.ChamberId, StringComparison.OrdinalIgnoreCase)))
            ?? placed.Spec.Sensors.FirstOrDefault(item => string.Equals(item.Parameter, sample.Parameter, StringComparison.Ordinal));
        var chamber = sensor?.ChamberId ?? sample.ChamberId ?? plant.DefaultChamber(sample.ToolId);
        return new TelemetryFrame(
            placed.Spec.Id,
            chamber,
            sample.Parameter,
            sample.Unit,
            sensor?.Id ?? placed.Spec.Id + "-" + sample.Parameter,
            sample.ObservedAt,
            sample.Value,
            "manual",
            "manual",
            "manual-" + sample.EvidenceId.ToString("N"),
            "manual")
        {
            Source = TelemetryFrame.Sources.Manual,
            EvidenceId = sample.EvidenceId,
        };
    }
}
