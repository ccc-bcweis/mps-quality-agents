// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Security.Cryptography;

namespace Mps.Bus;

/// <summary>
/// 26-character lexicographic event ids (48 bits of time, 80 bits of randomness).
/// Ids issued in the same millisecond stay ordered.
/// </summary>
public static class EventIds
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private static readonly object Gate = new();

    private static readonly byte[] Tail = new byte[10];

    private static long _lastMillisecond = -1;

    public static string Next(DateTimeOffset now)
    {
        lock (Gate)
        {
            var millisecond = now.ToUnixTimeMilliseconds();
            if (millisecond < 0)
            {
                millisecond = 0;
            }

            if (millisecond == _lastMillisecond)
            {
                Increment(Tail);
            }
            else
            {
                _lastMillisecond = millisecond;
                RandomNumberGenerator.Fill(Tail);
            }

            return Encode(millisecond, Tail);
        }
    }

    public static bool IsWellFormed(string? value)
    {
        if (value is null || value.Length != 26)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (Alphabet.IndexOf(character) < 0)
            {
                return false;
            }
        }

        return true;
    }

    private static void Increment(byte[] tail)
    {
        for (var index = tail.Length - 1; index >= 0; index--)
        {
            if (tail[index] != byte.MaxValue)
            {
                tail[index]++;
                return;
            }

            tail[index] = 0;
        }
    }

    private static string Encode(long millisecond, byte[] tail)
    {
        Span<char> chars = stackalloc char[26];
        var time = (ulong)millisecond;
        for (var index = 9; index >= 0; index--)
        {
            chars[index] = Alphabet[(int)(time & 31)];
            time >>= 5;
        }

        var buffer = 0;
        var bits = 0;
        var cursor = 10;
        foreach (var part in tail)
        {
            buffer = (buffer << 8) | part;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                chars[cursor++] = Alphabet[(buffer >> bits) & 31];
            }
        }

        return new string(chars);
    }
}
