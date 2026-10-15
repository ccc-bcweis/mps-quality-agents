// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Bus;
using Mps.Bus;
using Mps.Simulator.Sites;

namespace Mps.Api.Bus;

/// <summary>Turns an application draft into an envelope and waits until subscribers have seen it.</summary>
public sealed class BusEventSink : ILifecycleEventSink
{
    private readonly IEventBus _bus;
    private readonly TimeProvider _time;

    public BusEventSink(IEventBus bus, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(time);
        _bus = bus;
        _time = time;
    }

    public async ValueTask<string> PublishAsync(LifecycleEvent draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var stamped = await _bus.PublishAsync(ToEnvelope(draft), cancellationToken).ConfigureAwait(false);
        return stamped.EventId;
    }

    public async ValueTask<IReadOnlyList<string>> PublishManyAsync(
        IReadOnlyList<LifecycleEvent> drafts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        if (drafts.Count == 0)
        {
            return [];
        }

        var envelopes = new EventEnvelope[drafts.Count];
        for (var index = 0; index < drafts.Count; index++)
        {
            envelopes[index] = ToEnvelope(drafts[index]);
        }

        var stamped = await _bus.PublishManyAsync(envelopes, cancellationToken).ConfigureAwait(false);
        var ids = new string[stamped.Count];
        for (var index = 0; index < stamped.Count; index++)
        {
            ids[index] = stamped[index].EventId;
        }

        return ids;
    }

    private EventEnvelope ToEnvelope(LifecycleEvent draft)
    {
        var occurred = draft.OccurredAt == default ? _time.GetUtcNow() : draft.OccurredAt;
        var site = string.IsNullOrWhiteSpace(draft.SiteId) ? SiteFor(draft.ToolId) : draft.SiteId;
        var causation = string.IsNullOrWhiteSpace(draft.CausationId) ? CommandAmbient.EventId : draft.CausationId;
        var parent = string.IsNullOrWhiteSpace(draft.ParentEventId) ? causation : draft.ParentEventId;
        var correlation = string.IsNullOrWhiteSpace(draft.CorrelationId)
            ? CommandAmbient.CorrelationId ?? string.Empty
            : draft.CorrelationId;
        return EventEnvelope.Draft(
            draft.Type,
            correlation,
            draft.Source,
            occurred,
            draft.Payload,
            causation,
            parent,
            site,
            draft.ToolId,
            draft.ChamberId);
    }

    private static string? SiteFor(string? toolId) =>
        string.IsNullOrWhiteSpace(toolId) ? null : SiteIds.ForTool(toolId);
}
