// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;

namespace Mps.Domain.Telemetry;

public sealed record TelemetryPoint
{
    public TelemetryPoint(DateTimeOffset at, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new DomainRuleException("Telemetry value must be a finite number.");
        }

        At = at;
        Value = value;
    }

    public DateTimeOffset At { get; }

    public double Value { get; }
}

public sealed class TelemetryWindow
{
    public TelemetryWindow(
        ToolId toolId,
        string parameter,
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyList<TelemetryPoint> points)
    {
        if (toolId.IsEmpty)
        {
            throw new DomainRuleException("Tool id is required.");
        }

        if (to < from)
        {
            throw new DomainRuleException("Telemetry window end is before its start.");
        }

        ArgumentNullException.ThrowIfNull(points);
        ToolId = toolId;
        Parameter = RequiredText.Value(parameter, "Parameter is required.");
        From = from;
        To = to;
        Points = points.ToArray();
    }

    public ToolId ToolId { get; }

    public string Parameter { get; }

    public DateTimeOffset From { get; }

    public DateTimeOffset To { get; }

    public IReadOnlyList<TelemetryPoint> Points { get; }
}

public sealed record SpcViolation
{
    public SpcViolation(
        string rule,
        ToolId toolId,
        string? chamberId,
        string parameter,
        DateTimeOffset at,
        double value,
        double sigma,
        Severity severity)
    {
        if (toolId.IsEmpty)
        {
            throw new DomainRuleException("Tool id is required.");
        }

        if (double.IsNaN(value) || double.IsInfinity(value) || double.IsNaN(sigma) || double.IsInfinity(sigma))
        {
            throw new DomainRuleException("SPC value and sigma must be finite numbers.");
        }

        Rule = RequiredText.Value(rule, "SPC rule is required.");
        ToolId = toolId;
        ChamberId = string.IsNullOrWhiteSpace(chamberId) ? null : chamberId.Trim();
        Parameter = RequiredText.Value(parameter, "Parameter is required.");
        At = at;
        Value = value;
        Sigma = sigma;
        Severity = severity;
    }

    public string Rule { get; }

    public ToolId ToolId { get; }

    public string? ChamberId { get; }

    public string Parameter { get; }

    public DateTimeOffset At { get; }

    public double Value { get; }

    public double Sigma { get; }

    public Severity Severity { get; }
}
