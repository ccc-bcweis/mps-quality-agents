// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Evidence;
using Mps.Agents.Impact;
using Mps.Agents.Tools;
using Mps.Domain.Impact;

namespace Mps.Agents.Investigation;

/// <summary>
/// Evidence gathered for one incident, or a failure that must not become a hypothesis.
/// </summary>
public sealed class GatherResult
{
    private GatherResult(
        bool failed,
        string? failureReason,
        EvidencePack? pack,
        ImpactFeatures? features,
        ImpactScope? reportedScope,
        IReadOnlyList<ToolCallRecord> toolCalls,
        string promptId,
        string promptVersion)
    {
        Failed = failed;
        FailureReason = failureReason;
        Pack = pack;
        Features = features;
        ReportedScope = reportedScope;
        ToolCalls = toolCalls;
        PromptId = promptId;
        PromptVersion = promptVersion;
    }

    public bool Failed { get; }

    public string? FailureReason { get; }

    public EvidencePack? Pack { get; }

    public ImpactFeatures? Features { get; }

    public ImpactScope? ReportedScope { get; }

    public IReadOnlyList<ToolCallRecord> ToolCalls { get; }

    public string PromptId { get; }

    public string PromptVersion { get; }

    public static GatherResult Ready(
        EvidencePack pack,
        ImpactFeatures features,
        ImpactScope? reportedScope,
        IReadOnlyList<ToolCallRecord> toolCalls,
        string promptId,
        string promptVersion)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(features);
        ArgumentNullException.ThrowIfNull(toolCalls);
        return new GatherResult(false, null, pack, features, reportedScope, toolCalls.ToArray(), promptId, promptVersion);
    }

    public static GatherResult Stopped(
        string failureReason,
        IReadOnlyList<ToolCallRecord> toolCalls,
        string promptId,
        string promptVersion)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
        {
            throw new ArgumentException("A failure names the tool or the budget.", nameof(failureReason));
        }

        ArgumentNullException.ThrowIfNull(toolCalls);
        return new GatherResult(true, failureReason.Trim(), null, null, null, toolCalls.ToArray(), promptId, promptVersion);
    }
}
