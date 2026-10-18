// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.ModelGateway.Routing;

/// <summary>
/// The gateway's most recent behavior. <see cref="FellBack"/> is true when the last answer came from saved answers
/// because the live model failed, was over the spend cap, or the circuit was open.
/// </summary>
public sealed record ModelGatewayStatus(
    ModelRoute Mode,
    string? LastRoute,
    bool FellBack,
    int ConsecutiveFailures,
    bool CircuitOpen,
    DateTimeOffset? LastFailureAt,
    string? LastFailure);
