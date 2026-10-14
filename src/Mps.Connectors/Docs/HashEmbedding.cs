// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text;

namespace Mps.Connectors.Docs;

/// <summary>
/// Key-free hashed bag-of-tokens embedding. The same text always yields the same unit vector.
/// </summary>
public static class HashEmbedding
{
    public const string Model = "mps-hash-v1";

    public const int Dimensions = 256;

    public static float[] Embed(string text)
    {
        var vector = new float[Dimensions];
        foreach (var token in TextTokens.Tokenize(text ?? string.Empty))
        {
            var hash = Hash(token);
            var bucket = (int)(hash % Dimensions);
            var sign = ((hash >> 8) & 1) == 0 ? 1f : -1f;
            vector[bucket] += sign;
        }

        var norm = 0d;
        foreach (var value in vector)
        {
            norm += value * value;
        }

        if (norm == 0d)
        {
            vector[0] = 1f;
            return vector;
        }

        var scale = (float)(1d / Math.Sqrt(norm));
        for (var index = 0; index < vector.Length; index++)
        {
            vector[index] *= scale;
        }

        return vector;
    }

    public static float Dot(float[] left, float[] right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Length != right.Length)
        {
            throw new ArgumentException("Embedding widths differ.", nameof(right));
        }

        var sum = 0f;
        for (var index = 0; index < left.Length; index++)
        {
            sum += left[index] * right[index];
        }

        return sum;
    }

    private static uint Hash(string token)
    {
        uint hash = 2166136261;
        foreach (var value in Encoding.UTF8.GetBytes(token))
        {
            hash ^= value;
            hash *= 16777619;
        }

        return hash;
    }
}
