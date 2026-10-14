// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.ModelGateway.Routing;

/// <summary>
/// Which completion adapter the gateway prefers before the spend cap and the circuit breaker.
/// </summary>
public enum ModelRoute
{
    AzureOpenAI,
    OpenAI,
    Replay,
    MockReasoner,
}
