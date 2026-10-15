// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Equipment;
using Mps.Simulator.Sites;

namespace Mps.Api.Http;

/// <summary>
/// Optional plant filter for list routes. An empty value means every plant.
/// </summary>
public static class SiteQuery
{
    public static bool Matches(string? siteId, string toolId)
    {
        if (string.IsNullOrWhiteSpace(siteId))
        {
            return true;
        }

        return string.Equals(SiteIds.ForTool(toolId), siteId.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static bool MatchesTool(string? siteId, Tool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (string.IsNullOrWhiteSpace(siteId))
        {
            return true;
        }

        var actual = string.IsNullOrWhiteSpace(tool.SiteId) ? SiteIds.ForTool(tool.Id.ToString()) : tool.SiteId;
        return string.Equals(actual, siteId.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
