// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.ModelGateway.Routing;

/// <summary>
/// Routing, price, and spend-cap settings. Prices are US dollars per one million tokens.
/// </summary>
public sealed class ModelGatewayOptions
{
    public ModelRoute Mode { get; set; } = ModelRoute.Replay;

    /// <summary>
    /// Hard stop for live calls on the UTC day. At or above this amount the gateway serves replay.
    /// </summary>
    public decimal DailySpendCapUsd { get; set; } = 2.00m;

    public decimal InputUsdPerMillionTokens { get; set; } = 0.75m;

    public decimal OutputUsdPerMillionTokens { get; set; } = 4.50m;

    public int LiveRetryCount { get; set; } = 2;

    /// <summary>
    /// Consecutive transient live failures that open the circuit and force replay.
    /// </summary>
    public int CircuitBreakerFailures { get; set; } = 5;

    /// <summary>
    /// When a replay lookup misses, store the mock reasoner response under that prompt hash.
    /// </summary>
    public bool RecordReplayMisses { get; set; }
}
