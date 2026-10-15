// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Demo;

/// <summary>
/// Live reads the running bus. Replay reads one scenario recording.
/// This is separate from the saved-answer or live model choice.
/// </summary>
public sealed class ConsoleDataMode
{
    private readonly object _gate = new();
    private string _mode = "live";
    private string? _scenarioId;
    private IReadOnlyList<ReplayMessage> _messages = [];

    public bool IsReplay
    {
        get
        {
            lock (_gate)
            {
                return string.Equals(_mode, "replay", StringComparison.Ordinal);
            }
        }
    }

    public DataModeSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new DataModeSnapshot(_mode, _scenarioId, _messages.Count);
        }
    }

    public IReadOnlyList<ReplayMessage> Messages
    {
        get
        {
            lock (_gate)
            {
                return _messages;
            }
        }
    }

    public void UseLive()
    {
        lock (_gate)
        {
            _mode = "live";
            _scenarioId = null;
            _messages = [];
        }
    }

    public void UseReplay(string scenarioId, IReadOnlyList<ReplayMessage> messages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioId);
        ArgumentNullException.ThrowIfNull(messages);
        lock (_gate)
        {
            _mode = "replay";
            _scenarioId = scenarioId;
            _messages = messages;
        }
    }
}

public sealed record DataModeSnapshot(string Mode, string? ScenarioId, int MessageCount);
