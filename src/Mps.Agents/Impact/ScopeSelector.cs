// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Impact;

namespace Mps.Agents.Impact;

/// <summary>
/// Keeps the calculated scope. A broader lot or recipe reported by the impact tool is used when local rows already name that key.
/// </summary>
public static class ScopeSelector
{
    public static ImpactFeatures Select(
        ImpactFeatures calculated,
        ImpactScope? reported,
        IReadOnlyList<ImpactObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(calculated);
        ArgumentNullException.ThrowIfNull(observations);
        if (reported is null)
        {
            return calculated;
        }

        var broader = reported.Kind is ImpactScopeKind.MaterialLot or ImpactScopeKind.Recipe
            && calculated.Kind is ImpactScopeKind.Chamber or ImpactScopeKind.Tool
            && observations.Any(observation => Matches(reported, observation));
        if (!broader)
        {
            return calculated;
        }

        return new ImpactFeatures(
            reported.Kind,
            reported.Key,
            calculated.Shape,
            calculated.AlignedEventAt ?? reported.AlignedEventAt,
            reported.AlignmentNote ?? calculated.AlignmentNote);
    }

    private static bool Matches(ImpactScope reported, ImpactObservation observation)
    {
        var key = reported.Kind == ImpactScopeKind.MaterialLot ? observation.LotId : observation.RecipeId;
        return key is not null && string.Equals(key, reported.Key, StringComparison.OrdinalIgnoreCase);
    }
}
