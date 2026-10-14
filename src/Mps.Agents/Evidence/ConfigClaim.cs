// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Evidence;

namespace Mps.Agents.Evidence;

/// <summary>
/// A revision statement for one part, tagged with the system that stated it.
/// </summary>
public sealed class ConfigClaim
{
    public ConfigClaim(EvidenceSource source, string partName, string revision, string? externalRef)
    {
        if (string.IsNullOrWhiteSpace(partName))
        {
            throw new ArgumentException("Part name is required.", nameof(partName));
        }

        if (string.IsNullOrWhiteSpace(revision))
        {
            throw new ArgumentException("Revision is required.", nameof(revision));
        }

        Source = source;
        PartName = partName.Trim();
        Revision = revision.Trim();
        ExternalRef = string.IsNullOrWhiteSpace(externalRef) ? null : externalRef.Trim();
    }

    public EvidenceSource Source { get; }

    public string PartName { get; }

    public string Revision { get; }

    public string? ExternalRef { get; }
}
