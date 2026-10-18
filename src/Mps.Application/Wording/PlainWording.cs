// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Text;

using Mps.Domain;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Application.Wording;

/// <summary>
/// The plain-sentence rule in data/metadata/plain-sentence.yaml. Glossary terms from the allowed-term file are
/// allowed as whole tokens. Any other all-capitals token is rejected. The resolution prompt states this same text.
/// </summary>
public sealed class PlainWording
{
    public const string RelativePath = "metadata/plain-sentence.yaml";

    public const string PromptMarker = "{{plainSentence}}";

    private static readonly Lazy<PlainWording> SharedRule = new(() => Load(null));

    private readonly HashSet<string> _terms;

    private PlainWording(
        int maxLength,
        bool oneSentence,
        bool sentenceCase,
        string allowedTerms,
        IReadOnlyList<string> alphanumericExamples,
        IReadOnlyList<string> hyphenatedExamples,
        IEnumerable<string> terms)
    {
        MaxLength = maxLength;
        OneSentence = oneSentence;
        SentenceCase = sentenceCase;
        AllowedTerms = allowedTerms;
        AlphanumericExamples = alphanumericExamples;
        HyphenatedExamples = hyphenatedExamples;
        _terms = new HashSet<string>(terms, StringComparer.OrdinalIgnoreCase);
        PromptClause = BuildClause(maxLength, oneSentence, sentenceCase, allowedTerms, alphanumericExamples, hyphenatedExamples);
    }

    public static PlainWording Shared => SharedRule.Value;

    public int MaxLength { get; }

    public bool OneSentence { get; }

    public bool SentenceCase { get; }

    /// <summary>Path under data/ of the term list, as written in the rule file.</summary>
    public string AllowedTerms { get; }

    public IReadOnlyList<string> AlphanumericExamples { get; }

    public IReadOnlyList<string> HyphenatedExamples { get; }

    /// <summary>The sentence the prompts must contain. Built from this file, not from a second copy in code.</summary>
    public string PromptClause { get; }

    public static PlainWording Load(string? path)
    {
        path ??= DataFiles.Find(RelativePath);
        if (path is null || !File.Exists(path))
        {
            throw new InvalidOperationException("The plain-sentence rule (data/" + RelativePath + ") was not found.");
        }

        var file = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build()
            .Deserialize<FileShape>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("The plain-sentence rule is empty.");
        if (file.MaxLength <= 0)
        {
            throw new InvalidOperationException("The plain-sentence rule needs a maxLength greater than zero.");
        }

        if (file.OneSentence is null || file.SentenceCase is null)
        {
            throw new InvalidOperationException("The plain-sentence rule needs oneSentence and sentenceCase.");
        }

        var allowed = (file.AllowedTerms ?? string.Empty).Trim().Replace('\\', '/');
        if (allowed.Length == 0 || allowed.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The plain-sentence rule needs an allowedTerms path under data/.");
        }

        var terms = LoadTerms(allowed);
        var alphanumeric = Clean(file.Examples?.Alphanumeric);
        var hyphenated = Clean(file.Examples?.Hyphenated);
        return new PlainWording(file.MaxLength, file.OneSentence.Value, file.SentenceCase.Value, allowed, alphanumeric, hyphenated, terms);
    }

