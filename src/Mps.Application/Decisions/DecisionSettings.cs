// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Application.Decisions;

/// <summary>Human decision settings from data/metadata/decision.yaml, loaded once at startup.</summary>
public sealed class DecisionSettings
{
    private static readonly Lazy<DecisionSettings> SharedSettings = new(() => Load(DataFiles.Find(Path.Combine("metadata", "decision.yaml"))));

    public static DecisionSettings Shared => SharedSettings.Value;

    /// <summary>Shortest override justification accepted, in characters (ADR-005).</summary>
    public int OverrideMinChars { get; set; }

    public static DecisionSettings Load(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            throw new InvalidOperationException("data/metadata/decision.yaml was not found.");
        }

        var settings = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build()
            .Deserialize<DecisionSettings>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("decision.yaml is empty.");
        if (settings.OverrideMinChars <= 0)
        {
            throw new InvalidOperationException("decision.yaml is missing overrideMinChars.");
        }

        return settings;
    }
}
