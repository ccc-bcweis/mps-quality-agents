// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Extensions.AI;

using Mps.Agents.Prompts;
using Mps.Agents.Ranking;

namespace Mps.Agents.Resolution;

/// <summary>
/// Asks the model for one JSON object. A function call or invalid JSON is retried once, then escalated.
/// </summary>
public sealed class ResolutionAgent : IResolutionAgent
{
    public const int MaxModelCalls = 2;

    private readonly IChatClient _chat;
    private readonly PromptCatalog _prompts;

    public ResolutionAgent(IChatClient chat, PromptCatalog prompts)
    {
        ArgumentNullException.ThrowIfNull(chat);
        ArgumentNullException.ThrowIfNull(prompts);
        _chat = chat;
        _prompts = prompts;
    }

    public async Task<ResolutionResult> ResolveAsync(ResolutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var prompt = _prompts.Resolution;
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, prompt.Body),
            new(ChatRole.User, ResolutionJson.SerializeRequest(request)),
        };
        string? problem = null;

        for (var call = 1; call <= MaxModelCalls; call++)
        {
            if (call == 2)
            {
                messages.Add(new ChatMessage(ChatRole.User, "The previous reply was not the JSON object (" + (problem ?? "not JSON") + "). Return only the JSON object."));
            }

            var response = await _chat.GetResponseAsync(messages, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (response.Messages.Count > 0)
            {
                messages.AddRange(response.Messages);
            }

            if (response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().Any())
            {
                problem = "resolution agent emitted a function call";
                continue;
            }

            if (!ResolutionJson.TryParseClaim(response.Text, out var claim, out var escalateReason, out var error))
            {
                problem = error;
                continue;
            }

            if (escalateReason is not null)
            {
                return ResolutionResult.Escalate(escalateReason, call, prompt.Id, prompt.Version);
            }

            return ResolutionResult.Ready(claim!, call, prompt.Id, prompt.Version);
        }

        return ResolutionResult.Escalate(problem ?? "resolution reply is not JSON", MaxModelCalls, prompt.Id, prompt.Version);
    }
}
