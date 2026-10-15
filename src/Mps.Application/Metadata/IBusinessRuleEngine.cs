// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Incidents;

namespace Mps.Application.Metadata;

public enum RuleEffect
{
    OpenTicket,
    SetPriority,
    RouteToQueue,
    EscalateToHuman,
    AutoInvestigate,
}

public sealed class RuleContext
{
    public string? AlertRuleId { get; init; }

    public Severity? Severity { get; init; }

    public string? ParameterId { get; init; }

    public string? ParameterClass { get; init; }

    public int RepeatCount { get; init; }

    public string? SiteId { get; init; }

    public string? ToolId { get; init; }

    public double? Confidence { get; init; }

    public string? EvidenceSufficiency { get; init; }

    public string? Channel { get; init; }
}

public sealed class RuleDecision
{
    public RuleDecision(
        string ruleId,
        string ruleName,
        string why,
        IReadOnlyList<RuleEffect> effects,
        string? priority,
        string? queue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleName);
        ArgumentNullException.ThrowIfNull(effects);
        RuleId = ruleId;
        RuleName = ruleName;
        Why = why ?? string.Empty;
        Effects = effects;
        Priority = priority;
        Queue = queue;
    }

    public string RuleId { get; }

    public string RuleName { get; }

    public string Why { get; }

    public IReadOnlyList<RuleEffect> Effects { get; }

    public string? Priority { get; }

    public string? Queue { get; }

    public bool OpensTicket => Effects.Contains(RuleEffect.OpenTicket);

    public bool EscalatesToHuman => Effects.Contains(RuleEffect.EscalateToHuman);

    public bool AutoInvestigates => Effects.Contains(RuleEffect.AutoInvestigate);
}

public interface IBusinessRuleEngine
{
    RuleDecision Evaluate(RuleContext context);
}
