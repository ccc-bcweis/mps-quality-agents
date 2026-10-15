// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Persistence;

public sealed class ToolRow
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Family { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string Bay { get; set; } = string.Empty;

    public List<ChamberRow> Chambers { get; set; } = [];
}

public sealed class ChamberRow
{
    public string ToolId { get; set; } = string.Empty;

    public string ChamberId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public ToolRow? Tool { get; set; }
}

public sealed class AlertRow
{
    public Guid Id { get; set; }

    public string ToolId { get; set; } = string.Empty;

    public string? ChamberId { get; set; }

    public string Parameter { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Rule { get; set; } = string.Empty;

    public DateTime RaisedAt { get; set; }

    public string CorrelationId { get; set; } = string.Empty;
}

public sealed class IncidentRow
{
    public Guid Id { get; set; }

    public string ToolId { get; set; } = string.Empty;

    public string? ChamberId { get; set; }

    public string Parameter { get; set; } = string.Empty;

    public string ParameterKey { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    public DateTime OpenedAt { get; set; }

    public string Stage { get; set; } = string.Empty;

    public bool EscalationRequired { get; set; }

    public bool HasEscalated { get; set; }
}

public sealed class TicketRow
{
    public Guid Id { get; set; }

    public string Number { get; set; } = string.Empty;

    public Guid IncidentId { get; set; }

    public string ToolId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public DateTime OpenedAt { get; set; }
}

public sealed class TicketSequenceRow
{
    public int Id { get; set; }

    public int NextNumber { get; set; }
}

public sealed class InvestigationRow
{
    public Guid Id { get; set; }

    public Guid IncidentId { get; set; }

    /// <summary>Start order for one incident. A later investigation has a higher sequence.</summary>
    public int Sequence { get; set; }

    public string Stage { get; set; } = string.Empty;

    public string? EscalationReason { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public string EvidenceJson { get; set; } = "[]";

    public string HypothesesJson { get; set; } = "[]";

    public string EvidenceMatrixJson { get; set; } = "[]";
}

public sealed class CapaRow
{
    public Guid Id { get; set; }

    public Guid IncidentId { get; set; }

    public string Summary { get; set; } = string.Empty;

    public Guid ApprovalTokenId { get; set; }

    public string ActionHash { get; set; } = string.Empty;

    public string Stage { get; set; } = string.Empty;

    public DateTime OpenedAt { get; set; }

    public string ExternalId { get; set; } = string.Empty;
}

public sealed class FaultRow
{
    public Guid Id { get; set; }

    public string ToolId { get; set; } = string.Empty;

    public string? ChamberId { get; set; }

    public string Parameter { get; set; } = string.Empty;

    public string Shape { get; set; } = string.Empty;

    public double Magnitude { get; set; }

    public string RootCauseCategory { get; set; } = string.Empty;

    public bool Confounders { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public DateTime AcceptedAt { get; set; }
}

public sealed class ApprovalRow
{
    public Guid TokenId { get; set; }

    /// <summary>Monotonic per investigation so a retry sorts after the attempt it follows, even when the clock does not move.</summary>
    public int Sequence { get; set; }

    public Guid InvestigationId { get; set; }

    public Guid IncidentId { get; set; }

    public string ApproverId { get; set; } = string.Empty;

    public string ActionHash { get; set; } = string.Empty;

    public string Signature { get; set; } = string.Empty;

    public DateTime IssuedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool Consumed { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string Decision { get; set; } = string.Empty;

    public string Outcome { get; set; } = string.Empty;

    public string? Comment { get; set; }

    public Guid? CapaId { get; set; }

    public string? ExternalCapaId { get; set; }

    public int Attempts { get; set; }

    public string? FailureDetail { get; set; }
}
