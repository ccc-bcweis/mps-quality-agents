// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Spc;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Agents.Investigation;

/// <summary>How long to wait for enough readings to classify the signal shape. From data/metadata/investigation.yaml.</summary>
public sealed record ShapeWait(int Attempts, TimeSpan Delay)
{
    public static ShapeWait None { get; } = new(0, TimeSpan.Zero);

    public static ShapeWait Load(string? path = null)
    {
        path ??= DataFiles.Find(Path.Combine("metadata", "investigation.yaml"));
        if (path is null || !File.Exists(path))
        {
            return None;
        }

        var file = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build()
            .Deserialize<FileShape>(File.ReadAllText(path));
        var wait = file?.ShapeWait;
        return wait is null || wait.Attempts <= 0 || wait.DelaySeconds <= 0
            ? None
            : new ShapeWait(wait.Attempts, TimeSpan.FromSeconds(wait.DelaySeconds));
    }

    private sealed class FileShape
    {
        public WaitShape? ShapeWait { get; set; }
    }

    private sealed class WaitShape
    {
        public int Attempts { get; set; }

        public double DelaySeconds { get; set; }
    }
}
