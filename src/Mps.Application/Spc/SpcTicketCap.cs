// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

namespace Mps.Application.Spc;

/// <summary>
/// How many tickets one tool has opened in the simulated hour of the reading.
/// Past the cap the alert still stands; the ticket does not.
/// </summary>
public sealed class SpcTicketCap
{
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _opened = new(StringComparer.Ordinal);

    /// <summary>True when this ticket is still inside the cap and has been counted.</summary>
    public bool TryAdmit(string toolId, DateTimeOffset at, int cap)
    {
        if (cap <= 0 || string.IsNullOrWhiteSpace(toolId))
        {
            return true;
        }

        var hour = at.ToUniversalTime().Ticks / TimeSpan.TicksPerHour;
        var key = toolId.Trim() + "\u001f" + hour.ToString(CultureInfo.InvariantCulture);
        lock (_gate)
        {
            Prune(hour);
            _opened.TryGetValue(key, out var count);
            if (count >= cap)
            {
                return false;
            }

            _opened[key] = count + 1;
            return true;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _opened.Clear();
        }
    }

    private void Prune(long hour)
    {
        if (_opened.Count < 8)
        {
            return;
        }

        List<string>? stale = null;
        foreach (var pair in _opened)
        {
            var split = pair.Key.LastIndexOf('\u001f');
            if (split < 0 || !long.TryParse(pair.Key[(split + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var bucket) || hour - bucket > 2)
            {
                stale ??= [];
                stale.Add(pair.Key);
            }
        }

        if (stale is null)
        {
            return;
        }

        foreach (var key in stale)
        {
            _opened.Remove(key);
        }
    }
}
