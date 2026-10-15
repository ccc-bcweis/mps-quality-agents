// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Incidents;

namespace Mps.Application.Metadata;

public static class MetadataIntegrity
{
    public const double Epsilon = 1e-9;

    private static readonly string[] LineTypeOrder =
    [
        "photoresist-strip",
        "clean",
        "thin-film-deposition",
        "wafer-etch",
    ];

    private static readonly string[] Severities = ["low", "medium", "high", "critical"];

    private static readonly string[] EvidenceWords = ["sufficient", "insufficient", "contradictory"];

    private static readonly string[] Priorities = ["low", "medium", "high", "critical"];

    private static readonly HashSet<string> ActionTypes = new(StringComparer.Ordinal)
    {
        "open-ticket",
        "set-priority",
        "route-to-queue",
        "escalate-to-human",
        "auto-investigate",
    };

    public static IReadOnlyList<string> Check(MetadataCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var errors = new List<string>();
        CheckCompany(catalog.Company, errors);
        var sites = Index(catalog.Sites.Select(site => site.Id), "site", errors);
        CheckSites(catalog.Sites, errors);
        var capabilities = Index(catalog.Capabilities.Select(item => item.Id), "capability", errors);
        CheckCapabilities(catalog.Capabilities, sites, errors);
        var lines = Index(catalog.Lines.Select(line => line.Id), "line", errors);
        CheckLines(catalog, capabilities, errors);
        var tools = Index(catalog.Tools.Select(tool => tool.Id), "tool", errors);
        CheckTools(catalog, lines, capabilities, errors);
        CheckRules(catalog, sites, tools, errors);
        CheckMaps(catalog.ProcessMaps, errors);
        return errors;
    }

    private static void CheckCompany(CompanyProfile company, List<string> errors)
    {
        Require(company.Id, "Company id", errors);
        Require(company.Name, "Company name", errors);
        Require(company.Purpose, "Company purpose", errors);
        Require(company.Description, "Company description", errors);
        Require(company.Industry, "Company industry", errors);
    }

    private static void CheckSites(IReadOnlyList<SiteProfile> sites, List<string> errors)
    {
        if (sites.Count == 0)
        {
            errors.Add("At least one site is required.");
        }

        foreach (var site in sites)
        {
            Require(site.Id, "Site id", errors);
            Require(site.Name, "Site " + site.Id + " name", errors);
            Require(site.Purpose, "Site " + site.Id + " purpose", errors);
            Require(site.Description, "Site " + site.Id + " description", errors);
            Require(site.Timezone, "Site " + site.Id + " timezone", errors);
        }
    }

    private static void CheckCapabilities(
        IReadOnlyList<CapabilityProfile> capabilities,
        HashSet<string> sites,
        List<string> errors)
    {
        foreach (var capability in capabilities)
        {
            if (!sites.Contains(capability.SiteId))
            {
                errors.Add("Capability " + capability.Id + " site " + capability.SiteId + " does not exist.");
            }

            if (!Enum.IsDefined(capability.Type))
            {
                errors.Add("Capability " + capability.Id + " has an unknown type.");
            }

            if (capability.Simulated && capability.Type != CapabilityType.Manufacturing)
            {
                errors.Add("Capability " + capability.Id + " is simulated but is not manufacturing.");
            }

            Require(capability.Name, "Capability " + capability.Id + " name", errors);
            Require(capability.Description, "Capability " + capability.Id + " description", errors);
        }
    }

