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

        // A noisy oscillation rarely repeats exact extremes, but it swings back and forth with a period:
        // the autocorrelation turns clearly negative at half a period and clearly positive again at a full one.
        if (IsPeriodic(values))
        {
            return SignalShape.Oscillation;
        }

        if (IsIntermittent(values, range))
        {
            return SignalShape.Intermittent;
        }

        if (SignChanges(values) >= 3 && RepeatsExtremes(values))
        {
            return SignalShape.Oscillation;
        }

        if (IsRamp(values, range) || IsCorrelatedRamp(values, range))
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

    /// <summary>
    /// A slow ramp buried in sample noise fails the same-direction test. Correlation still sees the trend.
    /// A single step is classified earlier and is not reclassified here.
    /// </summary>
    private static bool IsCorrelatedRamp(IReadOnlyList<double> values, double range)
    {
        var net = values[values.Count - 1] - values[0];
        if (Math.Abs(net) < (RampShare * range))
        {
            return false;
        }

        var maxAbs = 0d;
        for (var i = 1; i < values.Count; i++)
        {
            var abs = Math.Abs(values[i] - values[i - 1]);
            if (abs > maxAbs)
            {
                maxAbs = abs;
            }
        }

        if (maxAbs >= (StepShare * range))
        {
            return false;
        }

        return Math.Abs(CorrelationWithIndex(values)) >= 0.6d;
    }

    private static double CorrelationWithIndex(IReadOnlyList<double> values)
    {
        var count = values.Count;
        double sumX = 0d;
        double sumY = 0d;
        double sumXX = 0d;
        double sumYY = 0d;
        double sumXY = 0d;
        for (var i = 0; i < count; i++)
        {
            var y = values[i];
            sumX += i;
            sumY += y;
            sumXX += (double)i * i;
            sumYY += y * y;
            sumXY += i * y;
        }

        var numerator = (count * sumXY) - (sumX * sumY);
        var denominator = Math.Sqrt(((count * sumXX) - (sumX * sumX)) * ((count * sumYY) - (sumY * sumY)));
        if (denominator <= 0d || double.IsNaN(denominator))
        {
            return 0d;
        }

        return numerator / denominator;
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

    private static bool IsPeriodic(IReadOnlyList<double> values)
    {
        if (values.Count < 16)
        {
            return false;
        }

        var mean = values.Average();
        var variance = values.Sum(value => (value - mean) * (value - mean));
        if (variance <= 0d)
        {
            return false;
        }

        double Acf(int lag)
        {
            var sum = 0d;
            for (var i = lag; i < values.Count; i++)
            {
                sum += (values[i] - mean) * (values[i - lag] - mean);
            }

            return sum / variance;
        }

        // Find the deepest dip, then require a clear peak again near twice that lag (one full period).
        var dipLag = 0;
        var dip = 0d;
        for (var lag = 1; lag <= values.Count / 3; lag++)
        {
            var acf = Acf(lag);
            if (acf < dip)
            {
                dip = acf;
                dipLag = lag;
            }
        }

        if (dipLag < 2 || dip > -0.45d)
        {
            return false;
        }

        var low = (int)Math.Floor(dipLag * 1.6d);
        var high = Math.Min(values.Count / 2, (int)Math.Ceiling(dipLag * 2.4d));
        for (var lag = low; lag <= high; lag++)
        {
            if (Acf(lag) > 0.3d)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when the recent readings swing back and forth at least twice, each move many times the normal
    /// reading-to-reading noise, and reach well above and well below the usual level. Noise, a step, a ramp, or
    /// spikes on one side do not.
    /// </summary>
    public static bool IsSwinging(IReadOnlyList<double> recent, IReadOnlyList<double> whole)
    {
        ArgumentNullException.ThrowIfNull(recent);
        ArgumentNullException.ThrowIfNull(whole);
        if (recent.Count < 8 || whole.Count < 16)
        {
            return false;
        }

        var steps = new List<double>(whole.Count);
        for (var i = 1; i < whole.Count; i++)
        {
            steps.Add(Math.Abs(whole[i] - whole[i - 1]));
        }

        var noise = Median(steps) / 0.954d;
        if (noise <= 0d)
        {
            return false;
        }

        // Count how often the readings cross from well above the usual level to well below it, or back.
        var center = Median(whole);
        var band = 4d * noise;
        var last = 0;
        var crossings = 0;
        foreach (var value in recent)
        {
            var zone = value > center + band ? 1 : value < center - band ? -1 : 0;
            if (zone == 0)
            {
                continue;
            }

            if (last != 0 && zone != last)
            {
                crossings++;
            }

            last = zone;
        }

        return crossings >= 2 && Reversals(recent, 6d * noise) >= 2;
    }

    private static int Reversals(IReadOnlyList<double> values, double threshold)
    {
        var high = values[0];
        var low = values[0];
        var trend = 0;
        var reversals = 0;
        for (var i = 1; i < values.Count; i++)
        {
            var x = values[i];
            switch (trend)
            {
                case 0:
                    high = Math.Max(high, x);
                    low = Math.Min(low, x);
                    if (high - x >= threshold)
                    {
                        trend = -1;
                        low = x;
                    }
                    else if (x - low >= threshold)
                    {
                        trend = 1;
                        high = x;
                    }

                    break;
                case 1:
                    if (x > high)
                    {
                        high = x;
                    }
                    else if (high - x >= threshold)
                    {
                        trend = -1;
                        low = x;
                        reversals++;
                    }

                    break;
                default:
                    if (x < low)
                    {
                        low = x;
                    }
                    else if (x - low >= threshold)
                    {
                        trend = 1;
                        high = x;
                        reversals++;
                    }

                    break;
            }
        }

        return reversals;
    }
}
