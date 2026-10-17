// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Application.Metadata;

/// <summary>
/// Plain display names for telemetry parameter ids ("cf4Flow" is "CF4 flow"), from data/metadata/parameters.yaml.
/// The id stays the contract value. The console gets the same list from /api/metadata/catalog.
/// </summary>
public static class ParameterNames
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> Labels = new(() => Load(DataFiles.Find(Path.Combine("metadata", "parameters.yaml"))));

    public static IReadOnlyDictionary<string, string> All => Labels.Value;

    /// <summary>The display name, or the id split into words when it is not in the list.</summary>
    public static string Display(string parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        if (All.TryGetValue(parameter, out var label))
        {
            return label;
        }

        var words = System.Text.RegularExpressions.Regex.Replace(parameter, "([a-z0-9])([A-Z])", "$1 $2").ToLowerInvariant();
        return words.Length == 0 ? parameter : char.ToUpperInvariant(words[0]) + words[1..];
    }

    public static IReadOnlyDictionary<string, string> Load(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            throw new InvalidOperationException("data/metadata/parameters.yaml was not found.");
        }

        var file = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build()
            .Deserialize<ParameterFile>(File.ReadAllText(path)) ?? new ParameterFile();
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in file.Parameters)
        {
            if (string.IsNullOrWhiteSpace(row.Id) || string.IsNullOrWhiteSpace(row.DisplayName) || !labels.TryAdd(row.Id.Trim(), row.DisplayName.Trim()))
            {
                throw new InvalidOperationException($"parameters.yaml: parameter '{row.Id}' needs a unique id and a display name.");
            }
        }

        return labels.Count == 0 ? throw new InvalidOperationException("parameters.yaml lists no parameters.") : labels;
    }

    private sealed class ParameterFile
    {
        public List<ParameterRow> Parameters { get; set; } = [];
    }

    private sealed class ParameterRow
    {
        public string? Id { get; set; }

        public string? DisplayName { get; set; }
    }
}
