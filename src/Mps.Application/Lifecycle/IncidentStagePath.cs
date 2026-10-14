// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Errors;
using Mps.Domain.Incidents;
using Mps.Domain.Lifecycle;

namespace Mps.Application.Lifecycle;

public static class IncidentStagePath
{
    public static IReadOnlyList<IncidentStage> Between(IncidentStage from, IncidentStage to, bool blockApproval)
    {
        if (from == to)
        {
            return [];
        }

        var previous = new Dictionary<IncidentStage, IncidentStage>();
        var seen = new HashSet<IncidentStage> { from };
        var queue = new Queue<IncidentStage>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in Enum.GetValues<IncidentStage>())
            {
                if (!IncidentLifecycle.CanTransition(current, next) || !seen.Add(next))
                {
                    continue;
                }

                if (blockApproval
                    && current == IncidentStage.RecommendationReady
                    && next == IncidentStage.AwaitingApproval)
                {
                    seen.Remove(next);
                    continue;
                }

                previous[next] = current;
                if (next == to)
                {
                    return Reconstruct(previous, from, to);
                }

                queue.Enqueue(next);
            }
        }

        throw new LifecycleConflictException($"Cannot move an incident from {from} to {to}.");
    }

    private static IncidentStage[] Reconstruct(
        Dictionary<IncidentStage, IncidentStage> previous,
        IncidentStage from,
        IncidentStage to)
    {
        var steps = new List<IncidentStage>();
        var cursor = to;
        while (cursor != from)
        {
            steps.Add(cursor);
            cursor = previous[cursor];
        }

        steps.Reverse();
        return steps.ToArray();
    }
}
