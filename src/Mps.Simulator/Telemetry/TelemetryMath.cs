// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Simulator.Fleet;

namespace Mps.Simulator.Telemetry;

internal static class TelemetryMath
{
    public const double Phi = 0.82d;

    public const double NoiseScale = 0.35d;

    public static string SeriesKey(string toolId, string chamberId, string parameter)
    {
        return string.Concat(toolId, "|", chamberId, "|", parameter);
    }

    public static double Offset(OperatingWindow window, IReadOnlyList<FaultSignal> faults, DateTimeOffset at, string toolId, string chamberId, string parameter)
    {
        var sum = 0d;
        foreach (var fault in faults)
        {
            if (!fault.AppliesTo(toolId, chamberId, parameter, at))
            {
                continue;
            }

            var elapsed = (at - fault.Onset).TotalSeconds;
            var span = fault.MagnitudeKind switch
            {
                MagnitudeKind.Sigma => fault.Magnitude * window.Sigma,
                MagnitudeKind.Percent => fault.Magnitude / 100d * (window.Nominal == 0d ? window.Maximum : window.Nominal),
                _ => throw new InvalidOperationException("Unknown magnitude kind."),
            };
            sum += fault.Shape switch
            {
                FaultShape.Step => span,
                FaultShape.Ramp => span * Math.Min(1d, elapsed / fault.RampSeconds),
                FaultShape.Oscillation => span * Math.Sin(2d * Math.PI * elapsed / fault.PeriodSeconds),
                FaultShape.Intermittent => Fractional(elapsed, fault.PeriodSeconds) < fault.DutyCycle ? span : 0d,
                _ => throw new InvalidOperationException("Unknown fault shape."),
            };
        }

        return sum;
    }

    private static double Fractional(double elapsed, double period)
    {
        var ratio = elapsed / period;
        return ratio - Math.Floor(ratio);
    }
}

internal sealed class SeriesCursor
{
    private readonly SeededRng _rng;
    private double _deviation;

    public SeriesCursor(int seed, string seriesKey)
    {
        _rng = SeededRng.Create(seed, seriesKey);
    }

    public double Next(OperatingWindow window, DateTimeOffset at, IReadOnlyList<FaultSignal> faults, string toolId, string chamberId, string parameter)
    {
        _deviation = (TelemetryMath.Phi * _deviation) + (window.Sigma * TelemetryMath.NoiseScale * _rng.NextNormal());
        return window.Nominal + _deviation + TelemetryMath.Offset(window, faults, at, toolId, chamberId, parameter);
    }
}
