// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Diagnostics.CodeAnalysis;

namespace Mps.ModelGateway.Replay;

/// <summary>
/// Recorded completions for a named scenario, keyed by scenario id and <see cref="PromptHasher"/>.
/// </summary>
public interface IScriptedReplayCatalog
{
    bool TryGet(string scenarioId, string promptHash, [NotNullWhen(true)] out ReplayTurn? turn);

    void Store(string scenarioId, string promptHash, ReplayTurn turn);
}

/// <summary>
/// Optional ambient scenario id for a model call that did not set <c>mps.scenarioId</c> on the chat options.
/// </summary>
public interface IScenarioHint
{
    string? CurrentScenarioId { get; }
}
