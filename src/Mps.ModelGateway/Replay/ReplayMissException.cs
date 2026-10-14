// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.ModelGateway.Replay;

/// <summary>
/// The replay catalog has no turn for this prompt. The gateway falls back to the mock reasoner.
/// </summary>
public sealed class ReplayMissException : Exception
{
    public ReplayMissException(string promptHash)
        : base($"No replay turn is recorded for prompt hash {promptHash}.")
    {
        PromptHash = promptHash;
    }

    public string PromptHash { get; }
}
