// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

using Mps.Domain;

namespace Mps.Domain.Identity;

public readonly record struct ToolId
{
    public ToolId(string value)
    {
        Value = RequiredText.Value(value, "Tool id is required.");
    }

    public string Value { get; }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct IncidentId
{
    public IncidentId(Guid value)
    {
        Value = GuidIds.Require(value, "Incident id is required.");
    }

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static IncidentId New() => new(Guid.NewGuid());

    public override string ToString() => GuidIds.Format(Value);
}

public readonly record struct AlertId
{
    public AlertId(Guid value)
    {
        Value = GuidIds.Require(value, "Alert id is required.");
    }

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static AlertId New() => new(Guid.NewGuid());

    public override string ToString() => GuidIds.Format(Value);
}

public readonly record struct TicketId
{
    public TicketId(Guid value)
    {
        Value = GuidIds.Require(value, "Ticket id is required.");
    }

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static TicketId New() => new(Guid.NewGuid());

    public override string ToString() => GuidIds.Format(Value);
}

public readonly record struct EvidenceId
{
    public EvidenceId(Guid value)
    {
        Value = GuidIds.Require(value, "Evidence id is required.");
    }

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static EvidenceId New() => new(Guid.NewGuid());

    public override string ToString() => GuidIds.Format(Value);
}

public readonly record struct HypothesisId
{
    public HypothesisId(Guid value)
    {
        Value = GuidIds.Require(value, "Hypothesis id is required.");
    }

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static HypothesisId New() => new(Guid.NewGuid());

    public override string ToString() => GuidIds.Format(Value);
}

public readonly record struct ApprovalTokenId
{
    public ApprovalTokenId(Guid value)
    {
        Value = GuidIds.Require(value, "Approval token id is required.");
    }

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static ApprovalTokenId New() => new(Guid.NewGuid());

    public override string ToString() => GuidIds.Format(Value);
}

public readonly record struct CapaId
{
    public CapaId(Guid value)
    {
        Value = GuidIds.Require(value, "CAPA id is required.");
    }

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static CapaId New() => new(Guid.NewGuid());

    public override string ToString() => GuidIds.Format(Value);
}

public readonly record struct InvestigationId
{
    public InvestigationId(Guid value)
    {
        Value = GuidIds.Require(value, "Investigation id is required.");
    }

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static InvestigationId New() => new(Guid.NewGuid());

    public override string ToString() => GuidIds.Format(Value);
}

file static class GuidIds
{
    public static Guid Require(Guid value, string message)
    {
        if (value == Guid.Empty)
        {
            throw new DomainRuleException(message);
        }

        return value;
    }

    public static string Format(Guid value) => value.ToString("D", CultureInfo.InvariantCulture);
}
