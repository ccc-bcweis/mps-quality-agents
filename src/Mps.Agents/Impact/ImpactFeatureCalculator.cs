// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

using Mps.Domain.Impact;

namespace Mps.Agents.Impact;

/// <summary>
/// Computes scope and timing. A lot shared by several tools wins, then a shared recipe, then one chamber, then one tool.
/// Events count only from 14 days before onset through 1 day after. The closest event at or before onset is the alignment.
/// </summary>
public static class ImpactFeatureCalculator
{
    public static ImpactFeatures Calculate(
        DateTimeOffset onset,
        SignalShape shape,
        IReadOnlyList<ImpactObservation> observations,
        IReadOnlyList<AlignedEvent> events)
    {
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(events);
        if (observations.Count == 0)
        {
            throw new ArgumentException("At least one observation is required.", nameof(observations));
        }

        var aligned = SelectEvent(onset, events);
        var (kind, key) = SelectScope(observations);
        string? note = null;
        if (aligned is not null)
        {
            note = string.Concat(
                aligned.Kind,
                " ",
                aligned.Key,
                " at ",
                aligned.At.ToString("O", CultureInfo.InvariantCulture));
        }

        return new ImpactFeatures(kind, key, shape, aligned?.At, note);
    }

    private static (ImpactScopeKind Kind, string Key) SelectScope(IReadOnlyList<ImpactObservation> observations)
    {
        var sharedLot = SharedKey(observations, static observation => observation.LotId);
        if (sharedLot is not null)
        {
            return (ImpactScopeKind.MaterialLot, sharedLot);
        }

        var sharedRecipe = SharedKey(observations, static observation => observation.RecipeId);
        if (sharedRecipe is not null)
        {
            return (ImpactScopeKind.Recipe, sharedRecipe);
        }

        var tools = observations
            .GroupBy(observation => observation.ToolId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First().ToolId)
            .ToArray();
        if (tools.Length == 1)
        {
            var chambers = observations
                .Select(observation => observation.ChamberId)
                .Where(chamber => !string.IsNullOrWhiteSpace(chamber))
                .Select(chamber => chamber!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (chambers.Length == 1)
            {
                return (ImpactScopeKind.Chamber, chambers[0]);
            }

            return (ImpactScopeKind.Tool, tools[0]);
        }

        var joined = string.Join(",", tools.OrderBy(tool => tool, StringComparer.Ordinal));
        return (ImpactScopeKind.Tool, joined);
    }

    private static string? SharedKey(IReadOnlyList<ImpactObservation> observations, Func<ImpactObservation, string?> select)
    {
        var winner = observations
            .Select(observation => new KeyedObservation(select(observation), observation.ToolId))
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .GroupBy(item => item.Key!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new SharedGroup(
                group.First().Key!,
                group.Select(item => item.ToolId).Distinct(StringComparer.OrdinalIgnoreCase).Count()))
            .Where(group => group.Tools >= 2)
            .OrderByDescending(group => group.Tools)
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .FirstOrDefault();
        return winner?.Key;
    }

    private static AlignedEvent? SelectEvent(DateTimeOffset onset, IReadOnlyList<AlignedEvent> events)
    {
        var start = onset.AddDays(-14);
        var end = onset.AddDays(1);
        var inWindow = events.Where(item => item.At >= start && item.At <= end).ToArray();
        var before = inWindow
            .Where(item => item.At <= onset)
            .OrderByDescending(item => item.At)
            .ThenBy(item => item.Kind, StringComparer.Ordinal)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .FirstOrDefault();
        if (before is not null)
        {
            return before;
        }

        return inWindow
            .OrderBy(item => item.At)
            .ThenBy(item => item.Kind, StringComparer.Ordinal)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private sealed record KeyedObservation(string? Key, string ToolId);

    private sealed record SharedGroup(string Key, int Tools);
}
