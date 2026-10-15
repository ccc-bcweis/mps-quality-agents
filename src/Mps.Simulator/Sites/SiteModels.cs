// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Equipment;
using Mps.Simulator.Fleet;

namespace Mps.Simulator.Sites;

public sealed class SiteLayout
{
    public SiteLayout(IReadOnlyList<SitePlan> sites)
    {
        ArgumentNullException.ThrowIfNull(sites);
        Sites = sites.ToArray();
        var families = new Dictionary<string, ToolFamily>(StringComparer.Ordinal);
        foreach (var site in Sites)
        {
            foreach (var bay in site.Bays)
            {
                foreach (var tool in bay.Tools)
                {
                    families[tool.Id] = tool.Family;
                }
            }
        }

        Families = families;
    }

    public IReadOnlyList<SitePlan> Sites { get; }

    public IReadOnlyDictionary<string, ToolFamily> Families { get; }
}

public sealed class SitePlan
{
    public SitePlan(
        string id,
        string name,
        string role,
        string location,
        string timezone,
        string toolPrefix,
        IReadOnlyList<BayPlan> bays,
        IReadOnlyList<FlowPlan> flows,
        IReadOnlyList<BehaviorPlan> behavior,
        IReadOnlyList<KpiAdjustPlan> kpiAdjust)
    {
        Id = id;
        Name = name;
        Role = role;
        Location = location;
        Timezone = timezone;
        ToolPrefix = toolPrefix;
        Bays = bays;
        Flows = flows;
        Behavior = behavior;
        KpiAdjust = kpiAdjust;
    }

    public string Id { get; }

    public string Name { get; }

    public string Role { get; }

    public string Location { get; }

    public string Timezone { get; }

    public string ToolPrefix { get; }

    public IReadOnlyList<BayPlan> Bays { get; }

    public IReadOnlyList<FlowPlan> Flows { get; }

    public IReadOnlyList<BehaviorPlan> Behavior { get; }

    public IReadOnlyList<KpiAdjustPlan> KpiAdjust { get; }

    public int ToolCount => Bays.Sum(bay => bay.Tools.Count);
}

public sealed class BayPlan
{
    public BayPlan(string id, string name, IReadOnlyList<ToolPlan> tools)
    {
        Id = id;
        Name = name;
        Tools = tools;
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyList<ToolPlan> Tools { get; }
}

public sealed class ToolPlan
{
    public ToolPlan(string id, string? templateId, string? name, ToolFamily family)
    {
        Id = id;
        TemplateId = templateId;
        Name = name;
        Family = family;
    }

    public string Id { get; }

    public string? TemplateId { get; }

    public string? Name { get; }

    public ToolFamily Family { get; }
}

public sealed class FlowPlan
{
    public FlowPlan(string fromBayId, string toBayId, string label)
    {
        FromBayId = fromBayId;
        ToBayId = toBayId;
        Label = label;
    }

    public string FromBayId { get; }

    public string ToBayId { get; }

    public string Label { get; }
}

public sealed class BehaviorPlan
{
    public BehaviorPlan(ToolFamily family, string parameter, double meanOffset, double sigmaScale)
    {
        Family = family;
        Parameter = parameter;
        MeanOffset = meanOffset;
        SigmaScale = sigmaScale;
    }

    public ToolFamily Family { get; }

    public string Parameter { get; }

    public double MeanOffset { get; }

    public double SigmaScale { get; }
}

public sealed class KpiAdjustPlan
{
    public KpiAdjustPlan(ToolFamily family, double uptimeDelta, double yieldDelta)
    {
        Family = family;
        UptimeDelta = uptimeDelta;
        YieldDelta = yieldDelta;
    }

    public ToolFamily Family { get; }

    public double UptimeDelta { get; }

    public double YieldDelta { get; }
}

public sealed class ThroughputKpi
{
    public ThroughputKpi(double waferStartsPerHour, double waferOutsPerHour, int wip, double yieldPct, double uptimePct)
    {
        WaferStartsPerHour = waferStartsPerHour;
        WaferOutsPerHour = waferOutsPerHour;
        Wip = wip;
        YieldPct = yieldPct;
        UptimePct = uptimePct;
    }

