// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Diagnostics.CodeAnalysis;

namespace Mps.ModelGateway.Replay;

/// <summary>
/// In-memory replay catalog. The same hash always returns the same turn.
/// </summary>
public sealed class InMemoryReplayCatalog : IReplayCatalog
{
    private readonly object _gate = new();

    private readonly Dictionary<string, ReplayTurn> _turns = new(StringComparer.Ordinal);

    public bool TryGet(string promptHash, [NotNullWhen(true)] out ReplayTurn? turn)
    {
        if (string.IsNullOrWhiteSpace(promptHash))
        {
            turn = null;
            return false;
        }

        lock (_gate)
        {
            return _turns.TryGetValue(promptHash, out turn);
        }
    }

    public void Store(string promptHash, ReplayTurn turn)
    {
        if (string.IsNullOrWhiteSpace(promptHash))
        {
            throw new ArgumentException("Prompt hash is required.", nameof(promptHash));
        }

        ArgumentNullException.ThrowIfNull(turn);
        lock (_gate)
        {
            _turns[promptHash] = turn;
        }
    }
}
