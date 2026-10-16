// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Explain;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Agents.Resolution;

/// <summary>
/// Makes sure a sufficient recommendation always carries a containment and a preventive action, each labelled with
/// its prefix. Missing ones come from data/metadata/recommended-actions.yaml, filled with the first cited record.
/// </summary>
public static class RecommendedActions
{
    public const string ContainmentPrefix = "Containment: ";

    public const string PreventivePrefix = "Preventive: ";

    private static readonly Lazy<IReadOnlyDictionary<string, Template>> Shared = new(() => Load(WhyItMattersCatalog.FindDataDirectory()));

    public static IReadOnlyList<string> Complete(RootCauseCategory category, IReadOnlyList<string> actions, IReadOnlyList<EvidenceItem> cited, string toolId) =>
        Complete(category, actions, cited, toolId, Shared.Value);

    public static IReadOnlyList<string> Complete(RootCauseCategory category, IReadOnlyList<string> actions, IReadOnlyList<EvidenceItem> cited, string toolId, IReadOnlyDictionary<string, Template> templates)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(cited);
        ArgumentNullException.ThrowIfNull(templates);
        var list = actions.Where(action => !string.IsNullOrWhiteSpace(action)).Select(action => action.Trim()).ToList();
        var record = cited
            .Where(item => item.Source is not (EvidenceSource.Spc or EvidenceSource.Telemetry) && !string.IsNullOrWhiteSpace(item.ExternalRef))
            .Select(item => item.ExternalRef!)
            .FirstOrDefault() ?? "the cited record";
        var key = char.ToLowerInvariant(category.ToString()[0]) + category.ToString()[1..];
        templates.TryGetValue(key, out var template);
        if (!list.Any(action => action.StartsWith(ContainmentPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            var first = list.FirstOrDefault(action => !action.StartsWith(PreventivePrefix, StringComparison.OrdinalIgnoreCase));
            if (first is not null)
            {
                list[list.IndexOf(first)] = ContainmentPrefix + first;
            }
            else if (template?.Containment is { Length: > 0 } containment)
            {
                list.Insert(0, ContainmentPrefix + Fill(containment, record, toolId));
            }
        }

        if (!list.Any(action => action.StartsWith(PreventivePrefix, StringComparison.OrdinalIgnoreCase)))
        {
            var other = list.FirstOrDefault(action => !action.StartsWith(ContainmentPrefix, StringComparison.OrdinalIgnoreCase));
            if (other is not null)
            {
                list[list.IndexOf(other)] = PreventivePrefix + other;
            }
            else if (template?.Preventive is { Length: > 0 } preventive)
            {
                list.Add(PreventivePrefix + Fill(preventive, record, toolId));
            }
        }

        return list;
    }

    public static IReadOnlyDictionary<string, Template> Load(string? dataDirectory)
    {
        var path = dataDirectory is null ? null : Path.Combine(dataDirectory, "metadata", "recommended-actions.yaml");
        if (path is null || !File.Exists(path))
        {
            return new Dictionary<string, Template>();
        }

        var deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build();
        return deserializer.Deserialize<Dictionary<string, Template>>(File.ReadAllText(path)) ?? new Dictionary<string, Template>();
    }

    private static string Fill(string text, string record, string toolId) =>
        text.Replace("{record}", record, StringComparison.Ordinal).Replace("{tool}", toolId, StringComparison.Ordinal);

    public sealed class Template
    {
        public string? Containment { get; set; }

        public string? Preventive { get; set; }
    }
}
