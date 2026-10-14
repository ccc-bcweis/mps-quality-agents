// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Ranking;

namespace Mps.Agents.Resolution;

/// <summary>
/// Ranks a root cause from an evidence pack. It has no tools.
/// </summary>
public interface IResolutionAgent
{
    Task<ResolutionResult> ResolveAsync(ResolutionRequest request, CancellationToken cancellationToken);
}
