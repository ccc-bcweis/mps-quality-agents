// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain;
using Mps.Domain.Equipment;

namespace Mps.Simulator.Sites;

/// <summary>
/// Plant ids, names and the tool-id prefix rule, read from data/fleet/sites.yaml (each plant's toolPrefix).
/// Enterprise rows use this so a tool id always names one plant. No plant id, name or prefix lives in code.
/// </summary>
public static class SiteIds
{
    public const int DefaultSeed = Demo.DemoDefaults.PlantSeed;

    public const string FrontEndRole = "Front-end wafer fab";

    public const string PilotRole = "Strip, clean, and pilot line";

    private static readonly Lazy<SiteDirectory> Shared = new(() =>
        SiteDirectory.From(SiteLayoutLoader.Load(DataFiles.Find(Path.Combine("fleet", "sites.yaml")) ?? throw new InvalidOperationException("data/fleet/sites.yaml was not found."))));

    /// <summary>The plants, prefixes and names in sites.yaml.</summary>
    public static SiteDirectory Directory => Shared.Value;

    /// <summary>The plant whose tools keep unprefixed ids (the reference plant).</summary>
    public static string Default => Directory.DefaultSiteId;

    public static string ForTool(string toolId) => Directory.ForTool(toolId);

    public static string NameFor(string siteId) => Directory.NameFor(siteId);

    public static bool IsKnown(string siteId) => Directory.IsKnown(siteId);

    public static ToolFamily FamilyOf(string toolId) => FamilyOf(toolId, Directory.PrefixFor(toolId));

    /// <summary>The family letter after the plant prefix: D deposition, E etch, S strip, C clean.</summary>
    public static ToolFamily FamilyOf(string toolId, string prefix)
    {
        ArgumentNullException.ThrowIfNull(toolId);
        ArgumentNullException.ThrowIfNull(prefix);
        var core = prefix.Length > 0 && toolId.StartsWith(prefix, StringComparison.Ordinal) ? toolId[prefix.Length..] : toolId;
        if (core.Length == 0)
        {
            throw new InvalidOperationException($"Tool id {toolId} has no family letter.");
        }

        return core[0] switch
        {
            'D' => ToolFamily.Deposition,
            'E' => ToolFamily.Etch,
            'S' => ToolFamily.Strip,
            'C' => ToolFamily.Clean,
            _ => throw new InvalidOperationException($"Tool id {toolId} has no known family letter."),
        };
    }
}

/// <summary>Plant ids, names and tool-id prefixes. The longest matching prefix wins; the plant with no prefix takes the rest.</summary>
public sealed class SiteDirectory
{
    private readonly (string Id, string Name, string Prefix)[] _sites;

    public SiteDirectory(IEnumerable<(string Id, string Name, string Prefix)> sites)
    {
        ArgumentNullException.ThrowIfNull(sites);
        _sites = sites.ToArray();
        var unprefixed = _sites.Where(site => site.Prefix.Length == 0).ToArray();
        if (unprefixed.Length != 1)
        {
            throw new InvalidOperationException($"sites.yaml needs exactly one plant with an empty toolPrefix. Found {unprefixed.Length}.");
        }

        DefaultSiteId = unprefixed[0].Id;
    }

    public string DefaultSiteId { get; }

    public IReadOnlyList<(string Id, string Name, string Prefix)> Sites => _sites;

    public static SiteDirectory From(SiteLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return new SiteDirectory(layout.Sites.Select(site => (site.Id, site.Name, site.ToolPrefix)));
    }

    public string ForTool(string toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId))
        {
            throw new ArgumentException("Tool id is required.", nameof(toolId));
        }

        return Match(toolId).Id;
    }

    public string PrefixFor(string toolId) => string.IsNullOrWhiteSpace(toolId) ? string.Empty : Match(toolId).Prefix;

    public string NameFor(string siteId) =>
        _sites.FirstOrDefault(site => string.Equals(site.Id, siteId, StringComparison.Ordinal)).Name ?? siteId;

    public bool IsKnown(string siteId) => _sites.Any(site => string.Equals(site.Id, siteId, StringComparison.Ordinal));

    private (string Id, string Name, string Prefix) Match(string toolId)
    {
        var prefixed = _sites.Where(site => site.Prefix.Length > 0 && toolId.StartsWith(site.Prefix, StringComparison.Ordinal))
            .OrderByDescending(site => site.Prefix.Length)
            .ToArray();
        return prefixed.Length > 0 ? prefixed[0] : _sites.First(site => site.Id == DefaultSiteId);
    }
}
