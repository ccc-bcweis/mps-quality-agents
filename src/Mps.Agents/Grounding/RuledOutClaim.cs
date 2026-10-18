// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Identity;

namespace Mps.Agents.Grounding;

/// <summary>One record the resolution answer rules out, with the reason in one short sentence.</summary>
public sealed record RuledOutClaim(EvidenceId Id, string Reason);
