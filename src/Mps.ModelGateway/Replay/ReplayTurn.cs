// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Extensions.AI;

using Mps.ModelGateway.Cost;

namespace Mps.ModelGateway.Replay;

/// <summary>
/// One recorded completion. Replaying it does not call a live model.
/// </summary>
public sealed class ReplayTurn
{
    public ReplayTurn(
        string modelId,
        string? text,
        IReadOnlyList<ReplayFunctionCall> functionCalls,
        int inputTokens,
        int outputTokens)
    {
        ArgumentNullException.ThrowIfNull(functionCalls);
        if (inputTokens < 0 || outputTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inputTokens), "Token counts cannot be negative.");
        }

        ModelId = string.IsNullOrWhiteSpace(modelId) ? "replay" : modelId.Trim();
        Text = text;
        FunctionCalls = functionCalls.ToArray();
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
    }

    public string ModelId { get; }

    public string? Text { get; }

    public IReadOnlyList<ReplayFunctionCall> FunctionCalls { get; }

    public int InputTokens { get; }

    public int OutputTokens { get; }

    public static ReplayTurn Capture(ChatResponse response, IReadOnlyList<ChatMessage> prompt)
    {
        ArgumentNullException.ThrowIfNull(response);
        var calls = new List<ReplayFunctionCall>();
        foreach (var message in response.Messages)
        {
            foreach (var call in message.Contents.OfType<FunctionCallContent>())
            {
                calls.Add(new ReplayFunctionCall(call.CallId, call.Name, Copy(call.Arguments)));
            }
        }

        var (input, output) = TokenCost.Count(response, prompt);
        return new ReplayTurn(response.ModelId ?? "mock-reasoner", response.Text, calls, input, output);
    }

    public ChatResponse ToChatResponse()
    {
        var contents = new List<AIContent>();
        foreach (var call in FunctionCalls)
        {
            contents.Add(new FunctionCallContent(call.CallId, call.Name, Copy(call.Arguments)));
        }

        if (!string.IsNullOrEmpty(Text))
        {
            contents.Add(new TextContent(Text));
        }

        var message = contents.Count == 0
            ? new ChatMessage(ChatRole.Assistant, string.Empty)
            : new ChatMessage(ChatRole.Assistant, contents);

        return new ChatResponse(message)
        {
            ModelId = ModelId,
            Usage = new UsageDetails
            {
                InputTokenCount = InputTokens,
                OutputTokenCount = OutputTokens,
                TotalTokenCount = (long)InputTokens + OutputTokens,
            },
        };
    }

    private static Dictionary<string, object?> Copy(IEnumerable<KeyValuePair<string, object?>>? arguments)
    {
        var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (arguments is null)
        {
            return copy;
        }

        foreach (var pair in arguments)
        {
            copy[pair.Key] = pair.Value;
        }

        return copy;
    }
}
