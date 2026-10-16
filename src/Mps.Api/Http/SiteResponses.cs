// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Simulator.Sites;

namespace Mps.Api.Http;

public static class SiteResponses
{
    public static SiteSummaryResponse ToSummary(SimulatedSite site)
    {
        return new SiteSummaryResponse(
            site.Id,
            site.Name,
            site.Role,
            site.Location,
            site.Timezone,
            site.Bays.Select(bay => new SiteBaySummaryResponse(bay.Id, bay.Name, bay.Tools.Count)).ToArray(),
            ToThroughput(site.Kpis));
    }

    public static SiteDetailResponse ToDetail(SimulatedSite site, Func<string, string>? liveStatus = null)
    {
        return new SiteDetailResponse(
            site.Id,
            site.Name,
            site.Role,
            site.Location,
            site.Timezone,
            site.Bays.Select(bay => new SiteBayDetailResponse(
                bay.Id,
                bay.Name,
                bay.Tools.Select(tool => ToPlaced(tool, liveStatus)).ToArray(),
                ToThroughput(bay.Kpis))).ToArray(),
            site.Flows.Select(flow => new WaferFlowResponse(flow.FromBayId, flow.ToBayId, flow.Label)).ToArray(),
            ToThroughput(site.Kpis));
    }

    public static FamilyComparisonResponse ToComparison(FamilyComparison comparison)
    {
        return new FamilyComparisonResponse(
            comparison.Family,
            comparison.ReferenceSiteId,
            comparison.Parameters.Select(parameter => new ParameterComparisonResponse(
                parameter.Parameter,
                parameter.Unit,
                parameter.Sites.Select(row => new SiteParameterStatsResponse(
                    row.SiteId,
                    row.SiteName,
                    row.Mean,
                    row.Sigma,
                    row.AlarmRate,
                    row.UptimePct,
                    row.YieldPct,
                    row.Discrepancy,
                    row.Reason)).ToArray())).ToArray());
    }

    private static PlacedToolResponse ToPlaced(PlacedTool tool, Func<string, string>? liveStatus)
    {
        // Status comes from the same live rule as the Production line and tool page, never the site seed.
        var status = liveStatus is null ? tool.Status : SiteStatus(liveStatus(tool.Id));
        return new PlacedToolResponse(tool.Id, tool.Name, tool.Family, tool.Model, status, tool.X, tool.Y, tool.SiteId);
    }

    /// <summary>The site contract spells watch as warning.</summary>
    public static string SiteStatus(string liveStatus) => liveStatus == "watch" ? "warning" : liveStatus;

    private static ThroughputResponse ToThroughput(ThroughputKpi kpis)
    {
        return new ThroughputResponse(kpis.WaferStartsPerHour, kpis.WaferOutsPerHour, kpis.Wip, kpis.YieldPct, kpis.UptimePct);
    }
}
