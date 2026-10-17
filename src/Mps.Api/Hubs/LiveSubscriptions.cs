// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Hubs;

/// <summary>
/// Who is watching which tool or plant on the fleet hub. The live stream only builds a delta for a tool or plant
/// that at least one open page has subscribed to, so an idle console costs no serialization.
/// </summary>
public sealed class LiveSubscriptions
{
    /// <summary>The plant id a page passes to watch every plant at once.</summary>
    public const string AllSites = "all";

    private readonly object _gate = new();
    private readonly Dictionary<string, HashSet<string>> _byConnection = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);

    public static string ToolKey(string toolId) => "tool:" + toolId.Trim();

    public static string SiteKey(string siteId) => "site:" + siteId.Trim().ToLowerInvariant();

    public bool Add(string connectionId, string key)
    {
        lock (_gate)
        {
            if (!_byConnection.TryGetValue(connectionId, out var keys))
            {
                keys = new HashSet<string>(StringComparer.Ordinal);
                _byConnection.Add(connectionId, keys);
            }

            if (!keys.Add(key))
            {
                return false;
            }

            _counts[key] = _counts.GetValueOrDefault(key) + 1;
            return true;
        }
    }

    public bool Remove(string connectionId, string key)
    {
        lock (_gate)
        {
            if (!_byConnection.TryGetValue(connectionId, out var keys) || !keys.Remove(key))
            {
                return false;
            }

            Decrement(key);
            return true;
        }
    }

    public void Drop(string connectionId)
    {
        lock (_gate)
        {
            if (!_byConnection.Remove(connectionId, out var keys))
            {
                return;
            }

            foreach (var key in keys)
            {
                Decrement(key);
            }
        }
    }

    public bool Watched(string key)
    {
        lock (_gate)
        {
            return _counts.GetValueOrDefault(key) > 0;
        }
    }

    /// <summary>True when any page watches a tool or a plant.</summary>
    public bool Any
    {
        get
        {
            lock (_gate)
            {
                return _counts.Count > 0;
            }
        }
    }

    private void Decrement(string key)
    {
        var next = _counts.GetValueOrDefault(key) - 1;
        if (next <= 0)
        {
            _counts.Remove(key);
        }
        else
        {
            _counts[key] = next;
        }
    }
}
