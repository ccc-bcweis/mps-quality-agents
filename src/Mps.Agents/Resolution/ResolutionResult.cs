// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Grounding;

namespace Mps.Agents.Resolution;

/// <summary>
/// A schema-valid resolution claim, or an escalation after the model budget.
/// </summary>
public sealed class ResolutionResult
{
    private ResolutionResult(bool escalated, string? reason, ResolutionClaim? claim, int modelCalls, string promptId, string promptVersion)
    {
        Escalated = escalated;
        Reason = reason;
        Claim = claim;
        ModelCalls = modelCalls;
        PromptId = promptId;
        PromptVersion = promptVersion;
    }

    public bool Escalated { get; }

    public string? Reason { get; }

    public ResolutionClaim? Claim { get; }

    public int ModelCalls { get; }

    public string PromptId { get; }

    public string PromptVersion { get; }

    public static ResolutionResult Ready(ResolutionClaim claim, int modelCalls, string promptId, string promptVersion)
    {
        ArgumentNullException.ThrowIfNull(claim);
        return new ResolutionResult(false, null, claim, modelCalls, promptId, promptVersion);
    }

    public static ResolutionResult Escalate(string reason, int modelCalls, string promptId, string promptVersion)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("An escalation names the reason.", nameof(reason));
        }

        return new ResolutionResult(true, reason.Trim(), null, modelCalls, promptId, promptVersion);
    }
}
