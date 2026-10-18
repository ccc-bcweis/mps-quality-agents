// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Demo;
using Mps.Api.Live;
using Mps.Application.Errors;
using Mps.Application.Persistence;
using Mps.Domain.Identity;
using Mps.Simulator.Fleet;
using Mps.Simulator.Sites;

namespace Mps.Api.Http;

public static partial class QualityEndpoints
{
    /// <summary>Sites, tools and the live fleet view.</summary>
    private static void MapSitesAndFleet(WebApplication app)
    {
        app.MapGet("/api/sites", (SiteWorld world) => Results.Ok(world.Sites.Select(SiteResponses.ToSummary).ToArray()));
        app.MapGet("/api/sites/compare", (string? family, SiteWorld world) =>
        {
            if (!FamilyParameterCatalog.TryParseFamily(family, out var parsed))
            {
                throw new IntakeValidationException("Query family must be deposition, etch, strip, or clean.");
            }

            return Results.Ok(SiteResponses.ToComparison(SiteComparison.Compare(world, parsed, Mps.Application.Spc.SpcRuleSettings.Shared.ControlLimitSigma, Mps.Application.Spc.SpcRuleSettings.Shared.WatchSigma)));
        });
        app.MapGet("/api/sites/{siteId}", async (string siteId, SiteWorld world, IAlertLog alerts, CancellationToken cancellationToken) =>
        {
            var site = world.Find(siteId);
            if (site is null)
            {
                return Results.NotFound();
            }

            var status = ToolAlertStatus.ByTool(await alerts.ListAsync(null, cancellationToken).ConfigureAwait(false));
            return Results.Ok(SiteResponses.ToDetail(site, toolId => ToolAlertStatus.Of(status, toolId)));
        });
        app.MapGet("/api/tools", (string? siteId, PlantSimulation plant) =>
        {
            // All sites is the union of every plant's floor, the same set the live stream reports.
            var all = string.IsNullOrWhiteSpace(siteId) || string.Equals(siteId.Trim(), "all", StringComparison.OrdinalIgnoreCase);
            var tools = plant.World.Sites
                .Where(item => all || string.Equals(item.Id, siteId!.Trim(), StringComparison.OrdinalIgnoreCase))
                .SelectMany(item => plant.ToolsForSite(item.Id));
            return Results.Ok(tools.Select(ContractMapper.ToResponse).ToArray());
        });
        app.MapGet("/api/tools/{toolId}", async (string toolId, IFleetCatalog fleet, CancellationToken cancellationToken) =>
        {
            var tool = await fleet.FindAsync(new ToolId(toolId), cancellationToken).ConfigureAwait(false);
            return tool is null
                ? Results.NotFound()
                : Results.Ok(ContractMapper.ToResponse(tool));
        });
        app.MapGet("/api/fleet/live", async (PlantSimulation plant, ConsoleDataMode mode, IAlertLog alerts, CancellationToken cancellationToken) =>
        {
            if (mode.IsReplay)
            {
                return Results.Ok(ReplayProjection.Fleet(mode.Messages));
            }

            var status = ToolAlertStatus.ByTool(await alerts.ListAsync(null, cancellationToken).ConfigureAwait(false));
            return Results.Ok(LiveResponses.Fleet(plant, status));
        });
        app.MapGet("/api/tools/{toolId}/live", async (string toolId, PlantSimulation plant, ConsoleDataMode mode, IAlertLog alerts, CancellationToken cancellationToken) =>
        {
            if (!mode.IsReplay)
            {
                var status = ToolAlertStatus.ByTool(await alerts.ListAsync(null, cancellationToken).ConfigureAwait(false));
                var live = LiveResponses.Tool(plant, toolId, status);
                return live is null ? Results.NotFound() : Results.Ok(live);
            }

            var recorded = ReplayProjection.Tool(mode.Messages, toolId);
            if (recorded is not null)
            {
                return Results.Ok(recorded);
            }

            var placed = plant.FindTool(toolId);
            if (placed is null)
            {
                return Results.NotFound();
            }

            var now = mode.Messages.Count == 0 ? plant.SimulatedNow : mode.Messages.Max(message => message.OccurredAt);
            return Results.Ok(new ToolLiveResponse(toolId, ContractMapper.SiteForTool(placed.SiteId ?? toolId), now, "nominal", [], []));
        });
    }
}
