// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Simulator.Telemetry;

/// <summary>
/// Maps real time onto fab time. Scale 60 means one simulated minute elapses each real second.
/// </summary>
public sealed class AcceleratedClock
{
    private readonly DateTimeOffset _realOrigin;
    private readonly DateTimeOffset _simulatedOrigin;
    private readonly double _scale;
    private readonly TimeProvider _time;

    public AcceleratedClock(DateTimeOffset simulatedOrigin, double scale, TimeProvider? timeProvider = null)
    {
        if (scale <= 0 || !double.IsFinite(scale))
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be a positive finite number.");
        }

        _time = timeProvider ?? TimeProvider.System;
        _realOrigin = _time.GetUtcNow();
        _simulatedOrigin = simulatedOrigin;
        _scale = scale;
    }

    public double Scale => _scale;

    public DateTimeOffset SimulatedNow =>
        _simulatedOrigin.AddTicks((long)((_time.GetUtcNow() - _realOrigin).Ticks * _scale));
}
