// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;

using Mps.Domain.Events;

namespace Mps.Bus;

/// <summary>
/// In-process bus. Each tool has its own bounded channel and one reader, so a slow tool does not reorder another.
/// Telemetry may be handed over as one batch. Alerts use the same wait policy and are not dropped.
/// A handler that publishes to the same tool is delivered inline, so the call cannot deadlock the reader.
/// </summary>
public sealed class InProcessEventBus : IEventBus, IAsyncDisposable
{
    public const int DefaultCapacity = 512;

    private readonly int _capacity;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<string, StrongBox<long>> _sequences = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Partition> _partitions = new(StringComparer.Ordinal);
    private readonly List<Registration> _registrations = [];
    private readonly object _registrationGate = new();
    private readonly object _partitionGate = new();
    private readonly AsyncLocal<string?> _owner = new();
    private int _disposed;

    public InProcessEventBus(int capacity = DefaultCapacity, TimeProvider? time = null)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be at least 1.");
        }

        _capacity = capacity;
        _time = time ?? TimeProvider.System;
    }

    public async ValueTask<EventEnvelope> PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var stamped = Stamp(envelope);
        await DeliverAsync(PartitionKey(stamped.ToolId), [stamped], cancellationToken).ConfigureAwait(false);
        return stamped;
    }

    public async ValueTask<IReadOnlyList<EventEnvelope>> PublishManyAsync(
        IReadOnlyList<EventEnvelope> envelopes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelopes);
        if (envelopes.Count == 0)
        {
            return [];
        }

        var stamped = new EventEnvelope[envelopes.Count];
        var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var index = 0; index < envelopes.Count; index++)
        {
            var item = Stamp(envelopes[index]);
            stamped[index] = item;
            var key = PartitionKey(item.ToolId);
            if (!groups.TryGetValue(key, out var indexes))
            {
                indexes = [];
                groups.Add(key, indexes);
            }

            indexes.Add(index);
        }

        foreach (var group in groups)
        {
            var batch = new EventEnvelope[group.Value.Count];
            for (var index = 0; index < group.Value.Count; index++)
            {
                batch[index] = stamped[group.Value[index]];
            }

            await DeliverAsync(group.Key, batch, cancellationToken).ConfigureAwait(false);
        }

        return stamped;
    }

    public IDisposable Subscribe(string typePattern, Func<EventEnvelope, CancellationToken, ValueTask> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typePattern);
        ArgumentNullException.ThrowIfNull(handler);
        return Add(new Registration(typePattern, batch: false, async (items, cancellationToken) =>
        {
            foreach (var item in items)
            {
                await handler(item, cancellationToken).ConfigureAwait(false);
            }
        }));
    }

    public IDisposable Subscribe<TPayload>(string typePattern, Func<EventEnvelope, TPayload, CancellationToken, ValueTask> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Subscribe(typePattern, async (envelope, cancellationToken) =>
        {
            var payload = envelope.Payload.Deserialize<TPayload>(EventJson.Options);
            if (payload is null)
            {
                throw new InvalidOperationException("Event " + envelope.EventId + " has no " + typeof(TPayload).Name + " payload.");
            }

            await handler(envelope, payload, cancellationToken).ConfigureAwait(false);
        });
    }

    public IDisposable SubscribeBatch(string typePattern, Func<IReadOnlyList<EventEnvelope>, CancellationToken, ValueTask> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typePattern);
        ArgumentNullException.ThrowIfNull(handler);
        return Add(new Registration(typePattern, batch: true, handler));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        List<Task> loops;
        lock (_partitionGate)
        {
            loops = [];
            foreach (var partition in _partitions.Values)
            {
                partition.Inbox.Writer.TryComplete();
                loops.Add(partition.Loop);
            }
        }

        if (loops.Count > 0)
        {
            await Task.WhenAll(loops).ConfigureAwait(false);
        }
    }

    private async ValueTask DeliverAsync(string key, EventEnvelope[] batch, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (string.Equals(_owner.Value, key, StringComparison.Ordinal))
        {
            await InvokeAsync(batch, cancellationToken).ConfigureAwait(false);
            return;
        }

        var partition = GetPartition(key);
        var work = new Work(batch);
        await partition.Inbox.Writer.WriteAsync(work, cancellationToken).ConfigureAwait(false);
        await work.Done.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private Partition GetPartition(string key)
    {
        if (_partitions.TryGetValue(key, out var existing))
        {
            return existing;
        }

        lock (_partitionGate)
        {
            if (_partitions.TryGetValue(key, out existing))
            {
                return existing;
            }

            var created = new Partition(_capacity);
            created.Loop = RunAsync(key, created);
            _partitions[key] = created;
            return created;
        }
    }

    private async Task RunAsync(string key, Partition partition)
    {
        try
        {
            await foreach (var work in partition.Inbox.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                _owner.Value = key;
                try
                {
                    await InvokeAsync(work.Batch, CancellationToken.None).ConfigureAwait(false);
                    work.Done.TrySetResult();
                }
                catch (Exception exception)
                {
                    work.Done.TrySetException(exception);
                }
                finally
                {
                    _owner.Value = null;
                }
            }
        }
        catch (ChannelClosedException)
        {
            // Shutdown.
        }
    }

    private async ValueTask InvokeAsync(EventEnvelope[] batch, CancellationToken cancellationToken)
    {
        Registration[] snapshot;
        lock (_registrationGate)
        {
            snapshot = [.. _registrations];
        }

        if (snapshot.Length == 0 || batch.Length == 0)
        {
            return;
        }

        var telemetry = EventTypes.IsTelemetry(batch[0].Type);
        List<Exception>? errors = null;
        foreach (var registration in snapshot)
        {
            // A catch-all subscriber listens for commands and domain facts. A multi-reading
            // telemetry batch stays on the batch path so one slow listener cannot take each row.
            if (telemetry && batch.Length > 1 && !registration.Batch && string.Equals(registration.Pattern, "*", StringComparison.Ordinal))
            {
                continue;
            }

            var matches = telemetry
                ? batch.Any(item => EventPatterns.Matches(registration.Pattern, item.Type))
                : EventPatterns.Matches(registration.Pattern, batch[0].Type);
            if (!matches)
            {
                continue;
            }

            if (!registration.Batch && telemetry)
            {
                foreach (var item in batch)
                {
                    if (!EventPatterns.Matches(registration.Pattern, item.Type))
                    {
                        continue;
                    }

                    await CallAsync(registration, [item], errors ??= [], cancellationToken).ConfigureAwait(false);
                }

                continue;
            }

            var selected = telemetry
                ? batch.Where(item => EventPatterns.Matches(registration.Pattern, item.Type)).ToArray()
                : batch;
            if (selected.Length == 0)
            {
                continue;
            }

            await CallAsync(registration, selected, errors ??= [], cancellationToken).ConfigureAwait(false);
        }

        if (errors is { Count: > 0 })
        {
            throw new AggregateException("An event subscriber failed.", errors);
        }
    }

    private static async ValueTask CallAsync(
        Registration registration,
        EventEnvelope[] items,
        List<Exception> errors,
        CancellationToken cancellationToken)
    {
        try
        {
            await registration.Handler(items, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            errors.Add(exception);
        }
    }

    private EventEnvelope Stamp(EventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (string.IsNullOrWhiteSpace(envelope.Type) || !envelope.Type.Contains('.', StringComparison.Ordinal))
        {
            throw new ArgumentException("Event type must be a dotted name.", nameof(envelope));
        }

        if (string.IsNullOrWhiteSpace(envelope.CorrelationId))
        {
            throw new ArgumentException("Correlation id is required.", nameof(envelope));
        }

        if (string.IsNullOrWhiteSpace(envelope.Source))
        {
            throw new ArgumentException("Source is required.", nameof(envelope));
        }

        var now = _time.GetUtcNow();
        var occurred = envelope.OccurredAt == default ? now : envelope.OccurredAt;
        var payload = envelope.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? EventJson.Empty()
            : envelope.Payload;
        return new EventEnvelope(
            string.IsNullOrWhiteSpace(envelope.EventId) ? EventIds.Next(now) : envelope.EventId,
            envelope.Type.Trim(),
            envelope.CorrelationId.Trim(),
            envelope.CausationId,
            envelope.ParentEventId,
            NextSequence(envelope.CorrelationId),
            envelope.Source.Trim(),
            envelope.SiteId,
            envelope.ToolId,
            envelope.ChamberId,
            occurred,
            now,
            envelope.SchemaVersion == 0 ? EventEnvelope.CurrentSchemaVersion : envelope.SchemaVersion,
            payload);
    }

    private long NextSequence(string correlationId)
    {
        var box = _sequences.GetOrAdd(correlationId, static _ => new StrongBox<long>(0));
        lock (box)
        {
            box.Value++;
            return box.Value;
        }
    }

    private Subscription Add(Registration registration)
    {
        lock (_registrationGate)
        {
            _registrations.Add(registration);
        }

        return new Subscription(this, registration);
    }

    private void Remove(Registration registration)
    {
        lock (_registrationGate)
        {
            _registrations.Remove(registration);
        }
    }

    private static string PartitionKey(string? toolId) =>
        string.IsNullOrWhiteSpace(toolId) ? "_plant" : toolId.Trim();

    private sealed class Partition
    {
        public Partition(int capacity)
        {
            Inbox = System.Threading.Channels.Channel.CreateBounded<Work>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            });
        }

        public Channel<Work> Inbox { get; }

        public Task Loop { get; set; } = Task.CompletedTask;
    }

    private sealed class Work
    {
        public Work(EventEnvelope[] batch)
        {
            Batch = batch;
            Done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public EventEnvelope[] Batch { get; }

        public TaskCompletionSource Done { get; }
    }

    private sealed class Registration
    {
        public Registration(string pattern, bool batch, Func<IReadOnlyList<EventEnvelope>, CancellationToken, ValueTask> handler)
        {
            Pattern = pattern;
            Batch = batch;
            Handler = handler;
        }

        public string Pattern { get; }

        public bool Batch { get; }

        public Func<IReadOnlyList<EventEnvelope>, CancellationToken, ValueTask> Handler { get; }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly InProcessEventBus _bus;
        private readonly Registration _registration;
        private int _disposed;

        public Subscription(InProcessEventBus bus, Registration registration)
        {
            _bus = bus;
            _registration = registration;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _bus.Remove(_registration);
            }
        }
    }
}
