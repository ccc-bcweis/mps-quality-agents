// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain;
using Mps.Domain.Identity;

namespace Mps.Domain.Equipment;

public enum ToolFamily
{
    Deposition,
    Etch,
    Strip,
    Clean,
}

public sealed record Chamber
{
    public Chamber(string id, string name)
    {
        Id = RequiredText.Value(id, "Chamber id is required.");
        Name = RequiredText.Value(name, "Chamber name is required.");
    }

    public string Id { get; }

    public string Name { get; }
}

/// <summary>
/// A fictional MPS process tool. Model names such as MPS-E7 are synthetic.
/// </summary>
public sealed class Tool
{
    public Tool(ToolId id, string name, ToolFamily family, string model, string bay, IReadOnlyList<Chamber> chambers, string? siteId = null)
    {
        if (id.IsEmpty)
        {
            throw new DomainRuleException("Tool id is required.");
        }

        ArgumentNullException.ThrowIfNull(chambers);
        if (chambers.Count == 0)
        {
            throw new DomainRuleException("A tool has at least one chamber.");
        }

        Id = id;
        Name = RequiredText.Value(name, "Tool name is required.");
        Family = family;
        Model = RequiredText.Value(model, "Tool model is required.");
        Bay = RequiredText.Value(bay, "Bay is required.");
        Chambers = chambers.ToArray();
        SiteId = string.IsNullOrWhiteSpace(siteId) ? null : siteId.Trim();
    }

    public ToolId Id { get; }

    public string Name { get; }

    public ToolFamily Family { get; }

    public string Model { get; }

    public string Bay { get; }

    public string? SiteId { get; }

    public IReadOnlyList<Chamber> Chambers { get; }
}
