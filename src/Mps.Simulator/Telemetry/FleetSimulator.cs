// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Threading.Channels;

using Mps.Simulator.Fleet;

namespace Mps.Simulator.Telemetry;

/// <summary>
/// In-process stream of the eight-tool fleet. One call to <see cref="Advance"/> emits one simulated second per series.
/// The API lane forwards <see cref="Frames"/> to SignalR. This type does not host a hub.
/// </summary>
public interface ITelemetrySource
{
    ChannelReader<TelemetryFrame> Frames { get; }

    FleetCatalog Fleet { get; }

    DateTimeOffset SimulatedNow { get; }

    void Inject(FaultSignal fault);

    IReadOnlyList<TelemetryFrame> Advance(TimeSpan simulatedElapsed);
}

public sealed class FleetSimulator : ITelemetrySource
{
    private readonly object _gate = new();
    private readonly Dictionary<string, SeriesCursor> _series = new(StringComparer.Ordinal);
    private readonly List<FaultSignal> _faults = [];
    private readonly Channel<TelemetryFrame> _channel;
    private DateTimeOffset _now;

    public FleetSimulator(FleetCatalog fleet, int seed, DateTimeOffset origin, int channelCapacity = 65536)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channelCapacity);

        Fleet = fleet;
        Seed = seed;
        Origin = origin;
        _now = origin;
        _channel = Channel.CreateBounded<TelemetryFrame>(new BoundedChannelOptions(channelCapacity)
        {
            SingleReader = false,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest,
        });
    }

    public FleetCatalog Fleet { get; }

    public int Seed { get; }

    public DateTimeOffset Origin { get; }

    public ChannelReader<TelemetryFrame> Frames => _channel.Reader;

    public DateTimeOffset SimulatedNow
    {
        get
        {
            lock (_gate)
            {
                return _now;
            }
        }
    }

    public IReadOnlyList<FaultSignal> ActiveFaults
    {
        get
        {
            lock (_gate)
            {
                return _faults.ToArray();
            }
        }
    }

    public void Inject(FaultSignal fault)
    {
        ArgumentNullException.ThrowIfNull(fault);
        lock (_gate)
        {
            _faults.Add(fault);
        }
    }

    public IReadOnlyList<TelemetryFrame> Advance(TimeSpan simulatedElapsed)
    {
        var seconds = (int)simulatedElapsed.TotalSeconds;
        if (seconds <= 0 || TimeSpan.FromSeconds(seconds) != simulatedElapsed)
        {
            throw new ArgumentOutOfRangeException(nameof(simulatedElapsed), "Advance in whole simulated seconds.");
        }

        lock (_gate)
        {
            var frames = new List<TelemetryFrame>();
            for (var second = 0; second < seconds; second++)
            {
                var at = _now;
                var elapsed = checked((int)(at - Origin).TotalSeconds);
                foreach (var tool in Fleet.Tools)
                {
                    var schedule = RecipeSchedule.Locate(tool, elapsed);
                    var runId = RecipeSchedule.RunId(tool.Id, elapsed);
                    var lotId = RecipeSchedule.LotId(tool.Id, elapsed);
                    foreach (var chamber in tool.Chambers)
                    {
                        foreach (var parameter in RecipeSchedule.Parameters(schedule.Recipe))
                        {
                            var window = RecipeSchedule.Resolve(schedule, parameter);
                            var key = TelemetryMath.SeriesKey(tool.Id, chamber.Id, parameter);
                            if (!_series.TryGetValue(key, out var cursor))
                            {
                                cursor = new SeriesCursor(Seed, key);
                                _series.Add(key, cursor);
                            }

                            var value = cursor.Next(window, at, _faults, tool.Id, chamber.Id, parameter);
                            var frame = new TelemetryFrame(
                                tool.Id,
                                chamber.Id,
                                parameter,
                                window.Unit,
                                at,
                                value,
                                schedule.Recipe.Id,
                                schedule.Step.Id,
                                runId,
                                lotId);
                            frames.Add(frame);
                            _channel.Writer.TryWrite(frame);
                        }
                    }
                }

                _now = _now.AddSeconds(1);
            }

            return frames;
        }
    }
}
