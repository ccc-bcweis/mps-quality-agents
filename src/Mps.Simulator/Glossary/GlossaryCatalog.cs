// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Simulator.Glossary;

/// <summary>
/// One plain-language entry. The console tooltip and the glossary page both read this file.
/// </summary>
public sealed record GlossaryEntry
{
    public GlossaryEntry(string term, string expansion, string plainDefinition, string whyItMatters)
    {
        Term = Require(term, nameof(term));
        Expansion = Require(expansion, nameof(expansion));
        PlainDefinition = Require(plainDefinition, nameof(plainDefinition));
        WhyItMatters = Require(whyItMatters, nameof(whyItMatters));
        if (WhyItMatters.Contains('\n', StringComparison.Ordinal) || WhyItMatters.Contains('\r', StringComparison.Ordinal))
        {
            throw new ArgumentException("whyItMatters is one line.", nameof(whyItMatters));
        }
    }

    public string Term { get; }

    public string Expansion { get; }

    public string PlainDefinition { get; }

    public string WhyItMatters { get; }

    private static string Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", name);
        }

        return value.Trim();
    }
}

/// <summary>
/// Loads <c>data/glossary.yaml</c>. Terms are matched exactly, including units such as sccm and mTorr.
/// </summary>
public sealed class GlossaryCatalog
{
    private readonly Dictionary<string, GlossaryEntry> _byTerm;

    public GlossaryCatalog(IReadOnlyList<GlossaryEntry> terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        Terms = terms.ToArray();
        _byTerm = new Dictionary<string, GlossaryEntry>(Terms.Count, StringComparer.Ordinal);
        foreach (var term in Terms)
        {
            if (!_byTerm.TryAdd(term.Term, term))
            {
                throw new GlossaryException($"Duplicate glossary term {term.Term}.");
            }
        }
    }

    public IReadOnlyList<GlossaryEntry> Terms { get; }

    public bool Contains(string term) => _byTerm.ContainsKey(term);

    public static GlossaryCatalog Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new GlossaryException($"Glossary file does not exist: {path}");
        }

        GlossaryFileDto dto;
        try
        {
            dto = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build()
                .Deserialize<GlossaryFileDto>(File.ReadAllText(path)) ?? new GlossaryFileDto();
        }
        catch (Exception exception) when (exception is not GlossaryException)
        {
            throw new GlossaryException($"Glossary file could not be read: {exception.Message}");
        }

        if (dto.Terms is null || dto.Terms.Count == 0)
        {
            throw new GlossaryException("Glossary terms are required.");
        }

        var terms = new List<GlossaryEntry>();
        foreach (var term in dto.Terms)
        {
            try
            {
                terms.Add(new GlossaryEntry(term.Term ?? string.Empty, term.Expansion ?? string.Empty, term.PlainDefinition ?? string.Empty, term.WhyItMatters ?? string.Empty));
            }
            catch (ArgumentException exception)
            {
                throw new GlossaryException($"Glossary term '{term.Term}' is incomplete. {exception.Message}");
            }
        }

        return new GlossaryCatalog(terms);
    }

    private sealed class GlossaryFileDto
    {
        public List<GlossaryTermDto>? Terms { get; set; }
    }

    private sealed class GlossaryTermDto
    {
        public string? Term { get; set; }

        public string? Expansion { get; set; }

        public string? PlainDefinition { get; set; }

        public string? WhyItMatters { get; set; }
    }
}

public sealed class GlossaryException : Exception
{
    public GlossaryException(string message)
        : base(message)
    {
    }
}
