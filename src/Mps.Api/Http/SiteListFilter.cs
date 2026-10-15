// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Persistence;
using Mps.Domain.Equipment;
using Mps.Simulator.Sites;

namespace Mps.Api.Http;

/// <summary>
/// Optional plant filter for history lists. An unknown plant matches no tools, so the list is empty.
/// </summary>
internal static class SiteListFilter
{
    public static async Task<HashSet<string>?> ToolIdsAsync(string? siteId, IFleetCatalog fleet, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        if (string.IsNullOrWhiteSpace(siteId))
        {
            return null;
        }

        var wanted = siteId.Trim();
        var tools = await fleet.ListAsync(cancellationToken).ConfigureAwait(false);
        return tools
            .Where(tool => string.Equals(SiteOf(tool), wanted, StringComparison.OrdinalIgnoreCase))
            .Select(tool => tool.Id.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<T> Matching<T>(IReadOnlyList<T> rows, HashSet<string>? toolIds, Func<T, string> toolId)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(toolId);
        if (toolIds is null)
        {
            return rows;
        }

        return rows.Where(row => toolIds.Contains(toolId(row))).ToArray();
    }

    private static string SiteOf(Tool tool) =>
        string.IsNullOrWhiteSpace(tool.SiteId) ? SiteIds.ForTool(tool.Id.ToString()) : tool.SiteId;
}
