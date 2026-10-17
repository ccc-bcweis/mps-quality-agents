// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Simulator.Telemetry;

/// <summary>
/// Fab clock for the demo. Scale 60 means one simulated minute elapses each real second.
/// <see cref="Reset"/> returns the clock to <see cref="BaselineOrigin"/>.
/// </summary>
public sealed class DemoClock
{
    public const double DefaultScale = Demo.DemoDefaults.ClockScale;

    /// <summary>Start of the authored scenario day, moved to today when real time is on.</summary>
    public static DateTimeOffset BaselineOrigin => RecordDates.Shift(RecordDates.ReferenceOrigin);

    private readonly TimeProvider _time;
    private readonly double _scale;
    private readonly object _gate = new();
    private DateTimeOffset _realOrigin;

    public DemoClock(TimeProvider? timeProvider = null, double scale = DefaultScale)
    {
        if (scale <= 0 || !double.IsFinite(scale))
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be a positive finite number.");
        }

        _time = timeProvider ?? TimeProvider.System;
        _scale = scale;
        _realOrigin = _time.GetUtcNow();
    }

    public double Scale => _scale;

    public DateTimeOffset SimulatedNow
    {
        get
        {
            lock (_gate)
            {
                var elapsed = _time.GetUtcNow() - _realOrigin;
                if (elapsed < TimeSpan.Zero)
                {
                    elapsed = TimeSpan.Zero;
                }

                return BaselineOrigin.AddTicks((long)(elapsed.Ticks * _scale));
            }
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _realOrigin = _time.GetUtcNow();
        }
    }
}
