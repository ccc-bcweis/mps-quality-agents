// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Incidents;

namespace Mps.Api.Http;

/// <summary>
/// The one tool status every view reads (Sites, Production line tiles, tool page): the worst active alert on the tool.
/// High or Critical is alarm, any other active alert is watch, no active alert is nominal.
/// </summary>
public static class ToolAlertStatus
{
    public static IReadOnlyDictionary<string, string> ByTool(IEnumerable<Alert> alerts)
    {
        ArgumentNullException.ThrowIfNull(alerts);
        return alerts
            .Where(alert => alert.IsActive)
            .GroupBy(alert => alert.ToolId.ToString(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Max(alert => alert.Severity) >= Severity.High ? "alarm" : "watch",
                StringComparer.OrdinalIgnoreCase);
    }

    public static string Of(IReadOnlyDictionary<string, string> byTool, string toolId) =>
        byTool.TryGetValue(toolId, out var status) ? status : "nominal";
}
