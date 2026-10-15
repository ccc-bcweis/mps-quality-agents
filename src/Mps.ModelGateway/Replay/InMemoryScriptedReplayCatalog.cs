// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Diagnostics.CodeAnalysis;

namespace Mps.ModelGateway.Replay;

/// <summary>
/// In-memory scripted replay. The key is the scenario id plus the prompt hash.
/// </summary>
public sealed class InMemoryScriptedReplayCatalog : IScriptedReplayCatalog
{
    private readonly object _gate = new();

    private readonly Dictionary<string, ReplayTurn> _turns = new(StringComparer.Ordinal);

    public bool TryGet(string scenarioId, string promptHash, [NotNullWhen(true)] out ReplayTurn? turn)
    {
        if (string.IsNullOrWhiteSpace(scenarioId) || string.IsNullOrWhiteSpace(promptHash))
        {
            turn = null;
            return false;
        }

        lock (_gate)
        {
            return _turns.TryGetValue(Key(scenarioId, promptHash), out turn);
        }
    }

    public void Store(string scenarioId, string promptHash, ReplayTurn turn)
    {
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            throw new ArgumentException("Scenario id is required.", nameof(scenarioId));
        }

        if (string.IsNullOrWhiteSpace(promptHash))
        {
            throw new ArgumentException("Prompt hash is required.", nameof(promptHash));
        }

        ArgumentNullException.ThrowIfNull(turn);
        lock (_gate)
        {
            _turns[Key(scenarioId, promptHash)] = turn;
        }
    }

    internal static string Key(string scenarioId, string promptHash)
    {
        return scenarioId.Trim() + "\n" + promptHash.Trim();
    }
}
