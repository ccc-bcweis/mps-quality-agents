// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Simulator.Telemetry;

/// <summary>One decimated sample kept for charts and SPC.</summary>
public readonly record struct LivePoint(DateTimeOffset At, double Value);

/// <summary>The newest sample of one series, including the sensor id the schematic uses.</summary>
public sealed record LiveReading(
    string ToolId,
    string ChamberId,
    string Parameter,
    string SensorId,
    string Unit,
    DateTimeOffset At,
    double Value);

/// <summary>
/// Ring buffer of decimated telemetry. The simulator still steps every second; only the kept samples are stored.
/// </summary>
public sealed class LiveSeriesLog
{
    private readonly object _gate = new();
    private readonly int _maxPoints;
    private readonly Dictionary<string, Queue<LivePoint>> _series = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LiveReading> _latest = new(StringComparer.Ordinal);

    public LiveSeriesLog(int maxPoints = 960)
    {
        if (maxPoints <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPoints), "The series ring must keep at least one point.");
        }

        _maxPoints = maxPoints;
    }

    public int MaxPoints => _maxPoints;

    public void Append(IEnumerable<TelemetryFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        lock (_gate)
        {
            foreach (var frame in frames)
            {
                var key = Key(frame.ToolId, frame.ChamberId, frame.Parameter);
                if (!_series.TryGetValue(key, out var buffer))
                {
                    buffer = new Queue<LivePoint>();
                    _series.Add(key, buffer);
                }

                buffer.Enqueue(new LivePoint(frame.SimulatedAt, frame.Value));
                while (buffer.Count > _maxPoints)
                {
                    buffer.Dequeue();
                }

                _latest[key] = new LiveReading(
                    frame.ToolId,
                    frame.ChamberId,
                    frame.Parameter,
                    frame.SensorId,
                    frame.Unit,
                    frame.SimulatedAt,
                    frame.Value);
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _series.Clear();
            _latest.Clear();
        }
    }

    public IReadOnlyList<LivePoint> Window(string toolId, string chamberId, string parameter, int maxPoints = 600)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPoints);

        lock (_gate)
        {
            if (!_series.TryGetValue(Key(toolId, chamberId, parameter), out var buffer) || buffer.Count == 0)
            {
                return [];
            }

            var skip = Math.Max(0, buffer.Count - maxPoints);
            return buffer.Skip(skip).ToArray();
        }
    }

    /// <summary>
    /// Points for a parameter. A null chamber uses the chamber whose latest sample is newest.
    /// </summary>
    public IReadOnlyList<LivePoint> WindowForParameter(string toolId, string parameter, string? chamberId, int maxPoints = 600)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPoints);

        lock (_gate)
        {
            var match = chamberId;
            if (string.IsNullOrWhiteSpace(match))
            {
                LiveReading? newest = null;
                foreach (var reading in _latest.Values)
                {
                    if (!string.Equals(reading.ToolId, toolId, StringComparison.Ordinal)
                        || !string.Equals(reading.Parameter, parameter, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (newest is null || reading.At > newest.At)
                    {
                        newest = reading;
                    }
                }

                match = newest?.ChamberId;
            }

            if (match is null || !_series.TryGetValue(Key(toolId, match, parameter), out var buffer) || buffer.Count == 0)
            {
                return [];
            }

            var skip = Math.Max(0, buffer.Count - maxPoints);
            return buffer.Skip(skip).ToArray();
        }
    }

    public string? NewestChamber(string toolId, string parameter)
    {
        lock (_gate)
        {
            LiveReading? newest = null;
            foreach (var reading in _latest.Values)
            {
                if (!string.Equals(reading.ToolId, toolId, StringComparison.Ordinal)
                    || !string.Equals(reading.Parameter, parameter, StringComparison.Ordinal))
                {
                    continue;
                }

                if (newest is null || reading.At > newest.At)
                {
                    newest = reading;
                }
            }

            return newest?.ChamberId;
        }
    }

    public IReadOnlyList<LiveReading> Snapshot()
    {
        lock (_gate)
        {
            return _latest.Values.ToArray();
        }
    }

    public IReadOnlyList<LiveReading> SnapshotForTool(string toolId)
    {
        lock (_gate)
        {
            return _latest.Values.Where(reading => string.Equals(reading.ToolId, toolId, StringComparison.Ordinal)).ToArray();
        }
    }

    private static string Key(string toolId, string chamberId, string parameter) =>
        string.Concat(toolId, "|", chamberId, "|", parameter);
}
