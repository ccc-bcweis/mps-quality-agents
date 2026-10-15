// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Bus;

public static class EventPatterns
{
    public static bool Matches(string pattern, string type)
    {
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(type))
        {
            return false;
        }

        if (string.Equals(pattern, "*", StringComparison.Ordinal))
        {
            return true;
        }

        if (pattern.EndsWith(".*", StringComparison.Ordinal))
        {
            return type.StartsWith(pattern[..^1], StringComparison.Ordinal);
        }

        return string.Equals(pattern, type, StringComparison.Ordinal);
    }
}
