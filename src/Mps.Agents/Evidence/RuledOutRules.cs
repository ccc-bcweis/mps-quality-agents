// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text;
using System.Text.RegularExpressions;

using Mps.Domain;
using Mps.Domain.Hypotheses;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Agents.Evidence;

/// <summary>
/// One rule from data/metadata/ruled-out.yaml. A record of a listed source whose text matches the phrases is ruled out
/// of the class in <see cref="RulesOut"/>. The reason is the template filled with that record's own ids.
/// </summary>
public sealed class RuledOutRule
{
    private static readonly string[] Tokens = ["{ecoId}", "{lotId}", "{noticeId}", "{workOrderId}", "{kind}"];

    private readonly Regex _filled;

    public RuledOutRule(
        string id,
        string description,
        IReadOnlyList<string> sources,
        IReadOnlyList<string> anyOf,
        IReadOnlyList<string> allOf,
        IReadOnlyList<string> noneOf,
        bool matchCase,
        RootCauseCategory rulesOut,
        string reason)
    {
        Id = id;
        Description = description;
        Sources = sources;
        AnyOf = anyOf;
        AllOf = allOf;
        NoneOf = noneOf;
        MatchCase = matchCase;
        RulesOut = rulesOut;
        Reason = reason;
        _filled = new Regex("^" + BuildPattern(reason) + "$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    public string Id { get; }

    public string Description { get; }

    public IReadOnlyList<string> Sources { get; }

    public IReadOnlyList<string> AnyOf { get; }

    public IReadOnlyList<string> AllOf { get; }

    public IReadOnlyList<string> NoneOf { get; }

    public bool MatchCase { get; }

    public RootCauseCategory RulesOut { get; }

    public string Reason { get; }

    /// <summary>True when the source is listed and the text has the phrases, and none of the excluded phrases.</summary>
    public bool Matches(string source, string text)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(text);
        if (!Sources.Contains(source, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var comparison = MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var anyOk = AnyOf.Count == 0 || AnyOf.Any(term => text.Contains(term, comparison));
        var allOk = AllOf.All(term => text.Contains(term, comparison));
        var noneOk = NoneOf.All(term => !text.Contains(term, comparison));
        return anyOk && allOk && noneOk && (AnyOf.Count > 0 || AllOf.Count > 0);
    }

    /// <summary>The reason template with this record's own ids.</summary>
    public string Fill(string? ecoId, string? lotId, string? noticeId, string? workOrderId, string? kind)
    {
        return Reason
            .Replace("{ecoId}", ecoId ?? string.Empty, StringComparison.Ordinal)
            .Replace("{lotId}", lotId ?? string.Empty, StringComparison.Ordinal)
            .Replace("{noticeId}", noticeId ?? string.Empty, StringComparison.Ordinal)
            .Replace("{workOrderId}", workOrderId ?? string.Empty, StringComparison.Ordinal)
            .Replace("{kind}", kind ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>True when the text is this rule's template filled in, including a reason the offline ranker copies.</summary>
    public bool IsFilledReason(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return _filled.IsMatch(text.Trim());
    }

    private static string BuildPattern(string template)
    {
        var builder = new StringBuilder();
        var rest = template;
        while (rest.Length > 0)
        {
            var at = -1;
            var token = string.Empty;
            foreach (var candidate in Tokens)
            {
                var found = rest.IndexOf(candidate, StringComparison.Ordinal);
                if (found >= 0 && (at < 0 || found < at))
                {
                    at = found;
                    token = candidate;
                }
            }

            if (at < 0)
            {
                builder.Append(Regex.Escape(rest));
                break;
            }

            if (at > 0)
            {
                builder.Append(Regex.Escape(rest[..at]));
            }

            builder.Append(".+");
            rest = rest[(at + token.Length)..];
        }

        return builder.ToString();
    }
}

/// <summary>The ruled-out map, from data/metadata/ruled-out.yaml. A bad file fails at startup.</summary>
public sealed class RuledOutRules
{
    public const string RelativePath = "metadata/ruled-out.yaml";

    private static readonly Lazy<RuledOutRules> SharedRules = new(() => Load());

    private static readonly HashSet<string> KnownTokens = new(StringComparer.Ordinal)
    {
        "{ecoId}", "{lotId}", "{noticeId}", "{workOrderId}", "{kind}",
    };

    public RuledOutRules(IReadOnlyList<RuledOutRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Rules = rules.ToArray();
    }

    public static RuledOutRules Shared => SharedRules.Value;

    public IReadOnlyList<RuledOutRule> Rules { get; }

    /// <summary>The first rule that matches this source and text, or null when the record can still support a class.</summary>
    public RuledOutRule? Match(string source, string text)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(text);
        foreach (var rule in Rules)
        {
            if (rule.Matches(source, text))
            {
                return rule;
            }
        }

        return null;
    }

    /// <summary>True when the text is one of the plant templates filled with a record's own ids.</summary>
    public bool IsPlantReason(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        foreach (var rule in Rules)
        {
            if (rule.IsFilledReason(trimmed))
            {
                return true;
            }
        }

        return false;
    }

    public static RuledOutRules Load(string? path = null)
    {
        path ??= DataFiles.Find(RelativePath);
        if (path is null || !File.Exists(path))
        {
            throw new InvalidOperationException("The ruled-out rules (data/" + RelativePath + ") were not found.");
        }

        var file = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build()
            .Deserialize<FileShape>(File.ReadAllText(path));
        var rules = new List<RuledOutRule>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in file?.Rules ?? [])
        {
            var id = string.IsNullOrWhiteSpace(row.Id) ? "(no id)" : row.Id.Trim();
            if (!seen.Add(id))
            {
                throw new InvalidOperationException("Ruled-out rule " + id + " is listed twice.");
            }

            if (string.IsNullOrWhiteSpace(row.RulesOut) || !Enum.TryParse<RootCauseCategory>(row.RulesOut.Trim(), ignoreCase: true, out var rulesOut))
            {
                throw new InvalidOperationException("Ruled-out rule " + id + " rules out \"" + row.RulesOut + "\", which is not a root-cause class.");
            }

            var anyOf = Clean(row.AnyOf);
            var allOf = Clean(row.AllOf);
            var noneOf = Clean(row.NoneOf);
            var sources = Clean(row.Sources);
            if (sources.Length == 0 || (anyOf.Length == 0 && allOf.Length == 0))
            {
                throw new InvalidOperationException("Ruled-out rule " + id + " needs sources and anyOf or allOf terms.");
            }

            var reason = (row.Reason ?? string.Empty).Trim();
            if (reason.Length == 0)
            {
                throw new InvalidOperationException("Ruled-out rule " + id + " needs a reason.");
            }

            foreach (Match token in Regex.Matches(reason, @"\{[^{}]+\}", RegexOptions.CultureInvariant))
            {
                if (!KnownTokens.Contains(token.Value))
                {
                    throw new InvalidOperationException("Ruled-out rule " + id + " has an unknown placeholder " + token.Value + ".");
                }
            }

            rules.Add(new RuledOutRule(id, (row.Description ?? id).Trim(), sources, anyOf, allOf, noneOf, row.MatchCase, rulesOut, reason));
        }

        return new RuledOutRules(rules);
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

        public List<string>? NoneOf { get; set; }

        public bool MatchCase { get; set; }

        public string? RulesOut { get; set; }

        public string? Reason { get; set; }
    }
}
