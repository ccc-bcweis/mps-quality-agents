// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Ranking;
using Mps.Application.Spc;
using Mps.Domain.Hypotheses;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Agents.Grounding;

/// <summary>One rule from data/metadata/category-evidence.yaml: a cited record of this kind implies this root-cause class.</summary>
public sealed record CategoryEvidenceRule(
    string Id,
    string Description,
    IReadOnlyList<string> Sources,
    IReadOnlyList<string> AnyOf,
    IReadOnlyList<string> AllOf,
    bool MatchCase,
    RootCauseCategory Implies)
{
    /// <summary>True when the note's source is listed and its title plus detail carries the terms.</summary>
    public bool Matches(EvidenceNote note)
    {
        ArgumentNullException.ThrowIfNull(note);
        if (!Sources.Contains(note.Source, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var text = note.Title + " " + note.Detail;
        var comparison = MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return (AnyOf.Count == 0 || AnyOf.Any(term => text.Contains(term, comparison)))
            && AllOf.All(term => text.Contains(term, comparison))
            && (AnyOf.Count > 0 || AllOf.Count > 0);
    }
}

/// <summary>The cited-record to root-cause class map, from data/metadata/category-evidence.yaml.</summary>
public sealed class CategoryEvidenceRules
{
    public const string RelativePath = "metadata/category-evidence.yaml";

    private static readonly Lazy<CategoryEvidenceRules> SharedRules = new(() => Load());

    public CategoryEvidenceRules(IReadOnlyList<CategoryEvidenceRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Rules = rules.ToArray();
    }

    public static CategoryEvidenceRules Shared => SharedRules.Value;

    public IReadOnlyList<CategoryEvidenceRule> Rules { get; }

    public static CategoryEvidenceRules Load(string? path = null)
    {
        path ??= DataFiles.Find(RelativePath);
        if (path is null || !File.Exists(path))
        {
            throw new InvalidOperationException("The category evidence map (data/" + RelativePath + ") was not found.");
        }

        var file = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build()
            .Deserialize<FileShape>(File.ReadAllText(path));
        var rules = new List<CategoryEvidenceRule>();
        foreach (var row in file?.Rules ?? [])
        {
            var id = string.IsNullOrWhiteSpace(row.Id) ? "(no id)" : row.Id.Trim();
            if (string.IsNullOrWhiteSpace(row.Implies) || !Enum.TryParse<RootCauseCategory>(row.Implies.Trim(), ignoreCase: true, out var implies))
            {
                throw new InvalidOperationException("Category evidence rule " + id + " implies \"" + row.Implies + "\", which is not a root-cause class.");
            }

            var anyOf = Clean(row.AnyOf);
            var allOf = Clean(row.AllOf);
            var sources = Clean(row.Sources);
            if (sources.Length == 0 || (anyOf.Length == 0 && allOf.Length == 0))
            {
                throw new InvalidOperationException("Category evidence rule " + id + " needs sources and anyOf or allOf terms.");
            }

            rules.Add(new CategoryEvidenceRule(id, (row.Description ?? id).Trim(), sources, anyOf, allOf, row.MatchCase, implies));
        }

        return new CategoryEvidenceRules(rules);
    }

    private static string[] Clean(List<string>? values) =>
        values?.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToArray() ?? [];

    private sealed class FileShape
    {
        public List<RowShape>? Rules { get; set; }
    }

    private sealed class RowShape
    {
        public string? Id { get; set; }

        public string? Description { get; set; }

        public List<string>? Sources { get; set; }

        public List<string>? AnyOf { get; set; }

        public List<string>? AllOf { get; set; }

        public bool MatchCase { get; set; }

        public string? Implies { get; set; }
    }
}
