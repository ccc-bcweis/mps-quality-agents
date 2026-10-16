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
    private SeededRng? _excursions;

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

    public DateTimeOffset Origin { get; private set; }

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

    /// <summary>
    /// Stamps each signal with the simulator clock while the clock cannot move. Onset is "now", not the baseline origin.
    /// </summary>
    public void InjectAtSimulatedNow(Func<DateTimeOffset, IEnumerable<FaultSignal>> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        lock (_gate)
        {
            foreach (var fault in build(_now))
            {
                ArgumentNullException.ThrowIfNull(fault);
                _faults.Add(fault);
            }
        }
    }

    /// <summary>
    /// Drops injected faults and series cursors and puts the simulator clock back on <see cref="Origin"/>.
    /// </summary>
    /// <summary>Removes matching faults. Series cursors and the clock are untouched. Returns how many went.</summary>
    public int RemoveFaults(Predicate<FaultSignal> match)
    {
        ArgumentNullException.ThrowIfNull(match);
        lock (_gate)
        {
            return _faults.RemoveAll(match);
        }
    }

    public void Reset() => Reset(Origin);

    /// <summary>Drops faults and series cursors and restarts the clock on <paramref name="origin"/>.</summary>
    public void Reset(DateTimeOffset origin)
    {
        lock (_gate)
        {
            _faults.Clear();
            _series.Clear();
            Origin = origin;
            _now = origin;
        }
    }

    public IReadOnlyList<TelemetryFrame> Advance(TimeSpan simulatedElapsed) => Advance(simulatedElapsed, 1);

    /// <summary>
    /// Advances one simulated second at a time so the seeded cursor matches a 1 Hz run.
    /// <paramref name="sampleStrideSeconds"/> keeps every Nth second of plant time since the origin in the returned list.
    /// </summary>
    public IReadOnlyList<TelemetryFrame> Advance(TimeSpan simulatedElapsed, int sampleStrideSeconds)
    {
        var seconds = (int)simulatedElapsed.TotalSeconds;
        if (seconds <= 0 || TimeSpan.FromSeconds(seconds) != simulatedElapsed)
        {
            throw new ArgumentOutOfRangeException(nameof(simulatedElapsed), "Advance in whole simulated seconds.");
        }

        if (sampleStrideSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleStrideSeconds), "Sample stride must be positive.");
        }

        lock (_gate)
        {
            var frames = new List<TelemetryFrame>();
            for (var second = 0; second < seconds; second++)
            {
                var at = _now;
                var elapsed = checked((int)(at - Origin).TotalSeconds);

                // Kept readings sit on a fixed grid of plant time (every Nth second since the origin), not on the end
                // of each call. Real time advances about two seconds per tick, so keeping "the last second of the call"
                // produced a reading every tick instead of one per stride.
                var keep = sampleStrideSeconds == 1 || (elapsed + 1) % sampleStrideSeconds == 0;
                foreach (var tool in Fleet.Tools)
                {
                    var schedule = RecipeSchedule.Locate(tool, elapsed);
                    var runId = RecipeSchedule.RunId(tool.Id, elapsed);
                    var lotId = RecipeSchedule.LotId(tool.Id, elapsed);
                    var values = new Dictionary<string, double>(StringComparer.Ordinal);
                    foreach (var chamber in tool.Chambers)
                    {
                        foreach (var parameter in RecipeSchedule.Parameters(schedule.Recipe))
                        {
                            var window = RecipeSchedule.Resolve(schedule, parameter);
                            var sensor = tool.RequireParameterSensor(chamber.Id, parameter);
                            if (!string.Equals(sensor.Unit, window.Unit, StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException($"Sensor {sensor.Id} unit {sensor.Unit} does not match {window.Unit}.");
                            }

                            var key = TelemetryMath.SeriesKey(tool.Id, chamber.Id, parameter);
                            var value = Cursor(key).Next(window, at, _faults, tool.Id, chamber.Id, parameter);
                            values[sensor.Id] = value;
                            if (keep)
                            {
                                AddFrame(frames, tool.Id, chamber.Id, parameter, sensor.Unit, sensor.Id, at, value + Excursion(window), schedule, runId, lotId);
                            }
                        }

                        foreach (var reflected in tool.Sensors.Where(sensor => sensor.TracksSensorId is not null && string.Equals(sensor.ChamberId, chamber.Id, StringComparison.Ordinal)).OrderBy(sensor => sensor.Id, StringComparer.Ordinal))
                        {
                            if (!values.TryGetValue(reflected.TracksSensorId!, out var forward))
                            {
                                continue;
                            }

                            var tracked = tool.Sensors.First(sensor => string.Equals(sensor.Id, reflected.TracksSensorId, StringComparison.Ordinal));
                            var key = TelemetryMath.SeriesKey(tool.Id, chamber.Id, reflected.Id);
                            var noise = Cursor(key).NextDeviation(reflected.Limits.Sigma);
                            var value = Math.Max(reflected.Limits.Minimum, (forward * (reflected.Limits.Nominal / tracked.Limits.Nominal)) + noise);
                            if (keep)
                            {
                                AddFrame(frames, tool.Id, chamber.Id, reflected.Id, reflected.Unit, reflected.Id, at, value, schedule, runId, lotId);
                            }
                        }
                    }

                    foreach (var sensor in tool.Sensors.Where(sensor => sensor.IsToolScope).OrderBy(sensor => sensor.Id, StringComparer.Ordinal))
                    {
                        var chamberId = sensor.FrameChamberId(tool.Id);
                        var key = TelemetryMath.SeriesKey(tool.Id, chamberId, sensor.Id);
                        double value;
                        if (sensor.Type == SensorCatalog.Output)
                        {
                            value = (elapsed % 3600) / (double)RecipeSchedule.WaferSeconds;
                        }
                        else
                        {
                            var window = new OperatingWindow(sensor.Id, sensor.Unit, sensor.Limits.Minimum, sensor.Limits.Maximum, sensor.Limits.Nominal);
                            value = Cursor(key).Next(window, at, _faults, tool.Id, chamberId, sensor.Id);
                        }

                        if (keep)
                        {
                            AddFrame(frames, tool.Id, chamberId, sensor.Id, sensor.Unit, sensor.Id, at, value, schedule, runId, lotId);
                        }
                    }
                }

                _now = _now.AddSeconds(1);
            }

            return frames;
        }
    }

    /// <summary>Baseline noise stays well inside spec. Rarely one kept point jumps past the watch line and the next is back to normal.</summary>
    private double Excursion(OperatingWindow window)
    {
        _excursions ??= SeededRng.Create(Seed, "lone-excursions");
        if (_excursions.NextUnit() >= TelemetryMath.ExcursionChance)
        {
            return 0d;
        }

        var size = TelemetryMath.ExcursionSigma * window.Sigma;
        var up = _excursions.NextUnit() < 0.5d;
        var candidate = window.Nominal + (up ? size : -size);
        if (candidate > window.Maximum || candidate < window.Minimum)
        {
            return 0d;
        }

        return up ? size : -size;
    }

    private SeriesCursor Cursor(string key)
    {
        if (!_series.TryGetValue(key, out var cursor))
        {
            cursor = new SeriesCursor(Seed, key);
            _series.Add(key, cursor);
        }

        return cursor;
    }

    private void AddFrame(
        List<TelemetryFrame> frames,
        string toolId,
        string chamberId,
        string parameter,
        string unit,
        string sensorId,
        DateTimeOffset at,
        double value,
        RecipeCursor schedule,
        string runId,
        string lotId)
    {
        var frame = new TelemetryFrame(
            toolId,
            chamberId,
            parameter,
            unit,
            sensorId,
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