    public double WaferStartsPerHour { get; }

    public double WaferOutsPerHour { get; }

    public int Wip { get; }

    public double YieldPct { get; }

    public double UptimePct { get; }
}

public sealed class PlacedTool
{
    public PlacedTool(
        string id,
        string name,
        ToolFamily family,
        string model,
        string bayId,
        string siteId,
        string status,
        double x,
        double y,
        double waferStartsPerHour,
        double yieldPct,
        double uptimePct,
        ToolSpec spec)
    {
        Id = id;
        Name = name;
        Family = family;
        Model = model;
        BayId = bayId;
        SiteId = siteId;
        Status = status;
        X = x;
        Y = y;
        WaferStartsPerHour = waferStartsPerHour;
        YieldPct = yieldPct;
        UptimePct = uptimePct;
        Spec = spec;
    }

    public string Id { get; }

    public string Name { get; }

    public ToolFamily Family { get; }

    public string Model { get; }

    public string BayId { get; }

    public string SiteId { get; }

    public string Status { get; }

    public double X { get; }

    public double Y { get; }

    public double WaferStartsPerHour { get; }

    public double YieldPct { get; }

    public double UptimePct { get; }

    public ToolSpec Spec { get; }
}

public sealed class SimulatedBay
{
    public SimulatedBay(string id, string name, IReadOnlyList<PlacedTool> tools, ThroughputKpi kpis)
    {
        Id = id;
        Name = name;
        Tools = tools;
        Kpis = kpis;
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyList<PlacedTool> Tools { get; }

    public ThroughputKpi Kpis { get; }
}

public sealed class SimulatedFlow
{
    public SimulatedFlow(string fromBayId, string toBayId, string label)
    {
        FromBayId = fromBayId;
        ToBayId = toBayId;
        Label = label;
    }

    public string FromBayId { get; }

    public string ToBayId { get; }

    public string Label { get; }
}

public sealed class SimulatedSite
{
    public SimulatedSite(
        string id,
        string name,
        string role,
        string location,
        string timezone,
        IReadOnlyList<SimulatedBay> bays,
        IReadOnlyList<SimulatedFlow> flows,
        IReadOnlyList<BehaviorPlan> behavior,
        ThroughputKpi kpis)
    {
        Id = id;
        Name = name;
        Role = role;
        Location = location;
        Timezone = timezone;
        Bays = bays;
        Flows = flows;
        Behavior = behavior;
        Kpis = kpis;
    }

    public string Id { get; }

    public string Name { get; }

    public string Role { get; }

    public string Location { get; }

    public string Timezone { get; }

    public IReadOnlyList<SimulatedBay> Bays { get; }

    public IReadOnlyList<SimulatedFlow> Flows { get; }

    public IReadOnlyList<BehaviorPlan> Behavior { get; }

    public ThroughputKpi Kpis { get; }

    public IEnumerable<PlacedTool> Tools => Bays.SelectMany(bay => bay.Tools);
}

public sealed class SiteWorld
{
    public SiteWorld(int seed, IReadOnlyList<SimulatedSite> sites)
    {
        ArgumentNullException.ThrowIfNull(sites);
        Seed = seed;
        Sites = sites.ToArray();
    }

    public int Seed { get; }

    public IReadOnlyList<SimulatedSite> Sites { get; }

    public SimulatedSite? Find(string siteId)
    {
        return Sites.FirstOrDefault(site => string.Equals(site.Id, siteId, StringComparison.Ordinal));
    }

    public PlacedTool? FindTool(string toolId)
    {
        foreach (var site in Sites)
        {
            foreach (var tool in site.Tools)
            {
                if (string.Equals(tool.Id, toolId, StringComparison.Ordinal))
                {
                    return tool;
                }
            }
        }

        return null;
    }
}
