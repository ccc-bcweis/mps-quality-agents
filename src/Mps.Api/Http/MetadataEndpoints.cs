// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Metadata;
using Mps.Domain.Incidents;

namespace Mps.Api.Http;

public static class MetadataEndpoints
{
    public static void Map(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/api/metadata/company", (IMetadataStore store) => Results.Ok(ToCompany(store)));
        app.MapGet("/api/metadata/sites", (IMetadataStore store) => Results.Ok(store.Sites.Select(site => ToSite(site, store)).ToArray()));
        app.MapGet("/api/metadata/tools/{toolId}", (string toolId, IMetadataStore store) =>
        {
            var tool = store.FindTool(toolId);
            return tool is null ? Results.NotFound() : Results.Ok(ToTool(tool, store));
        });
        app.MapGet("/api/metadata/rules", (IMetadataStore store) => Results.Ok(store.Rules.Select(ToRule).ToArray()));
        app.MapGet("/api/metadata/settings", () => Results.Ok(RuleSettings.Read(
            Mps.Application.Spc.SpcRuleSettings.Shared,
            Mps.Agents.Investigation.ShapeWait.Load())));
    }

    private static CompanyMetadataResponse ToCompany(IMetadataStore store)
    {
        var company = store.Company;
        return new CompanyMetadataResponse(
            company.Id,
            company.Name,
            company.Purpose,
            company.Description,
            company.Industry,
            store.LoadDuration.TotalMilliseconds);
    }

    private static MetadataSiteResponse ToSite(SiteProfile site, IMetadataStore store)
    {
        var capabilities = store.Capabilities
            .Where(capability => capability.SiteId.Equals(site.Id, StringComparison.OrdinalIgnoreCase))
            .Select(capability => ToCapability(capability, store))
            .ToArray();
        return new MetadataSiteResponse(site.Id, site.Name, site.Purpose, site.Description, site.Timezone, capabilities);
    }

    private static MetadataCapabilityResponse ToCapability(CapabilityProfile capability, IMetadataStore store)
    {
        var lines = store.Lines
            .Where(line => line.CapabilityId.Equals(capability.Id, StringComparison.OrdinalIgnoreCase))
            .OrderBy(line => line.Sequence)
            .Select(line => ToLine(line, store))
            .ToArray();
        var direct = store.Tools
            .Where(tool => tool.LineId is null && string.Equals(tool.CapabilityId, capability.Id, StringComparison.OrdinalIgnoreCase))
            .Select(ToCard)
            .ToArray();
        return new MetadataCapabilityResponse(
            capability.Id,
            capability.Name,
            capability.Type,
            capability.Description,
            capability.Simulated,
            lines,
            direct);
    }

    private static MetadataLineResponse ToLine(ProductionLine line, IMetadataStore store)
    {
        var stepNames = line.IncludesLineIds
            .Select(id => store.Lines.First(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).Name)
            .ToArray();
        var tools = line.ToolIds.Select(id => ToCard(store.FindTool(id)!)).ToArray();
        return new MetadataLineResponse(line.Id, line.Name, line.LineType, line.Sequence, line.Characteristics, stepNames, tools);
    }

    private static MetadataToolCard ToCard(ToolProfile tool)
    {
        return new MetadataToolCard(tool.Id, tool.Name, tool.ToolType, tool.Simulated);
    }

    private static MetadataToolResponse ToTool(ToolProfile tool, IMetadataStore store)
    {
        var site = store.Sites.First(item => item.Id.Equals(tool.SiteId, StringComparison.OrdinalIgnoreCase));
        var lineName = tool.LineId is null
            ? null
            : store.Lines.First(item => item.Id.Equals(tool.LineId, StringComparison.OrdinalIgnoreCase)).Name;
        return new MetadataToolResponse(
            tool.Id,
            tool.Name,
            tool.ToolType,
            tool.SiteId,
            site.Name,
            tool.LineId,
            lineName,
            tool.CapabilityId,
            tool.Simulated,
            tool.Inputs.Select(port => new MetadataPortResponse(port.Name, port.Description)).ToArray(),
            tool.Outputs.Select(port => new MetadataPortResponse(port.Name, port.Description)).ToArray(),
            tool.Parameters.Select(parameter => new MetadataParameterResponse(
                parameter.Id,
                parameter.Name,
                parameter.ParameterClass,
                parameter.Unit,
                parameter.Lcl,
                parameter.Ucl,
                parameter.AlertLow,
                parameter.AlertHigh,
                parameter.Nominal,
                parameter.Telemetry)).ToArray());
    }

    private static BusinessRuleResponse ToRule(BusinessRule rule)
    {
        return new BusinessRuleResponse(
            rule.Id,
            rule.Name,
            rule.WhenPlain,
            rule.ThenPlain,
            rule.Why,
            rule.Order,
            rule.Actions.Select(action => new RuleActionResponse(action.Type, action.Priority, action.Queue)).ToArray())
        {
            Conditions = RuleSettings.Conditions(rule.When),
        };
    }
}

public sealed record CompanyMetadataResponse(
    string Id,
    string Name,
    string Purpose,
    string Description,
    string Industry,
    double LoadMilliseconds);

public sealed record MetadataSiteResponse(
    string Id,
    string Name,
    string Purpose,
    string Description,
    string Timezone,
    IReadOnlyList<MetadataCapabilityResponse> Capabilities);

public sealed record MetadataCapabilityResponse(
    string Id,
    string Name,
    CapabilityType Type,
    string Description,
    bool Simulated,
    IReadOnlyList<MetadataLineResponse> Lines,
    IReadOnlyList<MetadataToolCard> DirectTools);

public sealed record MetadataLineResponse(
    string Id,
    string Name,
    string LineType,
    int Sequence,
    string Characteristics,
    IReadOnlyList<string> StepNames,
    IReadOnlyList<MetadataToolCard> Tools);

public sealed record MetadataToolCard(string Id, string Name, string ToolType, bool Simulated);

public sealed record MetadataPortResponse(string Name, string Description);

public sealed record MetadataParameterResponse(
    string Id,
    string Name,
    string ParameterClass,
    string Unit,
    double Lcl,
    double Ucl,
    double AlertLow,
    double AlertHigh,
    double Nominal,
    bool Telemetry);

public sealed record MetadataToolResponse(
    string Id,
    string Name,
    string ToolType,
    string SiteId,
    string SiteName,
    string? LineId,
    string? LineName,
    string? CapabilityId,
    bool Simulated,
    IReadOnlyList<MetadataPortResponse> Inputs,
    IReadOnlyList<MetadataPortResponse> Outputs,
    IReadOnlyList<MetadataParameterResponse> Parameters);

public sealed record RuleActionResponse(string Type, string? Priority, string? Queue);

public sealed record BusinessRuleResponse(
    string Id,
    string Name,
    string WhenPlain,
    string ThenPlain,
    string Why,
    int Order,
    IReadOnlyList<RuleActionResponse> Actions)
{
    /// <summary>The rule's configured conditions, so the screen can show each threshold as a field.</summary>
    public IReadOnlyList<RuleConditionResponse> Conditions { get; init; } = [];
}
