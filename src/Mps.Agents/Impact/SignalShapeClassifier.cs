// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Telemetry;

namespace Mps.Agents.Impact;

/// <summary>
/// Classifies a parameter trace. Too few points, a flat trace, or a mixed trace stay Unknown.
/// </summary>
public static class SignalShapeClassifier
{
    private const double StepShare = 0.7d;

    private const double FlatShare = 0.2d;

    private const double RampShare = 0.5d;

    private const double SameDirectionShare = 0.75d;

    public static SignalShape Classify(IReadOnlyList<TelemetryPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var ordered = points.OrderBy(point => point.At).Select(point => point.Value).ToArray();
        return Classify(ordered);
    }

    public static SignalShape Classify(IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count < 4 || values.Any(double.IsNaN) || values.Any(double.IsInfinity))
        {
            return SignalShape.Unknown;
        }

        var range = Range(values);
        if (range <= 0d)
        {
            return SignalShape.Unknown;
        }

        if (IsStep(values, range))
        {
            return SignalShape.Step;
        }

        if (IsIntermittent(values, range))
        {
            return SignalShape.Intermittent;
        }

        if (SignChanges(values) >= 3 && RepeatsExtremes(values))
        {
            return SignalShape.Oscillation;
        }

        if (IsRamp(values, range))
        {
            return SignalShape.Ramp;
        }

        return SignalShape.Unknown;
    }

    private static bool IsStep(IReadOnlyList<double> values, double range)
    {
        var maxAbs = 0d;
        var index = 1;
        for (var i = 1; i < values.Count; i++)
        {
            var abs = Math.Abs(values[i] - values[i - 1]);
            if (abs > maxAbs)
            {
                maxAbs = abs;
                index = i;
            }
        }

        if (maxAbs < (StepShare * range))
        {
            return false;
        }

        var left = values.Take(index).ToArray();
        var right = values.Skip(index).ToArray();
        return left.Length > 0
            && right.Length > 0
            && Range(left) <= (FlatShare * maxAbs)
            && Range(right) <= (FlatShare * maxAbs);
    }

    private static bool IsIntermittent(IReadOnlyList<double> values, double range)
    {
        var median = Median(values);
        var threshold = 0.5d * range;
        var outliers = values.Count(value => Math.Abs(value - median) > threshold);
        var edge = 0.25d * range;
        return outliers >= 1
            && outliers <= (values.Count / 3)
            && Math.Abs(values[0] - median) <= edge
            && Math.Abs(values[values.Count - 1] - median) <= edge;
    }

    private static bool IsRamp(IReadOnlyList<double> values, double range)
    {
        var net = values[values.Count - 1] - values[0];
        if (Math.Abs(net) < (RampShare * range))
        {
            return false;
        }

        var direction = Math.Sign(net);
        var same = 0;
        var nonzero = 0;
        var maxAbs = 0d;
        for (var i = 1; i < values.Count; i++)
        {
            var delta = values[i] - values[i - 1];
            var abs = Math.Abs(delta);
            if (abs > maxAbs)
            {
                maxAbs = abs;
            }

            if (Math.Abs(delta) <= 0d)
            {
                continue;
            }

            nonzero++;
            if (Math.Sign(delta) == direction)
            {
                same++;
            }
        }

        return nonzero > 0
            && same >= (SameDirectionShare * nonzero)
            && maxAbs < (StepShare * range);
    }

    private static bool RepeatsExtremes(IReadOnlyList<double> values)
    {
        var min = values.Min();
        var max = values.Max();
        var lows = 0;
        var highs = 0;
        foreach (var value in values)
        {
            if (Math.Abs(value - min) <= 1e-9)
            {
                lows++;
            }

            if (Math.Abs(value - max) <= 1e-9)
            {
                highs++;
            }
        }

        return lows >= 2 && highs >= 2;
    }

    private static int SignChanges(IReadOnlyList<double> values)
    {
        var changes = 0;
        var previous = 0;
        for (var i = 1; i < values.Count; i++)
        {
            var delta = values[i] - values[i - 1];
            var sign = Math.Sign(delta);
            if (sign == 0)
            {
                continue;
            }

            if (previous != 0 && sign != previous)
            {
                changes++;
            }

            previous = sign;
        }

        return changes;
    }

    private static double Median(IReadOnlyList<double> values)
    {
        var sorted = values.OrderBy(value => value).ToArray();
        var mid = sorted.Length / 2;
        if ((sorted.Length % 2) == 1)
        {
            return sorted[mid];
        }

        return (sorted[mid - 1] + sorted[mid]) / 2d;
    }

    private static double Range(IReadOnlyList<double> values)
    {
        return values.Max() - values.Min();
    }
}
