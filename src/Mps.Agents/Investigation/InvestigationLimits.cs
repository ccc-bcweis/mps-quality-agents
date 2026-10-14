// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Agents.Investigation;

/// <summary>
/// Budgets for one investigation. The default plan stays inside ten tool calls and sixty seconds.
/// </summary>
public sealed class InvestigationLimits
{
    public int MaxToolCalls { get; init; } = 10;

    public TimeSpan MaxDuration { get; init; } = TimeSpan.FromSeconds(60);
}
