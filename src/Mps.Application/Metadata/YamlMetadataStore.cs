// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Diagnostics;
using System.Globalization;

using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Application.Metadata;

public sealed class YamlMetadataStore : IMetadataStore
{
    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private readonly Dictionary<string, ToolProfile> _tools;

    public YamlMetadataStore(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Metadata directory is required.", nameof(directory));
        }

        var watch = Stopwatch.StartNew();
        var errors = new List<string>();
        var catalog = Read(directory, errors);
        if (catalog is not null)
        {
            errors.AddRange(MetadataIntegrity.Check(catalog));
        }

        if (errors.Count > 0 || catalog is null)
        {
            throw new MetadataSpecException(errors.Count == 0 ? ["Metadata did not load."] : errors);
        }

        watch.Stop();
        Company = catalog.Company;
        Sites = catalog.Sites;
        Capabilities = catalog.Capabilities;
        Lines = catalog.Lines;
        Tools = catalog.Tools;
        Rules = catalog.Rules.OrderBy(rule => rule.Order).ToArray();
        LoadDuration = watch.Elapsed;
        LoadCount = 1;
        _tools = Tools.ToDictionary(tool => tool.Id, StringComparer.OrdinalIgnoreCase);
    }

    public CompanyProfile Company { get; }

    public IReadOnlyList<SiteProfile> Sites { get; }

    public IReadOnlyList<CapabilityProfile> Capabilities { get; }

    public IReadOnlyList<ProductionLine> Lines { get; }

    public IReadOnlyList<ToolProfile> Tools { get; }

    public IReadOnlyList<BusinessRule> Rules { get; }

    public TimeSpan LoadDuration { get; }

    public int LoadCount { get; }

    public ToolProfile? FindTool(string toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId))
        {
            return null;
        }

        return _tools.TryGetValue(toolId.Trim(), out var tool) ? tool : null;
    }

    public string? ParameterClass(string toolId, string parameterId)
    {
        var tool = FindTool(toolId);
        if (tool is null || string.IsNullOrWhiteSpace(parameterId))
        {
            return null;
        }

        return tool.Parameters
            .FirstOrDefault(parameter => parameter.Id.Equals(parameterId.Trim(), StringComparison.OrdinalIgnoreCase))
            ?.ParameterClass;
    }

    public string? SiteIdForTool(string toolId)
    {
        return FindTool(toolId)?.SiteId;
    }

    private static MetadataCatalog? Read(string directory, List<string> errors)
    {
        if (!Directory.Exists(directory))
        {
            errors.Add("Metadata directory does not exist: " + directory);
            return null;
        }

        var company = Load<CompanyFile>(Path.Combine(directory, "company.yaml"), errors)?.Company;
        var sites = Load<SiteFile>(Path.Combine(directory, "sites.yaml"), errors)?.Sites;
        var capabilities = Load<CapabilityFile>(Path.Combine(directory, "capabilities.yaml"), errors)?.Capabilities;
        var lines = Load<LineFile>(Path.Combine(directory, "lines.yaml"), errors)?.Lines;
        var rules = Load<RuleFile>(Path.Combine(directory, "business-rules.yaml"), errors)?.Rules;
        var toolDtos = LoadTools(directory, errors);
        if (errors.Count > 0 || company is null || sites is null || capabilities is null || lines is null || rules is null)
        {
            return null;
        }

        return new MetadataCatalog(
            MapCompany(company),
            sites.Select(MapSite).ToArray(),
            capabilities.Select(dto => MapCapability(dto, errors)).ToArray(),
            lines.Select(MapLine).ToArray(),
            toolDtos.Select(MapTool).ToArray(),
            rules.Select(MapRule).ToArray());
    }

    private static List<ToolDto> LoadTools(string directory, List<string> errors)
    {
        var folder = Path.Combine(directory, "tools");
        if (!Directory.Exists(folder))
        {
            errors.Add("Metadata tools directory does not exist.");
            return [];
        }

        var tools = new List<ToolDto>();
        foreach (var file in Directory.GetFiles(folder, "*.yaml").OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            var document = Load<ToolFile>(file, errors);
            if (document?.Tools is not null)
            {
                tools.AddRange(document.Tools);
            }
        }

        return tools;
    }

    private static T? Load<T>(string path, List<string> errors)
        where T : class
    {
        if (!File.Exists(path))
        {
            errors.Add("Missing metadata file " + Path.GetFileName(path) + ".");
            return null;
        }

        try
        {
            return Yaml.Deserialize<T>(File.ReadAllText(path));
        }
        catch (Exception exception) when (exception is YamlException or InvalidOperationException)
        {
            errors.Add(Path.GetFileName(path) + ": " + exception.Message);
            return null;
        }
    }

    private static CompanyProfile MapCompany(CompanyDto dto)
    {
        return new CompanyProfile(Text(dto.Id), Text(dto.Name), Text(dto.Purpose), Text(dto.Description), Text(dto.Industry));
    }

    private static SiteProfile MapSite(SiteDto dto)
    {
        return new SiteProfile(Text(dto.Id), Text(dto.Name), Text(dto.Purpose), Text(dto.Description), Text(dto.Timezone));
    }

    private static CapabilityProfile MapCapability(CapabilityDto dto, List<string> errors)
    {
        if (!Enum.TryParse<CapabilityType>(dto.Type, ignoreCase: false, out var type))
        {
            errors.Add("Capability " + dto.Id + " has unknown type " + dto.Type + ".");
            type = CapabilityType.Other;
        }

        return new CapabilityProfile(Text(dto.Id), Text(dto.SiteId), Text(dto.Name), type, Text(dto.Description), dto.Simulated);
    }

    private static ProductionLine MapLine(LineDto dto)
    {
        return new ProductionLine(
            Text(dto.Id),
            Text(dto.CapabilityId),
            Text(dto.Name),
            Text(dto.LineType),
            dto.Sequence,
            Text(dto.Characteristics),
            Texts(dto.IncludesLineIds),
            Texts(dto.ToolIds));
    }

    private static ToolProfile MapTool(ToolDto dto)
    {
        return new ToolProfile(
            Text(dto.Id),
            Text(dto.Name),
            Text(dto.SiteId),
            BlankToNull(dto.LineId),
            BlankToNull(dto.CapabilityId),
            Text(dto.ToolType),
            dto.Simulated,
            (dto.Inputs ?? []).Select(port => new ToolPort(Text(port.Name), Text(port.Description))).ToArray(),
            (dto.Outputs ?? []).Select(port => new ToolPort(Text(port.Name), Text(port.Description))).ToArray(),
            (dto.Parameters ?? []).Select(MapParameter).ToArray());
    }

    private static ToolParameter MapParameter(ParameterDto dto)
    {
        return new ToolParameter(
            Text(dto.Id),
            Text(dto.Name),
            Text(dto.ParameterClass),
            Text(dto.Unit),
            dto.Telemetry,
            Number(dto.Lcl),
            Number(dto.Ucl),
            Number(dto.AlertLow),
            Number(dto.AlertHigh),
            Number(dto.Nominal));
    }

    private static BusinessRule MapRule(RuleDto dto)
    {
        var when = dto.When ?? new WhenDto();
        return new BusinessRule(
            Text(dto.Id),
            Text(dto.Name),
            dto.Order,
            Text(dto.WhenPlain),
            Text(dto.ThenPlain),
            Text(dto.Why),
            new RuleWhen
            {
                Severity = BlankToNull(when.Severity),
                SeverityAtLeast = BlankToNull(when.SeverityAtLeast),
                SeverityAtMost = BlankToNull(when.SeverityAtMost),
                AlertRuleId = BlankToNull(when.AlertRuleId),
                ParameterClass = BlankToNull(when.ParameterClass),
                ParameterId = BlankToNull(when.ParameterId),
                ToolId = BlankToNull(when.ToolId),
                SiteId = BlankToNull(when.SiteId),
                RepeatAtLeast = when.RepeatAtLeast,
                ConfidenceBelow = when.ConfidenceBelow,
                EvidenceSufficiency = BlankToNull(when.EvidenceSufficiency),
                Channel = BlankToNull(when.Channel),
                RequiresAlert = when.RequiresAlert,
            },
            (dto.Actions ?? []).Select(action => new RuleAction(Text(action.Type), BlankToNull(action.Priority), BlankToNull(action.Queue))).ToArray());
    }

    private static string Text(string? value) => value?.Trim() ?? string.Empty;

    private static string? BlankToNull(string? value)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static string[] Texts(List<string>? values)
    {
        return values is null ? [] : values.Select(Text).ToArray();
    }

    private static double Number(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        return double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private sealed class CompanyFile
    {
        public CompanyDto? Company { get; set; }
    }

    private sealed class CompanyDto
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Purpose { get; set; }

        public string? Description { get; set; }

        public string? Industry { get; set; }
    }

    private sealed class SiteFile
    {
        public List<SiteDto>? Sites { get; set; }
    }

    private sealed class SiteDto
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Purpose { get; set; }

        public string? Description { get; set; }

        public string? Timezone { get; set; }
    }

    private sealed class CapabilityFile
    {
        public List<CapabilityDto>? Capabilities { get; set; }
    }

    private sealed class CapabilityDto
    {
        public string? Id { get; set; }

        public string? SiteId { get; set; }

        public string? Name { get; set; }

        public string? Type { get; set; }

        public string? Description { get; set; }

        public bool Simulated { get; set; }
    }

    private sealed class LineFile
    {
        public List<LineDto>? Lines { get; set; }
    }

    private sealed class LineDto
    {
        public string? Id { get; set; }

        public string? CapabilityId { get; set; }

        public string? Name { get; set; }

        public string? LineType { get; set; }

        public int Sequence { get; set; }

        public string? Characteristics { get; set; }

        public List<string>? IncludesLineIds { get; set; }

        public List<string>? ToolIds { get; set; }
    }

    private sealed class ToolFile
    {
        public List<ToolDto>? Tools { get; set; }
    }

    private sealed class ToolDto
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? SiteId { get; set; }

        public string? LineId { get; set; }

        public string? CapabilityId { get; set; }

        public string? ToolType { get; set; }

        public bool Simulated { get; set; }

        public List<PortDto>? Inputs { get; set; }

        public List<PortDto>? Outputs { get; set; }

        public List<ParameterDto>? Parameters { get; set; }
    }

    private sealed class PortDto
    {
        public string? Name { get; set; }

        public string? Description { get; set; }
    }

    private sealed class ParameterDto
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? ParameterClass { get; set; }

        public string? Unit { get; set; }

        public bool Telemetry { get; set; }

        public string? Lcl { get; set; }

        public string? Ucl { get; set; }

        public string? AlertLow { get; set; }

        public string? AlertHigh { get; set; }

        public string? Nominal { get; set; }
    }

    private sealed class RuleFile
    {
        public List<RuleDto>? Rules { get; set; }
    }

    private sealed class RuleDto
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public int Order { get; set; }

        public string? WhenPlain { get; set; }

        public string? ThenPlain { get; set; }

        public string? Why { get; set; }

        public WhenDto? When { get; set; }

        public List<ActionDto>? Actions { get; set; }
    }

    private sealed class WhenDto
    {
        public string? Severity { get; set; }

        public string? SeverityAtLeast { get; set; }

        public string? SeverityAtMost { get; set; }

        public string? AlertRuleId { get; set; }

        public string? ParameterClass { get; set; }

        public string? ParameterId { get; set; }

        public string? ToolId { get; set; }

        public string? SiteId { get; set; }

        public int? RepeatAtLeast { get; set; }

        public double? ConfidenceBelow { get; set; }

        public string? EvidenceSufficiency { get; set; }

        public string? Channel { get; set; }

        public bool? RequiresAlert { get; set; }
    }

    private sealed class ActionDto
    {
        public string? Type { get; set; }

        public string? Priority { get; set; }

        public string? Queue { get; set; }
    }
}