    private static void CheckLines(MetadataCatalog catalog, HashSet<string> capabilities, List<string> errors)
    {
        var byId = catalog.Capabilities.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var line in catalog.Lines)
        {
            Require(line.Name, "Line " + line.Id + " name", errors);
            Require(line.Characteristics, "Line " + line.Id + " characteristics", errors);
            if (!capabilities.Contains(line.CapabilityId) || !byId.TryGetValue(line.CapabilityId, out var capability))
            {
                errors.Add("Line " + line.Id + " capability " + line.CapabilityId + " does not exist.");
                continue;
            }

            if (capability.Type != CapabilityType.Manufacturing || !capability.Simulated)
            {
                errors.Add("Line " + line.Id + " must belong to a simulated manufacturing capability.");
            }

            if (string.Equals(line.LineType, "end-to-end", StringComparison.Ordinal))
            {
                CheckEndToEnd(catalog, line, errors);
            }
            else
            {
                CheckStepLine(line, errors);
            }
        }
    }

    private static void CheckEndToEnd(MetadataCatalog catalog, ProductionLine line, List<string> errors)
    {
        if (line.Sequence != 0)
        {
            errors.Add("Line " + line.Id + " is end to end and must be sequence 0.");
        }

        if (line.ToolIds.Count > 0)
        {
            errors.Add("Line " + line.Id + " is end to end and cannot list tools.");
        }

        if (line.IncludesLineIds.Count != LineTypeOrder.Length)
        {
            errors.Add("Line " + line.Id + " must include photoresist strip, clean, deposition, and etch.");
            return;
        }

        var byId = catalog.Lines.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < LineTypeOrder.Length; index++)
        {
            var childId = line.IncludesLineIds[index];
            if (!byId.TryGetValue(childId, out var child))
            {
                errors.Add("Line " + line.Id + " includes missing line " + childId + ".");
                continue;
            }

            if (!string.Equals(child.CapabilityId, line.CapabilityId, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Line " + childId + " is not in the same capability as " + line.Id + ".");
            }

            if (!string.Equals(child.LineType, LineTypeOrder[index], StringComparison.Ordinal))
            {
                errors.Add("Line " + line.Id + " step " + (index + 1) + " must be " + LineTypeOrder[index] + ".");
            }

            if (string.Equals(child.LineType, "end-to-end", StringComparison.Ordinal))
            {
                errors.Add("Line " + line.Id + " cannot include another end to end line.");
            }
        }
    }

    private static void CheckStepLine(ProductionLine line, List<string> errors)
    {
        var index = Array.IndexOf(LineTypeOrder, line.LineType);
        if (index < 0)
        {
            errors.Add("Line " + line.Id + " has unknown type " + line.LineType + ".");
            return;
        }

        if (line.Sequence != index + 1)
        {
            errors.Add("Line " + line.Id + " sequence does not match its process order.");
        }

        if (line.IncludesLineIds.Count > 0)
        {
            errors.Add("Line " + line.Id + " is a step line and cannot include other lines.");
        }
    }

    private static void CheckTools(
        MetadataCatalog catalog,
        HashSet<string> lines,
        HashSet<string> capabilities,
        List<string> errors)
    {
        var lineById = catalog.Lines.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var capabilityById = catalog.Capabilities.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var toolsOnLine = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var tool in catalog.Tools)
        {
            Require(tool.Name, "Tool " + tool.Id + " name", errors);
            Require(tool.ToolType, "Tool " + tool.Id + " type", errors);
            var hasLine = !string.IsNullOrWhiteSpace(tool.LineId);
            var hasCapability = !string.IsNullOrWhiteSpace(tool.CapabilityId);
            if (hasLine == hasCapability)
            {
                errors.Add("Tool " + tool.Id + " must attach to a line or a manufacturing capability, not both and not neither.");
            }

            if (hasLine)
            {
                if (!lines.Contains(tool.LineId!) || !lineById.TryGetValue(tool.LineId!, out var line))
                {
                    errors.Add("Tool " + tool.Id + " line " + tool.LineId + " does not exist.");
                }
                else
                {
                    if (!capabilityById.TryGetValue(line.CapabilityId, out var capability)
                        || !string.Equals(capability.SiteId, tool.SiteId, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add("Tool " + tool.Id + " site does not match its line.");
                    }

                    if (!toolsOnLine.TryGetValue(line.Id, out var listed))
                    {
                        listed = [];
                        toolsOnLine[line.Id] = listed;
                    }

                    listed.Add(tool.Id);
                }
            }

            if (hasCapability)
            {
                if (!capabilities.Contains(tool.CapabilityId!) || !capabilityById.TryGetValue(tool.CapabilityId!, out var capability))
                {
                    errors.Add("Tool " + tool.Id + " capability " + tool.CapabilityId + " does not exist.");
                }
                else if (capability.Type != CapabilityType.Manufacturing)
                {
                    errors.Add("Tool " + tool.Id + " must attach to a manufacturing capability.");
                }
                else if (!string.Equals(capability.SiteId, tool.SiteId, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Tool " + tool.Id + " site does not match its capability.");
                }
            }

            if (tool.Inputs.Count == 0 || tool.Outputs.Count == 0)
            {
                errors.Add("Tool " + tool.Id + " needs inputs and outputs.");
            }

            CheckParameters(tool, errors);
        }

        foreach (var line in catalog.Lines)
        {
            var actual = toolsOnLine.TryGetValue(line.Id, out var found)
                ? found.ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var expected = line.ToolIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!actual.SetEquals(expected))
            {
                errors.Add("Line " + line.Id + " tools do not match the tools that point at it.");
            }

            foreach (var toolId in line.ToolIds)
            {
                if (!catalog.Tools.Any(tool => tool.Id.Equals(toolId, StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add("Line " + line.Id + " lists missing tool " + toolId + ".");
                }
            }
        }
    }

    private static void CheckParameters(ToolProfile tool, List<string> errors)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (tool.Parameters.Count == 0)
        {
            errors.Add("Tool " + tool.Id + " needs parameters.");
        }

        foreach (var parameter in tool.Parameters)
        {
            if (!seen.Add(parameter.Id))
            {
                errors.Add("Tool " + tool.Id + " repeats parameter " + parameter.Id + ".");
            }

            Require(parameter.Name, "Parameter " + parameter.Id + " name", errors);
            Require(parameter.ParameterClass, "Parameter " + parameter.Id + " class", errors);
            Require(parameter.Unit, "Parameter " + parameter.Id + " unit", errors);
            if (!parameter.Telemetry)
            {
                continue;
            }

            if (parameter.Lcl >= parameter.Ucl - Epsilon)
            {
                errors.Add("Parameter " + tool.Id + " " + parameter.Id + " lower limit must be below the upper limit.");
            }

            if (Outside(parameter.AlertLow, parameter.Lcl, parameter.Ucl)
                || Outside(parameter.AlertHigh, parameter.Lcl, parameter.Ucl)
                || parameter.AlertLow > parameter.AlertHigh + Epsilon)
            {
                errors.Add("Parameter " + tool.Id + " " + parameter.Id + " alert thresholds must sit inside the limits.");
            }

            if (Outside(parameter.Nominal, parameter.Lcl, parameter.Ucl))
            {
                errors.Add("Parameter " + tool.Id + " " + parameter.Id + " nominal must sit inside the limits.");
            }
        }
    }

    private static void CheckRules(
        MetadataCatalog catalog,
        HashSet<string> sites,
        HashSet<string> tools,
        List<string> errors)
    {
        Index(catalog.Rules.Select(rule => rule.Id), "rule", errors);
        var orders = new HashSet<int>();
        var classes = catalog.Tools
            .SelectMany(tool => tool.Parameters)
            .Select(parameter => parameter.ParameterClass)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var highest = catalog.Rules.Count == 0 ? 0 : catalog.Rules.Max(rule => rule.Order);
        foreach (var rule in catalog.Rules)
        {
            Require(rule.Name, "Rule " + rule.Id + " name", errors);
            Require(rule.WhenPlain, "Rule " + rule.Id + " when", errors);
            Require(rule.ThenPlain, "Rule " + rule.Id + " then", errors);
            Require(rule.Why, "Rule " + rule.Id + " why", errors);
            if (!orders.Add(rule.Order))
            {
                errors.Add("Rule order " + rule.Order + " is repeated.");
            }

            if (rule.When.IsEmpty() && rule.Order != highest)
            {
                errors.Add("Rule " + rule.Id + " has an empty condition before the last rule.");
            }

            CheckWhen(rule, catalog, sites, tools, classes, errors);
            CheckActions(rule, errors);
        }
    }

    private static void CheckWhen(
        BusinessRule rule,
        MetadataCatalog catalog,
        HashSet<string> sites,
        HashSet<string> tools,
        HashSet<string> classes,
        List<string> errors)
    {
        var when = rule.When;
        Known(when.Severity, Severities, "Rule " + rule.Id + " severity", errors);
        Known(when.SeverityAtLeast, Severities, "Rule " + rule.Id + " severity", errors);
        Known(when.SeverityAtMost, Severities, "Rule " + rule.Id + " severity", errors);
        if (!string.IsNullOrWhiteSpace(when.EvidenceSufficiency)
            && !EvidenceWords.Contains(when.EvidenceSufficiency, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("Rule " + rule.Id + " has an unknown evidence word.");
        }

        if (!string.IsNullOrWhiteSpace(when.Channel)
            && !Enum.TryParse<IncidentChannel>(when.Channel, ignoreCase: true, out _))
        {
            errors.Add("Rule " + rule.Id + " has an unknown channel.");
        }

        if (!string.IsNullOrWhiteSpace(when.SiteId) && !sites.Contains(when.SiteId))
        {
            errors.Add("Rule " + rule.Id + " site " + when.SiteId + " does not exist.");
        }

        if (!string.IsNullOrWhiteSpace(when.ToolId) && !tools.Contains(when.ToolId))
        {
            errors.Add("Rule " + rule.Id + " tool " + when.ToolId + " does not exist.");
        }

        if (!string.IsNullOrWhiteSpace(when.ParameterClass) && !classes.Contains(when.ParameterClass))
        {
            errors.Add("Rule " + rule.Id + " parameter class " + when.ParameterClass + " is unused.");
        }

        if (!string.IsNullOrWhiteSpace(when.ParameterId))
        {
            var holders = catalog.Tools.Where(tool =>
                tool.Parameters.Any(parameter => parameter.Id.Equals(when.ParameterId, StringComparison.OrdinalIgnoreCase)));
            if (!string.IsNullOrWhiteSpace(when.ToolId))
            {
                holders = holders.Where(tool => tool.Id.Equals(when.ToolId, StringComparison.OrdinalIgnoreCase));
            }

            var matches = holders.ToArray();
            if (matches.Length == 0)
            {
                errors.Add("Rule " + rule.Id + " parameter " + when.ParameterId + " does not exist.");
            }
            else if (!string.IsNullOrWhiteSpace(when.ParameterClass)
                && !matches.Any(tool => tool.Parameters.Any(parameter =>
                    parameter.Id.Equals(when.ParameterId, StringComparison.OrdinalIgnoreCase)
                    && parameter.ParameterClass.Equals(when.ParameterClass, StringComparison.OrdinalIgnoreCase))))
            {
                errors.Add("Rule " + rule.Id + " parameter class does not match " + when.ParameterId + ".");
            }
        }
    }

    private static void CheckActions(BusinessRule rule, List<string> errors)
    {
        foreach (var action in rule.Actions)
        {
            if (action.Type.Contains("capa", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Rule " + rule.Id + " cannot create a corrective action.");
            }

            if (!ActionTypes.Contains(action.Type))
            {
                errors.Add("Rule " + rule.Id + " has unknown action " + action.Type + ".");
            }

            if (string.Equals(action.Type, "set-priority", StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(action.Priority)
                    || !Priorities.Contains(action.Priority, StringComparer.OrdinalIgnoreCase)))
            {
                errors.Add("Rule " + rule.Id + " set-priority needs a priority.");
            }

            if (string.Equals(action.Type, "route-to-queue", StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(action.Queue))
            {
                errors.Add("Rule " + rule.Id + " route-to-queue needs a queue.");
            }
        }
    }

    private static void CheckMaps(IReadOnlyList<ProcessMap> maps, List<string> errors)
    {
        Index(maps.Select(map => map.Id), "process map", errors);
        if (maps.Count(map => string.Equals(map.State, "current", StringComparison.Ordinal)) != 1
            || maps.Count(map => string.Equals(map.State, "future", StringComparison.Ordinal)) != 1)
        {
            errors.Add("Process maps need exactly one current state and one future state.");
        }

        foreach (var map in maps)
        {
            if (!string.Equals(map.State, "current", StringComparison.Ordinal)
                && !string.Equals(map.State, "future", StringComparison.Ordinal))
            {
                errors.Add("Process map " + map.Id + " state must be current or future.");
            }

            var steps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var step in map.Steps)
            {
                if (!steps.Add(step.Id))
                {
                    errors.Add("Process map " + map.Id + " repeats step " + step.Id + ".");
                }

                Require(step.Name, "Step " + step.Id + " name", errors);
                foreach (var stage in step.HighlightStages)
                {
                    if (!Enum.TryParse<IncidentStage>(stage, ignoreCase: false, out _))
                    {
                        errors.Add("Step " + step.Id + " cites unknown stage " + stage + ".");
                    }
                }
            }

            foreach (var transition in map.Transitions)
            {
                if (!steps.Contains(transition.From) || !steps.Contains(transition.To))
                {
                    errors.Add("Process map " + map.Id + " transition " + transition.From + " to " + transition.To + " cites a missing step.");
                }

                Require(transition.Name, "Transition name", errors);
            }
        }
    }

    private static HashSet<string> Index(IEnumerable<string> ids, string kind, List<string> errors)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add("A " + kind + " is missing an id.");
                continue;
            }

            if (!set.Add(id))
            {
                errors.Add("Duplicate " + kind + " id " + id + ".");
            }
        }

        return set;
    }

    private static void Require(string? value, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(label + " is required.");
        }
    }

    private static void Known(string? value, string[] allowed, string label, List<string> errors)
    {
        if (value is null)
        {
            return;
        }

        if (!allowed.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(label + " is not a known value.");
        }
    }

    private static bool Outside(double value, double low, double high)
    {
        return value < low - Epsilon || value > high + Epsilon;
    }
}
