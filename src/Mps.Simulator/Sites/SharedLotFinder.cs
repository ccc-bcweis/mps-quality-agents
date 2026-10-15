// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Impact;
using Mps.Simulator.GroundTruth;
using Mps.Simulator.Seed;

namespace Mps.Simulator.Sites;

/// <summary>
/// Finds a material lot that one tool shares with a tool at another plant. A lot used only inside one plant is ignored.
/// </summary>
public static class SharedLotFinder
{
    public static ImpactScope? Find(EnterpriseSeed seed, string toolId)
    {
        ArgumentNullException.ThrowIfNull(seed);
        if (string.IsNullOrWhiteSpace(toolId))
        {
            return null;
        }

        string? bestLot = null;
        var bestSites = 1;
        foreach (var lotId in seed.MaterialUses
            .Where(use => string.Equals(use.ToolId, toolId, StringComparison.Ordinal))
            .Select(use => use.LotId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
        {
            var sites = seed.MaterialUses
                .Where(use => string.Equals(use.LotId, lotId, StringComparison.Ordinal))
                .Select(SiteOf)
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (sites > bestSites)
            {
                bestLot = lotId;
                bestSites = sites;
            }
        }

        if (bestLot is null)
        {
            return null;
        }

        var lot = seed.MaterialLots.FirstOrDefault(item => item.LotId == bestLot);
        if (lot is null)
        {
            return null;
        }

        var described = string.Join(
            " and ",
            seed.MaterialUses
                .Where(use => string.Equals(use.LotId, bestLot, StringComparison.Ordinal))
                .Select(use => (Tool: use.ToolId, Site: SiteOf(use)))
                .Distinct()
                .OrderBy(row => row.Site, StringComparer.Ordinal)
                .ThenBy(row => row.Tool, StringComparer.Ordinal)
                .Select(row => row.Tool + " at " + SiteIds.NameFor(row.Site)));
        var note = "Lot " + lot.LotId + " from " + lot.SupplierName + " was used on " + described + ". The same lot reached more than one plant.";
        return new ImpactScope(ImpactScopeKind.MaterialLot, lot.LotId, lot.ReceivedAt, note);
    }

    private static string SiteOf(MaterialUse use)
    {
        return string.IsNullOrWhiteSpace(use.SiteId) ? SiteIds.ForTool(use.ToolId) : use.SiteId;
    }
}