    /// <summary>True when the token is an all-capitals acronym that is not a glossary term or a hyphenated form of one.</summary>
    public bool IsUnknownAcronym(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (token.Length == 0 || _terms.Contains(token))
        {
            return false;
        }

        if (!token.Contains('-', StringComparison.Ordinal))
        {
            return LooksLikeAcronym(token);
        }

        var parts = token.Split('-');
        if (parts.Length < 2 || parts.Any(string.IsNullOrEmpty))
        {
            return LooksLikeAcronym(token);
        }

        foreach (var part in parts)
        {
            if (_terms.Contains(part) || part.All(char.IsDigit))
            {
                continue;
            }

            if (LooksLikeAcronym(part))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when the sentence contains an acronym the allowed-term list does not name.</summary>
    public bool HasUnexplainedAcronym(string sentence)
    {
        ArgumentNullException.ThrowIfNull(sentence);
        var token = new StringBuilder();
        for (var i = 0; i <= sentence.Length; i++)
        {
            if (i < sentence.Length && IsTokenChar(sentence, i, token.Length > 0))
            {
                token.Append(sentence[i]);
                continue;
            }

            if (token.Length == 0)
            {
                continue;
            }

            if (IsUnknownAcronym(token.ToString()))
            {
                return true;
            }

            token.Clear();
        }

        return false;
    }

    private static bool IsTokenChar(string sentence, int index, bool inToken)
    {
        var character = sentence[index];
        if (char.IsLetterOrDigit(character))
        {
            return true;
        }

        return character == '-' && inToken && index + 1 < sentence.Length && char.IsLetterOrDigit(sentence[index + 1]);
    }

    private static bool LooksLikeAcronym(string token)
    {
        var letters = 0;
        foreach (var character in token)
        {
            if (char.IsLetter(character))
            {
                letters++;
                if (!char.IsUpper(character))
                {
                    return false;
                }
            }
            else if (!char.IsDigit(character))
            {
                return false;
            }
        }

        return letters >= 2;
    }

    private static List<string> LoadTerms(string relative)
    {
        var path = DataFiles.Find(relative.Replace('/', Path.DirectorySeparatorChar));
        if (path is null || !File.Exists(path))
        {
            throw new InvalidOperationException("The plain-sentence allowed terms (data/" + relative + ") were not found.");
        }

        var file = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build()
            .Deserialize<TermFile>(File.ReadAllText(path));
        var terms = new List<string>();
        foreach (var row in file?.Terms ?? [])
        {
            var term = (row.Term ?? string.Empty).Trim();
            if (term.Length == 0)
            {
                throw new InvalidOperationException("The plain-sentence allowed terms include a blank term.");
            }

            terms.Add(term);
        }

        if (terms.Count == 0)
        {
            throw new InvalidOperationException("The plain-sentence allowed terms are empty.");
        }

        return terms;
    }

    private static string BuildClause(
        int maxLength,
        bool oneSentence,
        bool sentenceCase,
        string allowedTerms,
        IReadOnlyList<string> alphanumeric,
        IReadOnlyList<string> hyphenated)
    {
        var lead = new StringBuilder();
        if (oneSentence && sentenceCase)
        {
            lead.Append("one sentence in sentence case");
        }
        else if (oneSentence)
        {
            lead.Append("one sentence");
        }
        else if (sentenceCase)
        {
            lead.Append("sentence case");
        }
        else
        {
            lead.Append("plain wording");
        }

        lead.Append(" of at most ").Append(maxLength.ToString(CultureInfo.InvariantCulture)).Append(" characters");
        lead.Append(". A whole token from the plant glossary (").Append(allowedTerms).Append(") is allowed");
        if (alphanumeric.Count > 0 || hyphenated.Count > 0)
        {
            lead.Append(", including");
            if (alphanumeric.Count > 0)
            {
                lead.Append(" an alphanumeric token such as ").Append(JoinOr(alphanumeric));
            }

            if (hyphenated.Count > 0)
            {
                if (alphanumeric.Count > 0)
                {
                    lead.Append(" and");
                }

                lead.Append(" a hyphenated form such as ").Append(JoinOr(hyphenated));
            }
        }

        lead.Append(". Any other acronym is not.");
        return lead.ToString();
    }

    private static string JoinOr(IReadOnlyList<string> values)
    {
        if (values.Count == 1)
        {
            return values[0];
        }

        return string.Join(", ", values.Take(values.Count - 1)) + " or " + values[^1];
    }

    private static string[] Clean(List<string>? values) =>
        values?.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToArray() ?? [];

    private sealed class FileShape
    {
        public int MaxLength { get; set; }

        public bool? OneSentence { get; set; }

        public bool? SentenceCase { get; set; }

        public string? AllowedTerms { get; set; }

        public ExampleShape? Examples { get; set; }
    }

    private sealed class ExampleShape
    {
        public List<string>? Alphanumeric { get; set; }

        public List<string>? Hyphenated { get; set; }
    }

    private sealed class TermFile
    {
        public List<TermRow>? Terms { get; set; }
    }

    private sealed class TermRow
    {
        public string? Term { get; set; }
    }
}
