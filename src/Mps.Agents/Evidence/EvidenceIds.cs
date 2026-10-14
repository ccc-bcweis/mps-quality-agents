// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Evidence;
using Mps.Domain.Identity;

namespace Mps.Agents.Evidence;

/// <summary>
/// Deterministic evidence ids from the source and the external reference.
/// </summary>
public static class EvidenceIds
{
    public static readonly Guid Namespace = Guid.Parse("8f3c2a1b-6d4e-4c7a-9b21-0a6e5d4c3b2a");

    public static EvidenceId Create(EvidenceSource source, string externalRef)
    {
        if (string.IsNullOrWhiteSpace(externalRef))
        {
            throw new ArgumentException("External reference is required.", nameof(externalRef));
        }

        var name = source.ToString() + "|" + externalRef.Trim();
        return new EvidenceId(Uuid5.Create(Namespace, name));
    }
}
