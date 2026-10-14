// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Extensions.AI;

namespace Mps.ModelGateway.Cost;

/// <summary>
/// Converts token counts into US dollars. Missing usage is estimated from text length.
/// </summary>
public static class TokenCost
{
    public static decimal Compute(int inputTokens, int outputTokens, decimal inputUsdPerMillion, decimal outputUsdPerMillion)
    {
        if (inputTokens < 0 || outputTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inputTokens), "Token counts cannot be negative.");
        }

        if (inputUsdPerMillion < 0 || outputUsdPerMillion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inputUsdPerMillion), "Token prices cannot be negative.");
        }

        var input = (inputTokens / 1_000_000m) * inputUsdPerMillion;
        var output = (outputTokens / 1_000_000m) * outputUsdPerMillion;
        return decimal.Round(input + output, 6, MidpointRounding.AwayFromZero);
    }

    public static (int InputTokens, int OutputTokens) Count(ChatResponse response, IReadOnlyList<ChatMessage> prompt)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(prompt);

        if (response.Usage?.InputTokenCount is long input && response.Usage.OutputTokenCount is long output)
        {
            return (Clamp(input), Clamp(output));
        }

        return (Estimate(prompt), EstimateText(response.Text));
    }

    private static int Estimate(IReadOnlyList<ChatMessage> messages)
    {
        var characters = 0;
        foreach (var message in messages)
        {
            characters += message.Text?.Length ?? 0;
        }

        return Math.Max(1, characters / 4);
    }

    private static int EstimateText(string? text)
    {
        return Math.Max(1, (text?.Length ?? 0) / 4);
    }

    private static int Clamp(long value)
    {
        if (value <= 0)
        {
            return 0;
        }

        if (value > int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)value;
    }
}
