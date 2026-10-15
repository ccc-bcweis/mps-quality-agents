// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Application.Metadata;

public enum CapabilityType
{
    Manufacturing,
    Warehousing,
    ResearchAndDevelopment,
    Sales,
    BackOffice,
    Other,
}

public sealed record CompanyProfile(string Id, string Name, string Purpose, string Description, string Industry);

public sealed record SiteProfile(string Id, string Name, string Purpose, string Description, string Timezone);

public sealed record CapabilityProfile(
    string Id,
    string SiteId,
    string Name,
    CapabilityType Type,
    string Description,
    bool Simulated);

public sealed record ProductionLine(
    string Id,
    string CapabilityId,
    string Name,
    string LineType,
    int Sequence,
    string Characteristics,
    IReadOnlyList<string> IncludesLineIds,
    IReadOnlyList<string> ToolIds);

public sealed record ToolPort(string Name, string Description);

public sealed record ToolParameter(
    string Id,
    string Name,
    string ParameterClass,
    string Unit,
    bool Telemetry,
    double Lcl,
    double Ucl,
    double AlertLow,
    double AlertHigh,
    double Nominal);

public sealed record ToolProfile(
    string Id,
    string Name,
    string SiteId,
    string? LineId,
    string? CapabilityId,
    string ToolType,
    bool Simulated,
    IReadOnlyList<ToolPort> Inputs,
    IReadOnlyList<ToolPort> Outputs,
    IReadOnlyList<ToolParameter> Parameters);

public sealed class RuleWhen
{
    public string? Severity { get; init; }

    public string? SeverityAtLeast { get; init; }

    public string? SeverityAtMost { get; init; }

    public string? AlertRuleId { get; init; }

    public string? ParameterClass { get; init; }

    public string? ParameterId { get; init; }

    public string? ToolId { get; init; }

    public string? SiteId { get; init; }

    public int? RepeatAtLeast { get; init; }

    public double? ConfidenceBelow { get; init; }

    public string? EvidenceSufficiency { get; init; }

    public string? Channel { get; init; }

    public bool? RequiresAlert { get; init; }

    public bool IsEmpty()
    {
        return Severity is null
            && SeverityAtLeast is null
            && SeverityAtMost is null
            && string.IsNullOrWhiteSpace(AlertRuleId)
            && string.IsNullOrWhiteSpace(ParameterClass)
            && string.IsNullOrWhiteSpace(ParameterId)
            && string.IsNullOrWhiteSpace(ToolId)
            && string.IsNullOrWhiteSpace(SiteId)
            && RepeatAtLeast is null
            && ConfidenceBelow is null
            && string.IsNullOrWhiteSpace(EvidenceSufficiency)
            && string.IsNullOrWhiteSpace(Channel)
            && RequiresAlert is null;
    }
}

public sealed record RuleAction(string Type, string? Priority, string? Queue);

public sealed record BusinessRule(
    string Id,
    string Name,
    int Order,
    string WhenPlain,
    string ThenPlain,
    string Why,
    RuleWhen When,
    IReadOnlyList<RuleAction> Actions);

public sealed record ProcessStep(
    string Id,
    string Name,
    int Order,
    string? Detail,
    IReadOnlyList<string> HighlightStages);

public sealed record ProcessTransition(string From, string To, string Name);

public sealed record ProcessMap(
    string Id,
    string Name,
    string State,
    IReadOnlyList<ProcessStep> Steps,
    IReadOnlyList<ProcessTransition> Transitions);

public sealed class MetadataCatalog
{
    public MetadataCatalog(
        CompanyProfile company,
        IReadOnlyList<SiteProfile> sites,
        IReadOnlyList<CapabilityProfile> capabilities,
        IReadOnlyList<ProductionLine> lines,
        IReadOnlyList<ToolProfile> tools,
        IReadOnlyList<BusinessRule> rules,
        IReadOnlyList<ProcessMap> processMaps)
    {
        ArgumentNullException.ThrowIfNull(company);
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(processMaps);
        Company = company;
        Sites = sites;
        Capabilities = capabilities;
        Lines = lines;
        Tools = tools;
        Rules = rules;
        ProcessMaps = processMaps;
    }

    public CompanyProfile Company { get; }

    public IReadOnlyList<SiteProfile> Sites { get; }

    public IReadOnlyList<CapabilityProfile> Capabilities { get; }

    public IReadOnlyList<ProductionLine> Lines { get; }

    public IReadOnlyList<ToolProfile> Tools { get; }

    public IReadOnlyList<BusinessRule> Rules { get; }

    public IReadOnlyList<ProcessMap> ProcessMaps { get; }
}
