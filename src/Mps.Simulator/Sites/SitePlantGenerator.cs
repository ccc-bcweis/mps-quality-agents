// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Equipment;
using Mps.Simulator.Fleet;
using Mps.Simulator.Telemetry;

namespace Mps.Simulator.Sites;

/// <summary>
/// Builds the three plants from the family templates and <c>sites.yaml</c>.
/// The same seed always yields the same positions, telemetry, and throughput.
/// </summary>
public static class SitePlantGenerator
{
    public const int SampleCount = 128;

    public static SiteWorld Generate(FleetCatalog fleet, SiteLayout layout, int seed, IReadOnlyCollection<string>? alarmToolIds = null)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(layout);
        var alarms = new HashSet<string>(alarmToolIds ?? [], StringComparer.Ordinal);
        var sites = new List<SimulatedSite>();
        foreach (var plan in layout.Sites)
        {
            sites.Add(Build(fleet, plan, seed, alarms));
        }

        return new SiteWorld(seed, sites);
    }

    public static SiteWorld Load(string dataDirectory, int seed)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            throw new ArgumentException("Data directory is required.", nameof(dataDirectory));
        }

        var fleetDirectory = Path.Combine(dataDirectory, "fleet");
        var fleet = FleetCatalogLoader.Load(fleetDirectory);
        var layout = SiteLayoutLoader.Load(Path.Combine(fleetDirectory, "sites.yaml"));
        // Catalog faults are a library. They are not active until a scenario or a manual injection.
        return Generate(fleet, layout, seed);
    }

    private static SimulatedSite Build(FleetCatalog fleet, SitePlan plan, int seed, HashSet<string> alarms)
    {
        var bays = new List<SimulatedBay>();
        foreach (var bay in plan.Bays)
        {
            var tools = new List<PlacedTool>();
            for (var index = 0; index < bay.Tools.Count; index++)
            {
                tools.Add(Place(fleet, plan, bay, bay.Tools[index], index, seed, alarms));
            }

            bays.Add(new SimulatedBay(bay.Id, bay.Name, tools, Rollup(tools)));
        }

        var flows = plan.Flows.Select(flow => new SimulatedFlow(flow.FromBayId, flow.ToBayId, flow.Label)).ToArray();
        var all = bays.SelectMany(bay => bay.Tools).ToArray();
        return new SimulatedSite(plan.Id, plan.Name, plan.Role, plan.Location, plan.Timezone, bays, flows, plan.Behavior, Rollup(all));
    }

    private static PlacedTool Place(
        FleetCatalog fleet,
        SitePlan plan,
        BayPlan bay,
        ToolPlan planned,
        int index,
        int seed,
        HashSet<string> alarms)
    {
        var spec = Resolve(fleet, plan.Id, bay.Id, planned);
        var (x, y) = Position(seed, plan.Id, planned.Id, index);
        var (starts, yield, uptime) = Rates(seed, plan, planned, spec.Family);
        var status = alarms.Contains(planned.Id)
            ? "alarm"
            : plan.Behavior.Any(item => item.Family == spec.Family)
                ? "warning"
                : "nominal";
        return new PlacedTool(
            spec.Id,
            spec.Name,
            spec.Family,
            spec.Model,
            bay.Id,
            plan.Id,
            status,
            x,
            y,
            starts,
            yield,
            uptime,
            spec);
    }

    private static ToolSpec Resolve(FleetCatalog fleet, string siteId, string bayId, ToolPlan planned)
    {
        var existing = fleet.Tools.FirstOrDefault(tool => string.Equals(tool.Id, planned.Id, StringComparison.Ordinal));
        if (existing is not null)
        {
            if (!string.Equals(existing.Bay, bayId, StringComparison.Ordinal))
            {
                throw new FleetSpecException([$"{planned.Id} is {existing.Bay} in the fleet file and {bayId} in {siteId}."]);
            }

            if (planned.TemplateId is not null)
            {
                throw new FleetSpecException([$"{planned.Id} is already in the fleet file and does not take a template."]);
            }

            return existing;
        }

        if (string.IsNullOrWhiteSpace(planned.TemplateId))
        {
            throw new FleetSpecException([$"{planned.Id} needs a template tool from the original fleet."]);
        }

        var template = fleet.Tools.FirstOrDefault(tool => string.Equals(tool.Id, planned.TemplateId, StringComparison.Ordinal));
        if (template is null)
        {
            throw new FleetSpecException([$"{planned.Id} template {planned.TemplateId} is not in the original fleet."]);
        }

        if (template.Family != planned.Family)
        {
            throw new FleetSpecException([$"{planned.Id} family {planned.Family} does not match template {template.Id}."]);
        }

        var name = string.IsNullOrWhiteSpace(planned.Name) ? template.Family + " " + planned.Id : planned.Name;
        return Clone(template, planned.Id, name, bayId);
    }

    public static ToolSpec Clone(ToolSpec template, string id, string name, string bay)
    {
        var chambers = template.Chambers
            .Select(chamber => new ChamberSpec(Rewrite(chamber.Id, template.Id, id), chamber.Name))
            .ToArray();
        var sensors = template.Sensors
            .Select(sensor => new SensorSpec(
                Rewrite(sensor.Id, template.Id, id),
                sensor.Type,
                sensor.Unit,
                sensor.Subsystem,
                sensor.SchematicAnchor,
                sensor.Limits,
                sensor.ChamberId is null ? null : Rewrite(sensor.ChamberId, template.Id, id),
                sensor.Parameter,
                sensor.TracksSensorId is null ? null : Rewrite(sensor.TracksSensorId, template.Id, id)))
            .ToArray();
        return new ToolSpec(id, name, template.Family, template.Model, bay, chambers, template.Recipes, sensors);
    }

    private static string Rewrite(string value, string fromId, string toId)
    {
        if (value.StartsWith(fromId, StringComparison.Ordinal))
        {
            return toId + value[fromId.Length..];
        }

        return toId + "-" + value;
    }

    private static (double X, double Y) Position(int seed, string siteId, string toolId, int index)
    {
        var rng = SeededRng.Create(seed, siteId + "|" + toolId + "|place");
        var column = index % 3;
        var row = index / 3;
        var x = Clamp(16d + (column * 30d) + ((rng.NextUnit() - 0.5d) * 6d));
        var y = Clamp(22d + (row * 34d) + ((rng.NextUnit() - 0.5d) * 6d));
        return (Round1(x), Round1(y));
    }

    private static (double Starts, double Yield, double Uptime) Rates(int seed, SitePlan plan, ToolPlan tool, ToolFamily family)
    {
        var rng = SeededRng.Create(seed, plan.Id + "|" + tool.Id + "|kpi");
        var starts = Round1(3.8d + (rng.NextUnit() * 2.4d));
        var yield = 95.5d + (rng.NextUnit() * 3.2d);
        var uptime = 93.0d + (rng.NextUnit() * 5.0d);
        foreach (var adjust in plan.KpiAdjust)
        {
            if (adjust.Family == family)
            {
                yield += adjust.YieldDelta;
                uptime += adjust.UptimeDelta;
            }
        }

        return (starts, Round1(Math.Clamp(yield, 88d, 99.5d)), Round1(Math.Clamp(uptime, 85d, 99.5d)));
    }

    private static ThroughputKpi Rollup(IReadOnlyList<PlacedTool> tools)
    {
        if (tools.Count == 0)
        {
            return new ThroughputKpi(0, 0, 0, 0, 0);
        }

        var starts = tools.Sum(tool => tool.WaferStartsPerHour);
        var outs = tools.Sum(tool => tool.WaferStartsPerHour * tool.YieldPct / 100d);
        var wip = tools.Sum(tool => (int)Math.Round(tool.WaferStartsPerHour * 2.4d, MidpointRounding.AwayFromZero));
        var yield = tools.Sum(tool => tool.WaferStartsPerHour * tool.YieldPct) / starts;
        var uptime = tools.Sum(tool => tool.WaferStartsPerHour * tool.UptimePct) / starts;
        return new ThroughputKpi(Round1(starts), Round1(outs), wip, Round1(yield), Round1(uptime));
    }

    private static double Clamp(double value) => Math.Clamp(value, 4d, 96d);

    public static double Round1(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
}
