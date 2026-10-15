// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Application.Bus;

/// <summary>
/// The command currently being carried out on this async flow.
/// Child facts that leave causation empty adopt this event. A nested push restores the previous command.
/// </summary>
public static class CommandAmbient
{
    private static readonly AsyncLocal<Frame?> Current = new();

    public static string? CorrelationId => Current.Value?.CorrelationId;

    public static string? EventId => Current.Value?.EventId;

    public static IDisposable Push(string correlationId, string eventId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        return new Scope(correlationId, eventId);
    }

    /// <summary>
    /// A publish has to be awaited before its id exists. Setting the ambient inside that awaited method
    /// does not flow back to the caller, so the caller adopts the id after the publish returns.
    /// </summary>
    public static IDisposable? Adopt(Opened? opened)
    {
        if (opened is not { } handle)
        {
            return null;
        }

        return Push(handle.CorrelationId, handle.EventId);
    }

    public readonly record struct Opened(string CorrelationId, string EventId);

    private sealed class Frame(string correlationId, string eventId)
    {
        public string CorrelationId { get; } = correlationId;

        public string EventId { get; } = eventId;
    }

    private sealed class Scope : IDisposable
    {
        private readonly Frame? _previous;
        private bool _disposed;

        public Scope(string correlationId, string eventId)
        {
            _previous = Current.Value;
            Current.Value = new Frame(correlationId, eventId);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Current.Value = _previous;
        }
    }
}
