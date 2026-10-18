// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Identity;

namespace Mps.Domain.Hypotheses;

public enum RuledOutSource
{
    Record,
    Agent,
}

/// <summary>
/// One record the investigation considered and ruled out. Record means the plant data. Agent means the answer.
/// </summary>
public sealed record RuledOutEntry(EvidenceId Id, string Reason, RuledOutSource Source, RootCauseCategory? Class)
{
    public EvidenceId Id { get; init; } = Id.IsEmpty
        ? throw new DomainRuleException("Ruled-out evidence id is required.")
        : Id;

    public string Reason { get; init; } = RequiredText.Value(Reason, "Ruled-out reason is required.");
}
