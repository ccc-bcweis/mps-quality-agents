// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Spc;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Agents.Impact;

/// <summary>One shape from data/metadata/signal-shapes.yaml.</summary>
public sealed record SignalShapeEntry(string Id, SignalShape Shape, string Description, IReadOnlyList<string> Words);

/// <summary>
/// The shapes the resolution agent may report in its "shape" field, from data/metadata/signal-shapes.yaml. Each id must
/// name a shape the classifier measures, and every measured shape must have an entry.
/// </summary>
public sealed class SignalShapeCatalog
{
    public const string RelativePath = "metadata/signal-shapes.yaml";

    private static readonly Lazy<SignalShapeCatalog> SharedCatalog = new(() => Load());

    private readonly Dictionary<string, SignalShapeEntry> _byId;

    public SignalShapeCatalog(IReadOnlyList<SignalShapeEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Entries = entries.ToArray();
        _byId = Entries.ToDictionary(entry => entry.Id, StringComparer.OrdinalIgnoreCase);
        var missing = Enum.GetValues<SignalShape>().Where(shape => Entries.All(entry => entry.Shape != shape)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException("The signal shape catalog has no entry for: " + string.Join(", ", missing) + ".");
        }
    }

    public static SignalShapeCatalog Shared => SharedCatalog.Value;

    public IReadOnlyList<SignalShapeEntry> Entries { get; }

    /// <summary>The ids the agent may answer with, in catalog order.</summary>
    public IReadOnlyList<string> Ids => Entries.Select(entry => entry.Id).ToArray();

    public SignalShapeEntry Of(SignalShape shape) => Entries.First(entry => entry.Shape == shape);

    /// <summary>True when the reported id is in the catalog.</summary>
    public bool TryParse(string? id, out SignalShapeEntry entry)
    {
        entry = null!;
        return !string.IsNullOrWhiteSpace(id) && _byId.TryGetValue(id.Trim(), out entry!);
    }

    public static SignalShapeCatalog Load(string? path = null)
    {
        path ??= DataFiles.Find(RelativePath);
        if (path is null || !File.Exists(path))
        {
            throw new InvalidOperationException("The signal shape catalog (data/" + RelativePath + ") was not found.");
        }

        var file = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build()
            .Deserialize<FileShape>(File.ReadAllText(path));
        var entries = new List<SignalShapeEntry>();
        foreach (var row in file?.Shapes ?? [])
        {
            if (string.IsNullOrWhiteSpace(row.Id) || !Enum.TryParse<SignalShape>(row.Id.Trim(), ignoreCase: true, out var shape))
            {
                throw new InvalidOperationException("Signal shape \"" + row.Id + "\" in data/" + RelativePath + " is not a shape the classifier measures.");
            }

            entries.Add(new SignalShapeEntry(row.Id.Trim(), shape, (row.Description ?? row.Id).Trim(), row.Words?.Where(word => !string.IsNullOrWhiteSpace(word)).Select(word => word.Trim()).ToArray() ?? []));
        }

        return new SignalShapeCatalog(entries);
    }

    private sealed class FileShape
    {
        public List<RowShape>? Shapes { get; set; }
    }

    private sealed class RowShape
    {
        public string? Id { get; set; }

        public string? Description { get; set; }

        public List<string>? Words { get; set; }
    }
}
