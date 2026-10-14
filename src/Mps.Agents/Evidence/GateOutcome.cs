// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Agents.Evidence;

/// <summary>
/// Evidence-gate decision. RequestInfo is stored as an escalation whose reason starts with "request_info: ".
/// </summary>
public enum GateOutcome
{
    Sufficient,
    RequestInfo,
    Escalate,
}
