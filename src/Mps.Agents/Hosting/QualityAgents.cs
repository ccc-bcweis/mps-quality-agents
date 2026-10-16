// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Mps.Agents.Hosting;

/// <summary>
/// Wraps the model client as a Microsoft Agent Framework <see cref="ChatClientAgent"/>.
/// The client is used as provided: no automatic function invocation, so the agents keep their own
/// tool loop, call cap, and time limit. The system prompt travels in the message list.
/// </summary>
public static class QualityAgents
{
    public const string InvestigationName = "investigation-agent";

    public const string ResolutionName = "resolution-agent";

    public static ChatClientAgent Create(IChatClient chat, string name, string description)
    {
        ArgumentNullException.ThrowIfNull(chat);
        return new ChatClientAgent(chat, new ChatClientAgentOptions
        {
            Id = name,
            Name = name,
            Description = description,
            UseProvidedChatClientAsIs = true,
        });
    }

    /// <summary>Runs one model turn through the agent and returns the reply messages.</summary>
    public static async Task<(IList<ChatMessage> Messages, string Text)> TurnAsync(
        AIAgent agent,
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        var run = options is null ? null : new ChatClientAgentRunOptions(options);
        var response = await agent.RunAsync(messages, session: null, options: run, cancellationToken: cancellationToken).ConfigureAwait(false);
        return (response.Messages, response.Text);
    }
}
