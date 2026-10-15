// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Metadata;
using Mps.Domain;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Telemetry;

namespace Mps.Application.Spc;

public sealed class SpcSample
{
    public SpcSample(
        ToolId toolId,
        string? chamberId,
        string parameter,
        ControlBaseline baseline,
        IReadOnlyList<TelemetryPoint> points)
    {
        if (toolId.IsEmpty)
        {
            throw new DomainRuleException("Tool id is required.");
        }

        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(points);
        ToolId = toolId;
        ChamberId = string.IsNullOrWhiteSpace(chamberId) ? null : chamberId.Trim();
        Parameter = parameter?.Trim() ?? string.Empty;
        if (Parameter.Length == 0)
        {
            throw new DomainRuleException("Parameter is required.");
        }

        Baseline = baseline;
        Points = points.ToArray();
    }

    public ToolId ToolId { get; }

    public string? ChamberId { get; }

    public string Parameter { get; }

    public ControlBaseline Baseline { get; }

    public IReadOnlyList<TelemetryPoint> Points { get; }

    /// <summary>Optional engineering-unit context so the ticket title can say the real value and limit. Points may be z-scores.</summary>
    public SpcDisplay? Display { get; init; }
}

/// <summary>Real-unit context for a sample whose points are z-scores: the nominal, one sigma, the unit, and the tool family label.</summary>
public sealed record SpcDisplay(double Nominal, double Sigma, string Unit, string? FamilyLabel);

public sealed class SpcAlertOutcome
{
    public SpcAlertOutcome(
        IReadOnlyList<SpcViolation> violations,
        Alert? alert,
        Incident? incident,
        Ticket? ticket,
        bool debounced,
        RuleDecision? decision = null)
    {
        ArgumentNullException.ThrowIfNull(violations);
        Violations = violations.ToArray();
        Alert = alert;
        Incident = incident;
        Ticket = ticket;
        Debounced = debounced;
        Decision = decision;
    }

    public IReadOnlyList<SpcViolation> Violations { get; }

    public Alert? Alert { get; }

    public Incident? Incident { get; }

    public Ticket? Ticket { get; }

    public bool Debounced { get; }

    public RuleDecision? Decision { get; }
}
