// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Agents.Explain;

/// <summary>
/// How one evidence item bears on one root-cause class. Wire values are supports, rulesOut, and neutral.
/// </summary>
public enum EvidenceStance
{
    Supports,
    RulesOut,
    Neutral,
}
