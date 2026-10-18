// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Incidents;

namespace Mps.Application.Spc;

public enum SpcTier
{
    Nominal,
    Watch,
    Alert,
}

public enum SpcAction
{
    None,
    Raise,
    Escalate,
    Update,
    Clear,
}

/// <summary>One scored reading. Z is the plant score for this value against the window in force at <see cref="At"/>.</summary>
public sealed class SpcReading
{
    public SpcReading(
        string toolId,
        string? chamberId,
        string parameter,
        string? kind,
        DateTimeOffset at,
        double z,
        double rawValue,
        double center,
        double sigma,
        double? specLow,
        double? specHigh,
        string? correlationId,
        string? unit)
    {
        ToolId = toolId?.Trim() ?? string.Empty;
        ChamberId = string.IsNullOrWhiteSpace(chamberId) ? null : chamberId.Trim();
        Parameter = parameter?.Trim() ?? string.Empty;
        Kind = string.IsNullOrWhiteSpace(kind) ? null : kind.Trim();
        At = at;
        Z = z;
        RawValue = rawValue;
        Center = center;
        Sigma = sigma;
        SpecLow = specLow;
        SpecHigh = specHigh;
        CorrelationId = correlationId ?? string.Empty;
        Unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();
    }

    public string ToolId { get; }

    public string? ChamberId { get; }

    public string Parameter { get; }

    public string? Kind { get; }

    public DateTimeOffset At { get; }

    public double Z { get; }

    public double RawValue { get; }

    public double Center { get; }

    public double Sigma { get; }

    public double? SpecLow { get; }

    public double? SpecHigh { get; }

    public string CorrelationId { get; }

    public string? Unit { get; }
}

/// <summary>What one reading did to a series. <see cref="SpcAction.None"/> means the alert store is left alone.</summary>
public sealed class SpcObservation
{
    public SpcObservation(
        SpcReading reading,
        SpcAction action,
        SpcTier tier,
        Severity severity,
        string ruleText,
        IReadOnlyList<string> liveRules,
        string correlationId,
        string clearReason,
        int inControlReadings,
        int quietReadings)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(liveRules);
        Reading = reading;
        Action = action;
        Tier = tier;
        Severity = severity;
        RuleText = ruleText ?? string.Empty;
        LiveRules = liveRules;
        CorrelationId = correlationId ?? string.Empty;
        ClearReason = clearReason ?? string.Empty;
        InControlReadings = inControlReadings;
        QuietReadings = quietReadings;
    }

    public SpcReading Reading { get; }

    public SpcAction Action { get; }

    public SpcTier Tier { get; }

    public Severity Severity { get; }

    public string RuleText { get; }

    public IReadOnlyList<string> LiveRules { get; }

    public string CorrelationId { get; }

    public string ClearReason { get; }

    public int InControlReadings { get; }

    public int QuietReadings { get; }
}

/// <summary>An active alert restored into the engine. Buffers start empty; the rules clear it from later readings.</summary>
public sealed class SpcSeriesSeed
{
    public SpcSeriesSeed(
        string toolId,
        string? chamberId,
        string parameter,
        Severity severity,
        string rule,
        string correlationId,
        DateTimeOffset lastSeenAt)
    {
        ToolId = toolId?.Trim() ?? string.Empty;
        ChamberId = string.IsNullOrWhiteSpace(chamberId) ? null : chamberId.Trim();
        Parameter = parameter?.Trim() ?? string.Empty;
        Severity = severity;
        Rule = rule ?? string.Empty;
        CorrelationId = correlationId ?? string.Empty;
        LastSeenAt = lastSeenAt;
    }

    public string ToolId { get; }

    public string? ChamberId { get; }

    public string Parameter { get; }

    public Severity Severity { get; }

    public string Rule { get; }

    public string CorrelationId { get; }

    public DateTimeOffset LastSeenAt { get; }
}
