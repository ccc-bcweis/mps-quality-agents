// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.ModelGateway.Cost;

/// <summary>
/// One model completion recorded by the gateway. Amounts are US dollars.
/// </summary>
public sealed record CostEntry
{
    public CostEntry(
        string callId,
        string route,
        string model,
        int inputTokens,
        int outputTokens,
        decimal costUsd,
        DateTimeOffset occurredAt,
        bool liveCallRefused,
        string? conversationId)
    {
        if (string.IsNullOrWhiteSpace(callId))
        {
            throw new ArgumentException("Call id is required.", nameof(callId));
        }

        if (string.IsNullOrWhiteSpace(route))
        {
            throw new ArgumentException("Route is required.", nameof(route));
        }

        if (inputTokens < 0 || outputTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inputTokens), "Token counts cannot be negative.");
        }

        if (costUsd < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(costUsd), "Cost cannot be negative.");
        }

        CallId = callId.Trim();
        Route = route.Trim();
        Model = string.IsNullOrWhiteSpace(model) ? Route : model.Trim();
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        CostUsd = costUsd;
        OccurredAt = occurredAt;
        LiveCallRefused = liveCallRefused;
        ConversationId = string.IsNullOrWhiteSpace(conversationId) ? null : conversationId.Trim();
    }

    public string CallId { get; }

    public string Route { get; }

    public string Model { get; }

    public int InputTokens { get; }

    public int OutputTokens { get; }

    public decimal CostUsd { get; }

    public DateTimeOffset OccurredAt { get; }

    public bool LiveCallRefused { get; }

    public string? ConversationId { get; }
}
