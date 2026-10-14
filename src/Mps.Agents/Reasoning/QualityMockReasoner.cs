// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Extensions.AI;

using Mps.Agents.Ranking;
using Mps.Agents.Resolution;
using Mps.Agents.Tools;
using Mps.ModelGateway.Adapters;

namespace Mps.Agents.Reasoning;

/// <summary>
/// Deterministic investigation tool plan and resolution JSON. The resolution object includes whyItMatters for every cited evidence id, signalId on sensor signals, and an evidence matrix for all four root-cause classes. The gateway stays free of root-cause rules.
/// </summary>
public sealed class QualityMockReasoner : IMockReasoner
{
    public ChatResponse Complete(IReadOnlyList<ChatMessage> messages, ChatOptions? options)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (IsResolution(messages))
        {
            return Text(Resolve(messages));
        }

        var completed = messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Count();
        if (completed >= AgentToolNames.DefaultPlan.Count)
        {
            return Text("Evidence gathered.");
        }

        var name = AgentToolNames.DefaultPlan[completed];
        var call = new FunctionCallContent("call-" + completed.ToString(System.Globalization.CultureInfo.InvariantCulture), name, new Dictionary<string, object?>());
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, [call]))
        {
            ModelId = "mock-reasoner",
            Usage = Usage(),
        };
    }

    private static string Resolve(IReadOnlyList<ChatMessage> messages)
    {
        var json = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text;
        if (json is null || !ResolutionJson.TryReadRequest(json, out var request) || request is null)
        {
            return "{\"outcome\":\"escalate\",\"reason\":\"resolution request is missing\"}";
        }

        return ResolutionJson.SerializeDecision(RootCauseRanker.Rank(request));
    }

    private static bool IsResolution(IReadOnlyList<ChatMessage> messages)
    {
        return messages.Any(message => message.Role == ChatRole.System && (message.Text?.Contains("resolution agent", StringComparison.Ordinal) ?? false));
    }

    private static ChatResponse Text(string text)
    {
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
        {
            ModelId = "mock-reasoner",
            Usage = Usage(),
        };
    }

    private static UsageDetails Usage()
    {
        return new UsageDetails { InputTokenCount = 32, OutputTokenCount = 32, TotalTokenCount = 64 };
    }
}
