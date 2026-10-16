// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Identity;
using Mps.Domain.Incidents;

namespace Mps.Application.Spc;

/// <summary>
/// Suppresses a repeat alert for the same tool, chamber, and parameter inside the debounce window.
/// The window is measured on sample time, not on the wall clock.
/// </summary>
public static class SpcDebounce
{
    public static bool ShouldSuppress(
        IEnumerable<Alert> existing,
        ToolId toolId,
        string? chamberId,
        string parameter,
        DateTimeOffset at,
        TimeSpan window,
        Severity? severity = null)
    {
        ArgumentNullException.ThrowIfNull(existing);
        if (window <= TimeSpan.Zero)
        {
            return false;
        }

        var normalizedChamber = string.IsNullOrWhiteSpace(chamberId) ? null : chamberId.Trim();
        foreach (var alert in existing)
        {
            // A cleared alert never hides a new violation, and a watch never hides an escalation to alarm.
            if (severity is not null && (!alert.IsActive || alert.Severity < severity.Value))
            {
                continue;
            }

            if (alert.ToolId != toolId)
            {
                continue;
            }

            if (!string.Equals(alert.Parameter, parameter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(alert.ChamberId, normalizedChamber, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var age = at - alert.RaisedAt;
            if (age >= TimeSpan.Zero && age < window)
            {
                return true;
            }
        }

        return false;
    }
}
