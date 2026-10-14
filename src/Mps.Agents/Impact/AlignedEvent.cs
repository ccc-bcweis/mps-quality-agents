// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Agents.Impact;

/// <summary>
/// A discrete enterprise event that may line up with the onset.
/// </summary>
public sealed class AlignedEvent
{
    public AlignedEvent(string kind, string key, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new ArgumentException("Event kind is required.", nameof(kind));
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Event key is required.", nameof(key));
        }

        Kind = kind.Trim();
        Key = key.Trim();
        At = at;
    }

    public string Kind { get; }

    public string Key { get; }

    public DateTimeOffset At { get; }
}
