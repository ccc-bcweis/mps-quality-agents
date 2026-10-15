// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Bus;

/// <summary>
/// Single entry point for telemetry, commands, and domain facts.
/// Publish waits when a tool partition is full. It does not drop.
/// Subscribers for one tool run in publish order. A trailing <c>.*</c> matches a family, and <c>*</c> matches every type.
/// </summary>
public interface IEventBus
{
    ValueTask<EventEnvelope> PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<EventEnvelope>> PublishManyAsync(
        IReadOnlyList<EventEnvelope> envelopes,
        CancellationToken cancellationToken = default);

    IDisposable Subscribe(string typePattern, Func<EventEnvelope, CancellationToken, ValueTask> handler);

    IDisposable Subscribe<TPayload>(string typePattern, Func<EventEnvelope, TPayload, CancellationToken, ValueTask> handler);

    IDisposable SubscribeBatch(string typePattern, Func<IReadOnlyList<EventEnvelope>, CancellationToken, ValueTask> handler);
}
