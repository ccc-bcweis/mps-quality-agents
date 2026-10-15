// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Equipment;
using Mps.Simulator.Fleet;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Simulator.Sites;

/// <summary>
/// Loads <c>data/fleet/sites.yaml</c>. The family files stay the original eight tools.
/// </summary>
public static class SiteLayoutLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static SiteLayout Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new FleetSpecException([$"Site file does not exist: {path}"]);
        }

        SiteFileDto dto;
        try
        {
            dto = Deserializer.Deserialize<SiteFileDto>(File.ReadAllText(path)) ?? new SiteFileDto();
        }
        catch (Exception exception)
        {
            throw new FleetSpecException([$"sites.yaml: {exception.Message}"]);
        }

        var errors = new List<string>();
        var sites = new List<SitePlan>();
        var seenSites = new HashSet<string>(StringComparer.Ordinal);
        var seenTools = new HashSet<string>(StringComparer.Ordinal);
        var seenBays = new HashSet<string>(StringComparer.Ordinal);
        foreach (var siteDto in dto.Sites ?? [])
        {
            var site = MapSite(siteDto, errors, seenSites, seenTools, seenBays);
            if (site is not null)
            {
                sites.Add(site);
            }
        }

        if (sites.Count < 3)
        {
            errors.Add($"The demo needs 3 plants. Found {sites.Count}.");
        }

        if (errors.Count > 0)
        {
            throw new FleetSpecException(errors);
        }

        return new SiteLayout(sites);
    }

    private static SitePlan? MapSite(
        SiteDto dto,
        List<string> errors,
        HashSet<string> seenSites,
        HashSet<string> seenTools,
        HashSet<string> seenBays)
    {
        var id = Required(dto.Id, "site id", errors);
        var name = Required(dto.Name, $"{id}: name", errors);
        var role = Required(dto.Role, $"{id}: role", errors);
        var location = Required(dto.Location, $"{id}: location", errors);
        var timezone = Required(dto.Timezone, $"{id}: timezone", errors);
        var prefix = dto.ToolPrefix ?? string.Empty;
        if (role.Length > 0 && role != SiteIds.FrontEndRole && role != SiteIds.PilotRole)
        {
            errors.Add($"{id}: role must be '{SiteIds.FrontEndRole}' or '{SiteIds.PilotRole}'.");
        }

        if (id.Length > 0 && !seenSites.Add(id))
        {
            errors.Add($"{id}: duplicate plant id.");
        }

        var bays = new List<BayPlan>();
        foreach (var bayDto in dto.Bays ?? [])
        {
            var bay = MapBay(id, prefix, bayDto, errors, seenTools, seenBays);
            if (bay is not null)
            {
                bays.Add(bay);
            }
        }

        if (bays.Count is < 3 or > 5)
        {
            errors.Add($"{id}: a plant needs 3 to 5 bays. Found {bays.Count}.");
        }

        var toolCount = bays.Sum(bay => bay.Tools.Count);
        if (toolCount is < 8 or > 16)
        {
            errors.Add($"{id}: a plant needs 8 to 16 tools. Found {toolCount}.");
        }

        var bayIds = bays.Select(bay => bay.Id).ToHashSet(StringComparer.Ordinal);
        var flows = new List<FlowPlan>();
        foreach (var flow in dto.Flows ?? [])
        {
            var from = Required(flow.FromBayId, $"{id}: flow from", errors);
            var to = Required(flow.ToBayId, $"{id}: flow to", errors);
            var label = Required(flow.Label, $"{id}: flow label", errors);
            if (from.Length > 0 && !bayIds.Contains(from))
            {
                errors.Add($"{id}: flow leaves unknown bay {from}.");
            }

            if (to.Length > 0 && !bayIds.Contains(to))
            {
                errors.Add($"{id}: flow enters unknown bay {to}.");
            }

            if (from.Length > 0 && to.Length > 0)
            {
                flows.Add(new FlowPlan(from, to, label));
            }
        }

        var behavior = new List<BehaviorPlan>();
        foreach (var item in dto.Behavior ?? [])
        {
            if (!FamilyParameterCatalog.TryParseFamily(item.Family, out var family))
            {
                errors.Add($"{id}: behavior family '{item.Family}' is not a tool family.");
                continue;
            }

            var parameter = Required(item.Parameter, $"{id}: behavior parameter", errors);
            if (parameter.Length > 0 && !FamilyParameterCatalog.For(family).ContainsKey(parameter))
            {
                errors.Add($"{id}: {parameter} is not a {family} parameter.");
            }

            if (item.SigmaScale is null || item.SigmaScale <= 0 || item.MeanOffset is null || !double.IsFinite(item.MeanOffset.Value))
            {
                errors.Add($"{id}: behavior on {parameter} needs a finite offset and a positive spread scale.");
                continue;
            }

            behavior.Add(new BehaviorPlan(family, parameter, item.MeanOffset.Value, item.SigmaScale.Value));
        }

        var adjust = new List<KpiAdjustPlan>();
        foreach (var item in dto.KpiAdjust ?? [])
        {
            if (!FamilyParameterCatalog.TryParseFamily(item.Family, out var family))
            {
                errors.Add($"{id}: throughput adjustment family '{item.Family}' is not a tool family.");
                continue;
            }

            adjust.Add(new KpiAdjustPlan(family, item.UptimeDelta ?? 0d, item.YieldDelta ?? 0d));
        }

        if (id.Length == 0)
        {
            return null;
        }

        return new SitePlan(id, name, role, location, timezone, prefix, bays, flows, behavior, adjust);
    }

    private static BayPlan? MapBay(
        string siteId,
        string prefix,
        BayDto dto,
        List<string> errors,
        HashSet<string> seenTools,
        HashSet<string> seenBays)
    {
        var id = Required(dto.Id, $"{siteId}: bay id", errors);
        var name = Required(dto.Name, $"{siteId}: bay name", errors);
        if (id.Length > 0 && !seenBays.Add(id))
        {
            errors.Add($"{siteId}: duplicate bay {id}.");
        }

        var tools = new List<ToolPlan>();
        foreach (var tool in dto.Tools ?? [])
        {
            var toolId = Required(tool.Id, $"{siteId}/{id}: tool id", errors);
            if (toolId.Length == 0)
            {
                continue;
            }

            if (!seenTools.Add(toolId))
            {
                errors.Add($"{toolId}: duplicate tool id.");
            }

            if (siteId.Length > 0 && !string.Equals(SiteIds.ForTool(toolId), siteId, StringComparison.Ordinal))
            {
                errors.Add($"{toolId}: prefix does not belong to {siteId}.");
            }

            if (prefix.Length > 0 && !toolId.StartsWith(prefix, StringComparison.Ordinal))
            {
                errors.Add($"{toolId}: must start with {prefix}.");
            }

            if (prefix.Length == 0 && (toolId.StartsWith("I-", StringComparison.Ordinal) || toolId.StartsWith("P-", StringComparison.Ordinal)))
            {
                errors.Add($"{toolId}: Northwater keeps the original ids.");
            }

            ToolFamily family;
            try
            {
                family = SiteIds.FamilyOf(toolId);
            }
            catch (InvalidOperationException exception)
            {
                errors.Add(exception.Message);
                continue;
            }

            var template = string.IsNullOrWhiteSpace(tool.Template) ? null : tool.Template.Trim();
            tools.Add(new ToolPlan(toolId, template, string.IsNullOrWhiteSpace(tool.Name) ? null : tool.Name.Trim(), family));
        }

        return id.Length == 0 ? null : new BayPlan(id, name, tools);
    }

    private static string Required(string? value, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{label} is required.");
            return string.Empty;
        }

        return value.Trim();
    }

    private sealed class SiteFileDto
    {
        public List<SiteDto>? Sites { get; set; }
    }

    private sealed class SiteDto
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Role { get; set; }

        public string? Location { get; set; }

        public string? Timezone { get; set; }

        public string? ToolPrefix { get; set; }

        public List<BayDto>? Bays { get; set; }

        public List<FlowDto>? Flows { get; set; }

        public List<BehaviorDto>? Behavior { get; set; }

        public List<KpiAdjustDto>? KpiAdjust { get; set; }
    }

    private sealed class BayDto
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public List<ToolDto>? Tools { get; set; }
    }

    private sealed class ToolDto
    {
        public string? Id { get; set; }

        public string? Template { get; set; }

        public string? Name { get; set; }
    }

    private sealed class FlowDto
    {
        public string? FromBayId { get; set; }

        public string? ToBayId { get; set; }

        public string? Label { get; set; }
    }

    private sealed class BehaviorDto
    {
        public string? Family { get; set; }

        public string? Parameter { get; set; }

        public double? MeanOffset { get; set; }

        public double? SigmaScale { get; set; }
    }

    private sealed class KpiAdjustDto
    {
        public string? Family { get; set; }

        public double? UptimeDelta { get; set; }

        public double? YieldDelta { get; set; }
    }
}
