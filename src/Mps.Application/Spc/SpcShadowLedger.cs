// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Text;

namespace Mps.Application.Spc;

/// <summary>
/// Counts what shadow mode would raise, clear, and ticket. It never writes an alert.
/// </summary>
public sealed class SpcShadowLedger
{
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _byToolRuleTier = new(StringComparer.Ordinal);

    public int AlertRaises { get; private set; }

    public int WatchRaises { get; private set; }

    public int Clears { get; private set; }

    public int Tickets { get; private set; }

    public int TicketsSuppressed { get; private set; }

    public void Note(SpcObservation observation, bool wouldTicket, bool ticketSuppressed)
    {
        ArgumentNullException.ThrowIfNull(observation);
        lock (_gate)
        {
            if (observation.Action is SpcAction.Raise or SpcAction.Escalate)
            {
                if (observation.Tier == SpcTier.Alert)
                {
                    AlertRaises++;
                }
                else if (observation.Tier == SpcTier.Watch)
                {
                    WatchRaises++;
                }
            }
            else if (observation.Action == SpcAction.Clear)
            {
                Clears++;
            }

            if (wouldTicket)
            {
                Tickets++;
            }

            if (ticketSuppressed)
            {
                TicketsSuppressed++;
            }

            var rules = observation.LiveRules.Count == 0 ? (IReadOnlyList<string>)[observation.RuleText] : observation.LiveRules;
            foreach (var rule in rules)
            {
                var key = observation.Reading.ToolId + "\u001f" + rule + "\u001f" + observation.Tier + "\u001f" + observation.Action;
                _byToolRuleTier.TryGetValue(key, out var count);
                _byToolRuleTier[key] = count + 1;
            }
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            AlertRaises = 0;
            WatchRaises = 0;
            Clears = 0;
            Tickets = 0;
            TicketsSuppressed = 0;
            _byToolRuleTier.Clear();
        }
    }

    public string Format()
    {
        lock (_gate)
        {
            var builder = new StringBuilder();
            builder.Append("alertRaises=").Append(AlertRaises.ToString(CultureInfo.InvariantCulture));
            builder.Append(" watchRaises=").Append(WatchRaises.ToString(CultureInfo.InvariantCulture));
            builder.Append(" clears=").Append(Clears.ToString(CultureInfo.InvariantCulture));
            builder.Append(" tickets=").Append(Tickets.ToString(CultureInfo.InvariantCulture));
            builder.Append(" ticketsSuppressed=").Append(TicketsSuppressed.ToString(CultureInfo.InvariantCulture));
            foreach (var pair in _byToolRuleTier.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var parts = pair.Key.Split('\u001f');
                builder.AppendLine();
                builder.Append(parts.Length > 0 ? parts[0] : string.Empty).Append(' ');
                builder.Append(parts.Length > 1 ? parts[1] : string.Empty).Append(' ');
                builder.Append(parts.Length > 2 ? parts[2] : string.Empty).Append(' ');
                builder.Append(parts.Length > 3 ? parts[3] : string.Empty).Append(' ');
                builder.Append(pair.Value.ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
