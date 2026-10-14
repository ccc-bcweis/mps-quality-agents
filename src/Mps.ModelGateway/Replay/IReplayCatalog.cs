// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Diagnostics.CodeAnalysis;

namespace Mps.ModelGateway.Replay;

/// <summary>
/// Recorded completions keyed by <see cref="PromptHasher"/>.
/// </summary>
public interface IReplayCatalog
{
    bool TryGet(string promptHash, [NotNullWhen(true)] out ReplayTurn? turn);

    void Store(string promptHash, ReplayTurn turn);
}
