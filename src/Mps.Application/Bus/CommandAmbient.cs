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
