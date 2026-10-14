// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text;

namespace Mps.Simulator.Telemetry;

/// <summary>
/// SplitMix64 plus Box-Muller. The stream is a function of the seed and the series key, not of <see cref="System.Random"/>.
/// </summary>
internal sealed class SeededRng
{
    private ulong _state;

    private SeededRng(ulong state)
    {
        _state = state == 0 ? 0x9E3779B97F4A7C15UL : state;
    }

    public static SeededRng Create(int seed, string seriesKey)
    {
        return new SeededRng(Mix(seed, seriesKey));
    }

    public double NextNormal()
    {
        var u1 = Math.Max(NextUnit(), 1e-12d);
        var u2 = NextUnit();
        return Math.Sqrt(-2d * Math.Log(u1)) * Math.Cos(2d * Math.PI * u2);
    }

    private double NextUnit()
    {
        return (Next() >> 11) * (1d / (1UL << 53));
    }

    private ulong Next()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static ulong Mix(int seed, string seriesKey)
    {
        var hash = 14695981039346656037UL;
        MixByte(ref hash, (byte)seed);
        MixByte(ref hash, (byte)(seed >> 8));
        MixByte(ref hash, (byte)(seed >> 16));
        MixByte(ref hash, (byte)(seed >> 24));
        foreach (var value in Encoding.UTF8.GetBytes(seriesKey))
        {
            MixByte(ref hash, value);
        }

        return hash == 0 ? 1UL : hash;
    }

    private static void MixByte(ref ulong hash, byte value)
    {
        hash ^= value;
        hash *= 1099511628211UL;
    }
}
