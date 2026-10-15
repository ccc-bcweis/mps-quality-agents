// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Extensions.AI;

using Mps.Agents.Evidence;
using Mps.Agents.Explain;
using Mps.Agents.Impact;
using Mps.Agents.Prompts;
using Mps.Agents.Tools;
using Mps.Application.Printouts;
using Mps.Domain.Incidents;

namespace Mps.Agents.Investigation;

/// <summary>
/// Runs the read-only tool plan. The evidence pack is built from tool results.
/// </summary>
public sealed class InvestigationAgent : IInvestigationAgent
{
    private readonly ReadOnlyToolSession _tools;
    private readonly IChatClient _chat;
    private readonly PromptCatalog _prompts;
    private readonly TimeProvider _time;
    private readonly InvestigationLimits _limits;
    private readonly IManualMeasurementLog? _measurements;

    public InvestigationAgent(
        ReadOnlyToolSession tools,
        IChatClient chat,
        PromptCatalog prompts,
        TimeProvider? time = null,
        InvestigationLimits? limits = null,
        IManualMeasurementLog? measurements = null)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(chat);
        ArgumentNullException.ThrowIfNull(prompts);
        _tools = tools;
        _chat = chat;
        _prompts = prompts;
        _time = time ?? TimeProvider.System;
        _limits = limits ?? new InvestigationLimits();
        _measurements = measurements;
    }

    public async Task<GatherResult> GatherAsync(Incident incident, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incident);
        var prompt = _prompts.Investigation;
        var from = incident.OpenedAt.AddDays(-14);
        var until = incident.OpenedAt.AddHours(1);
        var harvest = new ToolHarvest();
        var calls = new List<ToolCallRecord>();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, prompt.Body),
            new(ChatRole.User, Describe(incident, from, until)),
        };
        var started = _time.GetUtcNow();
        var executed = 0;
        var turnLimit = _limits.MaxToolCalls + 2;

        for (var turn = 0; turn < turnLimit && executed < _limits.MaxToolCalls; turn++)
        {
            if (_time.GetUtcNow() - started > _limits.MaxDuration)
            {
                return GatherResult.Stopped("Investigation exceeded 60 seconds.", calls, prompt.Id, prompt.Version);
            }

            var response = await _chat.GetResponseAsync(messages, ToolOptions(), cancellationToken).ConfigureAwait(false);
            if (response.Messages.Count > 0)
            {
                messages.AddRange(response.Messages);
            }

            var functionCalls = response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().ToArray();
            if (functionCalls.Length == 0)
            {
                break;
            }

            foreach (var functionCall in functionCalls)
            {
                if (executed >= _limits.MaxToolCalls)
                {
                    break;
                }

                var name = functionCall.Name;
                var callId = string.IsNullOrWhiteSpace(functionCall.CallId) ? "call-" + calls.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) : functionCall.CallId;
                if (!AgentToolNames.IsAllowed(name))
                {
                    calls.Add(ToolCallRecord.Disallowed(name));
                    messages.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent(callId, "disallowed")]));
                    continue;
                }

                string payload;
                try
                {
                    payload = await _tools.ExecuteAsync(name, incident, from, until, harvest, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    calls.Add(ToolCallRecord.Ran(name));
                    return GatherResult.Stopped("Tool " + name + " failed.", calls, prompt.Id, prompt.Version);
                }

                calls.Add(ToolCallRecord.Ran(name));
                messages.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent(callId, payload)]));
                executed++;
            }
        }

        var facts = EvidencePackFactory.Build(incident, harvest);
        var extra = _measurements?.ForTool(incident.ToolId.Value);
        if (extra is { Count: > 0 })
        {
            var pack = facts.Pack.Including(extra.Select(item => item.Evidence).ToArray(), WhyItMattersCatalog.Shared);
            facts = new GatheredFacts(pack, facts.Observations, facts.Events, facts.Shape);
        }

        ImpactFeatures features;
        if (facts.Observations.Count == 0)
        {
            features = new ImpactFeatures(Mps.Domain.Impact.ImpactScopeKind.Tool, incident.ToolId.Value, facts.Shape, null, null);
        }
        else
        {
            var calculated = ImpactFeatureCalculator.Calculate(incident.OpenedAt, facts.Shape, facts.Observations, facts.Events);
            features = ScopeSelector.Select(calculated, harvest.ReportedScope, facts.Observations);
        }

        return GatherResult.Ready(facts.Pack, features, harvest.ReportedScope, calls, prompt.Id, prompt.Version);
    }

    private static string Describe(Incident incident, DateTimeOffset from, DateTimeOffset until)
    {
        return string.Join(
            '\n',
            "toolId: " + incident.ToolId.Value,
            "parameter: " + incident.Parameter,
            "chamberId: " + (incident.ChamberId ?? string.Empty),
            "from: " + from.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            "until: " + until.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static ChatOptions ToolOptions()
    {
        var tools = new List<AITool>(AgentToolNames.DefaultPlan.Count);
        foreach (var name in AgentToolNames.DefaultPlan)
        {
            tools.Add(AIFunctionFactory.Create(
                () => "read-only",
                new AIFunctionFactoryOptions { Name = name, Description = "Read-only evidence tool." }));
        }

        return new ChatOptions { Tools = tools };
    }
}
