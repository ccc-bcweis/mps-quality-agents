// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Demo;

/// <summary>
/// Latest reset phase. The reset request updates it and the live stream pushes each change to the harness
/// (resetProgress), so the page stays usable without polling.
/// </summary>
public sealed class ResetProgress
{
    private readonly object _gate = new();
    private ResetProgressSnapshot _current = ResetProgressSnapshot.Idle;

    /// <summary>Raised after every change, outside the lock.</summary>
    public event Action<ResetProgressSnapshot>? Changed;

    public ResetProgressSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public void Begin(string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        lock (_gate)
        {
            _current = new ResetProgressSnapshot(true, kind, "starting", "Reset started.");
        }

        Changed?.Invoke(Current);
    }

    public void Report(string phase, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        lock (_gate)
        {
            _current = _current with { Running = true, Phase = phase, Message = message };
        }

        Changed?.Invoke(Current);
    }

    public void Complete(string kind)
    {
        lock (_gate)
        {
            _current = new ResetProgressSnapshot(false, kind, "done", "Reset finished.");
        }

        Changed?.Invoke(Current);
    }

    public void Fail(string message)
    {
        lock (_gate)
        {
            var text = string.IsNullOrWhiteSpace(message) ? "Reset failed." : message.Trim();
            _current = new ResetProgressSnapshot(false, _current.Kind, "failed", text);
        }

        Changed?.Invoke(Current);
    }
}

public sealed record ResetProgressSnapshot(bool Running, string? Kind, string Phase, string Message)
{
    public static ResetProgressSnapshot Idle { get; } = new(false, null, "idle", "Ready.");
}
