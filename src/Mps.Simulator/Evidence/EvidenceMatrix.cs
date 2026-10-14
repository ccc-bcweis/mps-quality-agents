// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Simulator.Faults;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Simulator.Evidence;

/// <summary>
/// One plain-language row. <see cref="Supports"/> and <see cref="RulesOut"/> name root-cause classes and do not overlap.
/// </summary>
public sealed record CauseSignal(string Id, string Plain, string WhyItMatters, IReadOnlyList<string> Supports, IReadOnlyList<string> RulesOut);

/// <summary>
/// One root-cause class and the one-line reason a supervisor should care which signals point at it.
/// </summary>
public sealed record CauseClass(string Category, string WhyItMatters);

/// <summary>
/// The root-cause class to signal matrix in <c>data/evidence-matrix.yaml</c>.
/// </summary>
public sealed class EvidenceMatrix
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public EvidenceMatrix(IReadOnlyList<CauseClass> classes, IReadOnlyList<CauseSignal> signals)
    {
        ArgumentNullException.ThrowIfNull(classes);
        ArgumentNullException.ThrowIfNull(signals);
        Classes = classes.ToArray();
        Signals = signals.ToArray();
        var errors = new List<string>();
        Validate(Classes, Signals, errors);
        if (errors.Count > 0)
        {
            throw new EvidenceMatrixException(errors);
        }
    }

    public IReadOnlyList<CauseClass> Classes { get; }

    public IReadOnlyList<CauseSignal> Signals { get; }

    public IReadOnlyList<CauseSignal> Supporting(string category)
    {
        return Signals.Where(signal => signal.Supports.Contains(category, StringComparer.Ordinal)).ToArray();
    }

    public IReadOnlyList<CauseSignal> RulingOut(string category)
    {
        return Signals.Where(signal => signal.RulesOut.Contains(category, StringComparer.Ordinal)).ToArray();
    }

    public static EvidenceMatrix Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new EvidenceMatrixException([$"Evidence matrix does not exist: {path}"]);
        }

        MatrixFileDto dto;
        try
        {
            dto = Deserializer.Deserialize<MatrixFileDto>(File.ReadAllText(path)) ?? new MatrixFileDto();
        }
        catch (Exception exception) when (exception is not EvidenceMatrixException)
        {
            throw new EvidenceMatrixException([$"Evidence matrix could not be read: {exception.Message}"]);
        }

        var errors = new List<string>();
        var classes = new List<CauseClass>();
        foreach (var item in dto.Classes ?? [])
        {
            var category = item.Category?.Trim() ?? string.Empty;
            var why = OneLine(item.WhyItMatters, $"class {category}", errors);
            if (category.Length == 0 || why is null)
            {
                errors.Add("Each class needs a category and a one-line whyItMatters.");
                continue;
            }

            classes.Add(new CauseClass(category, why));
        }

        var signals = new List<CauseSignal>();
        foreach (var item in dto.Signals ?? [])
        {
            var id = item.Id?.Trim() ?? string.Empty;
            var plain = OneLine(item.Plain, $"signal {id} plain", errors);
            var why = OneLine(item.WhyItMatters, $"signal {id} whyItMatters", errors);
            var supports = Clean(item.Supports);
            var rulesOut = Clean(item.RulesOut);
            if (id.Length == 0 || plain is null || why is null)
            {
                errors.Add($"Signal '{id}' needs an id, a plain line, and a whyItMatters line.");
                continue;
            }

            signals.Add(new CauseSignal(id, plain, why, supports, rulesOut));
        }

        if (errors.Count > 0)
        {
            throw new EvidenceMatrixException(errors);
        }

        return new EvidenceMatrix(classes, signals);
    }

    private static void Validate(IReadOnlyList<CauseClass> classes, IReadOnlyList<CauseSignal> signals, List<string> errors)
    {
        var categories = classes.Select(item => item.Category).ToArray();
        if (categories.Distinct(StringComparer.Ordinal).Count() != categories.Length)
        {
            errors.Add("Root-cause classes must be unique.");
        }

        if (!categories.Order(StringComparer.Ordinal).SequenceEqual(FaultCategories.All.Order(StringComparer.Ordinal)))
        {
            errors.Add("The matrix must name the four root-cause classes and no others.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var signal in signals)
        {
            if (!seen.Add(signal.Id))
            {
                errors.Add($"Duplicate signal id {signal.Id}.");
            }

            if (signal.Supports.Count == 0 || signal.RulesOut.Count == 0)
            {
                errors.Add($"Signal {signal.Id} needs both supports and rulesOut.");
            }

            if (signal.Supports.Intersect(signal.RulesOut, StringComparer.Ordinal).Any())
            {
                errors.Add($"Signal {signal.Id} supports and rules out the same class.");
            }

            foreach (var category in signal.Supports.Concat(signal.RulesOut))
            {
                if (!FaultCategories.All.Contains(category, StringComparer.Ordinal))
                {
                    errors.Add($"Signal {signal.Id} names an unknown class {category}.");
                }
            }
        }

        foreach (var category in FaultCategories.All)
        {
            var supportCount = signals.Count(signal => signal.Supports.Contains(category, StringComparer.Ordinal));
            var ruledOut = signals.Any(signal => signal.RulesOut.Contains(category, StringComparer.Ordinal));
            if (supportCount < 2)
            {
                errors.Add($"Class {category} needs at least two supporting signals.");
            }

            if (!ruledOut)
            {
                errors.Add($"Class {category} needs at least one signal that rules it out.");
            }
        }
    }

    private static string? OneLine(string? value, string name, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{name} is required.");
            return null;
        }

        var text = value.Trim();
        if (text.Contains('\n', StringComparison.Ordinal) || text.Contains('\r', StringComparison.Ordinal) || text.Length > 220)
        {
            errors.Add($"{name} must be one line of at most 220 characters.");
            return null;
        }

        foreach (var token in FaultPlainLanguage.HiddenLabels)
        {
            if (text.Contains(token, StringComparison.Ordinal))
            {
                errors.Add($"{name} must stay in plain language.");
                return null;
            }
        }

        return text;
    }

    private static string[] Clean(List<string>? values)
    {
        return values?.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).ToArray() ?? [];
    }

    private sealed class MatrixFileDto
    {
        public List<ClassDto>? Classes { get; set; }

        public List<SignalDto>? Signals { get; set; }
    }

    private sealed class ClassDto
    {
        public string? Category { get; set; }

        public string? WhyItMatters { get; set; }
    }

    private sealed class SignalDto
    {
        public string? Id { get; set; }

        public string? Plain { get; set; }

        public string? WhyItMatters { get; set; }

        public List<string>? Supports { get; set; }

        public List<string>? RulesOut { get; set; }
    }
}

public sealed class EvidenceMatrixException : Exception
{
    public EvidenceMatrixException(IReadOnlyList<string> errors)
        : base(string.Join(Environment.NewLine, errors ?? throw new ArgumentNullException(nameof(errors))))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
