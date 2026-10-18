// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Application.Bus;

/// <summary>
/// A fact the application wants on the bus. The host fills the event id, sequence, and recorded time.
/// </summary>
public sealed record LifecycleEvent(
    string Type,
    string CorrelationId,
    string? CausationId,
    string? ParentEventId,
    string Source,
    string? SiteId,
    string? ToolId,
    string? ChamberId,
    DateTimeOffset OccurredAt,
    object? Payload);

/// <summary>
/// Port the application uses to emit. The host adapts it to the in-process bus.
/// </summary>
public interface ILifecycleEventSink
{
    ValueTask<string> PublishAsync(LifecycleEvent draft, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<string>> PublishManyAsync(
        IReadOnlyList<LifecycleEvent> drafts,
        CancellationToken cancellationToken = default);
}

/// <summary>Used by unit tests that construct a service without a host.</summary>
public sealed class NullLifecycleEventSink : ILifecycleEventSink
{
    public static NullLifecycleEventSink Instance { get; } = new();

    public ValueTask<string> PublishAsync(LifecycleEvent draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        _ = cancellationToken;
        return ValueTask.FromResult(string.Empty);
    }

    public ValueTask<IReadOnlyList<string>> PublishManyAsync(
        IReadOnlyList<LifecycleEvent> drafts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        _ = cancellationToken;
        return ValueTask.FromResult<IReadOnlyList<string>>([]);
    }
}
