// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Spc;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Application.Decisions;

/// <summary>The evidence-loop cap: the most rounds one ticket can have. From data/metadata/investigation.yaml (evidenceRounds.max).</summary>
public sealed record EvidenceRoundSettings(int MaxRounds)
{
    /// <summary>Used when the file or the setting is missing.</summary>
    public const int DefaultMaxRounds = 5;

    private static readonly Lazy<EvidenceRoundSettings> Loaded = new(() => Load());

    public static EvidenceRoundSettings Shared => Loaded.Value;

    public static EvidenceRoundSettings Load(string? path = null)
    {
        path ??= DataFiles.Find(Path.Combine("metadata", "investigation.yaml"));
        if (path is null || !File.Exists(path))
        {
            return new EvidenceRoundSettings(DefaultMaxRounds);
        }

        var file = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build()
            .Deserialize<FileShape>(File.ReadAllText(path));
        var max = file?.EvidenceRounds?.Max ?? 0;
        return new EvidenceRoundSettings(max >= 1 ? max : DefaultMaxRounds);
    }

    private sealed class FileShape
    {
        public RoundsShape? EvidenceRounds { get; set; }
    }

    private sealed class RoundsShape
    {
        public int Max { get; set; }
    }
}
