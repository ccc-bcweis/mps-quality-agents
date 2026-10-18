// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Extensions.AI;

using Mps.Agents.Hosting;
using Mps.Agents.Prompts;
using Mps.Agents.Ranking;
using Mps.ModelGateway.Adapters;
using Mps.ModelGateway.Replay;

namespace Mps.Agents.Resolution;

/// <summary>
/// Asks the model for one JSON object. A live model sees short handles. Ids in the answer are mapped back before the
/// claim is returned. A function call or invalid JSON is retried once, then escalated.
/// </summary>
public sealed class ResolutionAgent : IResolutionAgent
{
    /// <summary>Model calls per resolution, JSON repair included (investigation.yaml resolution.maxModelCalls).</summary>
    public static int MaxModelCalls => Mps.Agents.Investigation.InvestigationTuning.Shared.Resolution.MaxModelCalls;

    private readonly IChatClient _chat;
    private readonly Microsoft.Agents.AI.ChatClientAgent _agent;
    private readonly PromptCatalog _prompts;

    public ResolutionAgent(IChatClient chat, PromptCatalog prompts)
    {
        ArgumentNullException.ThrowIfNull(chat);
        ArgumentNullException.ThrowIfNull(prompts);
        _chat = chat;
        _agent = QualityAgents.Create(chat, QualityAgents.ResolutionName, "Ranks the root cause from the evidence pack as one JSON object.");
        _prompts = prompts;
    }

    public async Task<ResolutionResult> ResolveAsync(ResolutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var prompt = _prompts.Resolution;
        var outbound = SendsHandles ? request.WithHandles() : request;
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, prompt.Body),
            new(ChatRole.User, ResolutionJson.SerializeRequest(outbound)),
        };
        string? problem = null;

        for (var call = 1; call <= MaxModelCalls; call++)
        {
            if (call == 2)
            {
                messages.Add(new ChatMessage(ChatRole.User, "The previous reply was not the JSON object (" + (problem ?? "not JSON") + "). Return only the JSON object."));
            }

            var response = await QualityAgents.TurnAsync(_agent, messages, null, cancellationToken).ConfigureAwait(false);
            if (response.Messages.Count > 0)
            {
                messages.AddRange(response.Messages);
            }

            if (response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().Any())
            {
                problem = "resolution agent emitted a function call";
                continue;
            }

            var text = ResolutionJson.RewriteAnswerIds(response.Text, outbound.ResolveHandle);
            if (!ResolutionJson.TryParseClaim(text, out var claim, out var escalateReason, out var error))
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

    /// <summary>Replay and the offline ranker keep real ids. Every other client, including the live model, sees handles.</summary>
    private bool SendsHandles => _chat is not MockReasonerChatClient and not ReplayChatClient;
}
