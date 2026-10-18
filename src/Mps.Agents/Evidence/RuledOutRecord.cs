// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Agents.Evidence;

/// <summary>
/// A pack record the plant data already rules out. RuleId names the row in data/metadata/ruled-out.yaml.
/// </summary>
public sealed record RuledOutRecord(EvidenceId Id, RootCauseCategory Class, string Reason, string RuleId);
