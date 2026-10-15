// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Diagnostics;

using Mps.Application.Metrics;
using Mps.Domain.Incidents;

namespace Mps.Application.Metadata;

public sealed class BusinessRuleEngine : IBusinessRuleEngine
{
    private readonly IMetadataStore _store;
    private readonly IReadOnlyList<BusinessRule> _rules;

    public BusinessRuleEngine(IMetadataStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _rules = store.Rules.OrderBy(rule => rule.Order).ToArray();
    }

    public RuleDecision Evaluate(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (var rule in _rules)
        {
            if (Matches(rule, context))
            {
                var decision = ToDecision(rule);
                WriteTrace(decision);
                return decision;
            }
        }

        throw new MetadataSpecException(["No business rule matched this case."]);
    }

    private bool Matches(BusinessRule rule, RuleContext context)
    {
        var when = rule.When;
        if (when.IsEmpty())
        {
            return true;
        }

        if (when.RequiresAlert == true && string.IsNullOrWhiteSpace(context.AlertRuleId))
        {
            return false;
        }

        if (when.RequiresAlert == false && !string.IsNullOrWhiteSpace(context.AlertRuleId))
        {
            return false;
        }

        if (!SeverityFits(when.Severity, context.Severity, exact: true))
        {
            return false;
        }

        if (!SeverityFits(when.SeverityAtLeast, context.Severity, atLeast: true))
        {
            return false;
        }

        if (!SeverityFits(when.SeverityAtMost, context.Severity, atMost: true))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(when.AlertRuleId) && !AlertMatches(when.AlertRuleId, context.AlertRuleId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(when.ToolId) && !Same(when.ToolId, context.ToolId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(when.ParameterId) && !Same(when.ParameterId, context.ParameterId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(when.ParameterClass) && !Same(when.ParameterClass, ResolveClass(context)))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(when.SiteId) && !Same(when.SiteId, ResolveSite(context)))
        {
            return false;
        }

        if (when.RepeatAtLeast is not null && context.RepeatCount < when.RepeatAtLeast.Value)
        {
            return false;
        }

        if (when.ConfidenceBelow is not null
            && (context.Confidence is null || context.Confidence.Value >= when.ConfidenceBelow.Value))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(when.EvidenceSufficiency) && !Same(when.EvidenceSufficiency, context.EvidenceSufficiency))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(when.Channel) && !Same(when.Channel, context.Channel))
        {
            return false;
        }

        return true;
    }

    private string? ResolveClass(RuleContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.ParameterClass))
        {
            return context.ParameterClass;
        }

        if (string.IsNullOrWhiteSpace(context.ToolId) || string.IsNullOrWhiteSpace(context.ParameterId))
        {
            return null;
        }

        return _store.ParameterClass(context.ToolId, context.ParameterId);
    }

    private string? ResolveSite(RuleContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.SiteId))
        {
            return context.SiteId;
        }

        if (string.IsNullOrWhiteSpace(context.ToolId))
        {
            return null;
        }

        return _store.SiteIdForTool(context.ToolId);
    }

    private static bool AlertMatches(string expected, string? actual)
    {
        if (string.IsNullOrWhiteSpace(actual))
        {
            return false;
        }

        foreach (var token in actual.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SeverityFits(string? expected, Severity? actual, bool exact = false, bool atLeast = false, bool atMost = false)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        if (actual is null || !Enum.TryParse<Severity>(expected, ignoreCase: true, out var parsed))
        {
            return false;
        }

        if (exact)
        {
            return actual.Value == parsed;
        }

        if (atLeast)
        {
            return actual.Value >= parsed;
        }

        if (atMost)
        {
            return actual.Value <= parsed;
        }

        return false;
    }

    private static bool Same(string expected, string? actual)
    {
        return !string.IsNullOrWhiteSpace(actual) && actual.Trim().Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static RuleDecision ToDecision(BusinessRule rule)
    {
        var effects = new List<RuleEffect>();
        string? priority = null;
        string? queue = null;
        foreach (var action in rule.Actions)
        {
            switch (action.Type)
            {
                case "open-ticket":
                    effects.Add(RuleEffect.OpenTicket);
                    break;
                case "set-priority":
                    effects.Add(RuleEffect.SetPriority);
                    priority = action.Priority;
                    break;
                case "route-to-queue":
                    effects.Add(RuleEffect.RouteToQueue);
                    queue = action.Queue;
                    break;
                case "escalate-to-human":
                    effects.Add(RuleEffect.EscalateToHuman);
                    break;
                case "auto-investigate":
                    effects.Add(RuleEffect.AutoInvestigate);
                    break;
                default:
                    throw new MetadataSpecException(["Rule " + rule.Id + " has unknown action " + action.Type + "."]);
            }
        }

        return new RuleDecision(rule.Id, rule.Name, rule.Why, effects, priority, queue);
    }

    private static void WriteTrace(RuleDecision decision)
    {
        using var activity = QualityDiagnostics.ActivitySource.StartActivity("business-rule");
        activity?.SetTag("mps.rule.id", decision.RuleId);
        activity?.SetTag("mps.rule.name", decision.RuleName);
        if (decision.Queue is not null)
        {
            activity?.SetTag("mps.rule.queue", decision.Queue);
        }

        if (decision.Priority is not null)
        {
            activity?.SetTag("mps.rule.priority", decision.Priority);
        }

        activity?.SetTag("mps.rule.effects", string.Join(',', decision.Effects));
    }
}
