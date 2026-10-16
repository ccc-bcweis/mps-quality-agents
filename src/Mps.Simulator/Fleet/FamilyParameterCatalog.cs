// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Equipment;

namespace Mps.Simulator.Fleet;

/// <summary>
/// Family envelopes from the spec table. A step window must sit inside the envelope and use its unit.
/// Bounds that the spec only names (film thickness, edge-ring height, flows) are fictional demo limits.
/// </summary>
public readonly record struct ParameterLimit(string Unit, double Minimum, double Maximum);

public static class FamilyParameterCatalog
{
    private static readonly Lazy<Dictionary<ToolFamily, IReadOnlyDictionary<string, ParameterLimit>>> Envelopes = new(LoadEnvelopes);

    public static IReadOnlyDictionary<string, ParameterLimit> For(ToolFamily family)
    {
        return Envelopes.Value.TryGetValue(family, out var limits)
            ? limits
            : throw new ArgumentOutOfRangeException(nameof(family), family, "Unknown tool family.");
    }

    public static string ModelFor(ToolFamily family)
    {
        return family switch
        {
            ToolFamily.Deposition => "MPS-D7",
            ToolFamily.Etch => "MPS-E7",
            ToolFamily.Strip => "MPS-S7",
            ToolFamily.Clean => "MPS-C7",
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "Unknown tool family."),
        };
    }

    public static string IdPrefix(ToolFamily family)
    {
        return family switch
        {
            ToolFamily.Deposition => "D",
            ToolFamily.Etch => "E",
            ToolFamily.Strip => "S",
            ToolFamily.Clean => "C",
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "Unknown tool family."),
        };
    }

    public static bool TryParseFamily(string? text, out ToolFamily family)
    {
        switch (text?.Trim())
        {
            case "deposition":
                family = ToolFamily.Deposition;
                return true;
            case "etch":
                family = ToolFamily.Etch;
                return true;
            case "strip":
                family = ToolFamily.Strip;
                return true;
            case "clean":
                family = ToolFamily.Clean;
                return true;
            default:
                family = default;
                return false;
        }
    }

    /// <summary>Reads data/metadata/family-envelopes.yaml, found by walking up from the working and app folders.</summary>
    private static Dictionary<ToolFamily, IReadOnlyDictionary<string, ParameterLimit>> LoadEnvelopes()
    {
        var path = FindEnvelopeFile() ?? throw new FleetSpecException(["data/metadata/family-envelopes.yaml was not found."]);
        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
            .Build();
        var raw = deserializer.Deserialize<Dictionary<string, List<EnvelopeRow>>>(File.ReadAllText(path))
            ?? throw new FleetSpecException(["family-envelopes.yaml is empty."]);
        var result = new Dictionary<ToolFamily, IReadOnlyDictionary<string, ParameterLimit>>();
        foreach (var (name, rows) in raw)
        {
            if (!TryParseFamily(name, out var family))
            {
                throw new FleetSpecException([$"family-envelopes.yaml names unknown family '{name}'."]);
            }

            result[family] = rows.ToDictionary(
                row => row.Parameter ?? throw new FleetSpecException(["An envelope row has no parameter."]),
                row => new ParameterLimit(row.Unit ?? string.Empty, row.Minimum, row.Maximum),
                StringComparer.Ordinal);
        }

        return result;
    }

    private static string? FindEnvelopeFile()
    {
        var configured = Environment.GetEnvironmentVariable("Mps__DataDirectory");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(Path.Combine(configured, "metadata", "family-envelopes.yaml")))
        {
            return Path.Combine(configured, "metadata", "family-envelopes.yaml");
        }

        foreach (var seed in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = string.IsNullOrWhiteSpace(seed) ? null : new DirectoryInfo(seed); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "data", "metadata", "family-envelopes.yaml");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private sealed class EnvelopeRow
    {
        public string? Parameter { get; set; }

        public string? Unit { get; set; }

        public double Minimum { get; set; }

        public double Maximum { get; set; }
    }
}
