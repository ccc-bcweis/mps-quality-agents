// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Http;
using Mps.Simulator.Sites;

namespace Mps.Api.Bus;

/// <summary>
/// Walks parent and causation links across the event log and the telemetry table.
/// The focus node is the root of <see cref="CausalTraceResponse.Tree"/>. Ancestors are listed root-first and do not include the focus.
/// </summary>
public sealed class CausalTraceService
{
    public const int MaxDepth = 64;

    public const int MaxNodes = 5000;

    public const int MaxChildren = 200;

    private readonly SqliteEventStore _store;
    private readonly SiteWorld _world;

    public CausalTraceService(SqliteEventStore store, SiteWorld world)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(world);
        _store = store;
        _world = world;
    }

    public CausalTraceResponse? Build(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return null;
        }

        var focus = _store.Find(eventId.Trim());
        if (focus is null)
        {
            return null;
        }

        var ancestors = new List<TraceNodeResponse>();
        var guard = new HashSet<string>(StringComparer.Ordinal) { focus.EventId };
        var cursor = focus;
        while (ancestors.Count < MaxDepth)
        {
            var nextId = cursor.ParentEventId ?? cursor.CausationId;
            if (nextId is null || !guard.Add(nextId))
            {
                break;
            }

            var next = _store.Find(nextId);
            if (next is null)
            {
                break;
            }

            ancestors.Add(TraceSummaries.Node(next, ToolName(next.ToolId), Cause(next), [], false));
            cursor = next;
        }

        ancestors.Reverse();
        var rootId = ancestors.Count == 0 ? focus.EventId : ancestors[0].EventId;
        var budget = MaxNodes;
        var path = new HashSet<string>(StringComparer.Ordinal) { focus.EventId };
        var tree = BuildNode(focus, 0, ref budget, path);
        return new CausalTraceResponse(focus.EventId, rootId, ancestors, tree);
    }

    private TraceNodeResponse BuildNode(StoredEvent node, int depth, ref int budget, HashSet<string> path)
    {
        var children = new List<TraceNodeResponse>();
        var truncated = false;
        if (depth >= MaxDepth || budget <= 0)
        {
            truncated = true;
        }
        else
        {
            var rows = _store.Children(node.EventId, MaxChildren + 1);
            if (rows.Count > MaxChildren)
            {
                truncated = true;
                rows = rows.Take(MaxChildren).ToArray();
            }

            foreach (var child in rows)
            {
                if (budget <= 0)
                {
                    truncated = true;
                    break;
                }

                if (!path.Add(child.EventId))
                {
                    continue;
                }

                budget--;
                children.Add(BuildNode(child, depth + 1, ref budget, path));
                path.Remove(child.EventId);
            }
        }

        return TraceSummaries.Node(node, ToolName(node.ToolId), Cause(node), children, truncated);
    }

    private StoredEvent? Cause(StoredEvent row) =>
        row.FromTelemetry || string.IsNullOrWhiteSpace(row.CausationId) ? null : _store.Find(row.CausationId);

    private string? ToolName(string? toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId))
        {
            return null;
        }

        return _world.FindTool(toolId)?.Name ?? toolId;
    }
}
