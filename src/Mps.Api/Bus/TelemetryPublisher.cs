// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Live;
using Mps.Application.Bus;
using Mps.Domain.Events;
using Mps.Simulator.Telemetry;

namespace Mps.Api.Bus;

/// <summary>Publishes simulator frames after they are stored in the live ring, and waits until the telemetry table has them.</summary>
public sealed class TelemetryPublisher
{
    private const int ChunkSize = EventBatch.Size;

    private readonly PlantSimulation _plant;
    private readonly ILifecycleEventSink _sink;
    private readonly ICascadeDirectory _cascade;

    public TelemetryPublisher(PlantSimulation plant, ILifecycleEventSink sink, ICascadeDirectory cascade)
    {
        ArgumentNullException.ThrowIfNull(plant);
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(cascade);
        _plant = plant;
        _sink = sink;
        _cascade = cascade;
    }

    public async Task PublishPendingAsync(CancellationToken cancellationToken)
    {
        var frames = _plant.TakePendingFrames();
        if (frames.Count == 0)
        {
            return;
        }

        for (var offset = 0; offset < frames.Count; offset += ChunkSize)
        {
            var count = Math.Min(ChunkSize, frames.Count - offset);
            var drafts = new LifecycleEvent[count];
            for (var index = 0; index < count; index++)
            {
                drafts[index] = ToDraft(frames[offset + index]);
            }

            await _sink.PublishManyAsync(drafts, cancellationToken).ConfigureAwait(false);
        }
    }

    private LifecycleEvent ToDraft(TelemetryFrame frame)
    {
        var parent = _cascade.FaultEventId(frame.ToolId, frame.Parameter);
        var correlation = _cascade.CorrelationFor(frame.ToolId, frame.Parameter) ?? "nominal-" + frame.ToolId;
        var window = _plant.WindowAt(frame.ToolId, frame.Parameter, frame.SimulatedAt);
        return new LifecycleEvent(
            EventTypes.TelemetryReading,
            correlation,
            parent,
            parent,
            "simulator",
            null,
            frame.ToolId,
            frame.ChamberId,
            frame.SimulatedAt,
            new
            {
                parameter = frame.Parameter,
                value = frame.Value,
                unit = window?.Unit ?? frame.Unit,
                low = window?.Minimum,
                high = window?.Maximum,
            });
    }
}
