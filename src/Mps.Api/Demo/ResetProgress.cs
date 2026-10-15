// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Demo;

/// <summary>
/// Latest reset phase. The reset request updates it. The harness polls it so the page stays usable.
/// </summary>
public sealed class ResetProgress
{
    private readonly object _gate = new();
    private ResetProgressSnapshot _current = ResetProgressSnapshot.Idle;

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
    }

    public void Report(string phase, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        lock (_gate)
        {
            _current = _current with { Running = true, Phase = phase, Message = message };
        }
    }

    public void Complete(string kind)
    {
        lock (_gate)
        {
            _current = new ResetProgressSnapshot(false, kind, "done", "Reset finished.");
        }
    }

    public void Fail(string message)
    {
        lock (_gate)
        {
            var text = string.IsNullOrWhiteSpace(message) ? "Reset failed." : message.Trim();
            _current = new ResetProgressSnapshot(false, _current.Kind, "failed", text);
        }
    }
}

public sealed record ResetProgressSnapshot(bool Running, string? Kind, string Phase, string Message)
{
    public static ResetProgressSnapshot Idle { get; } = new(false, null, "idle", "Ready.");
}
