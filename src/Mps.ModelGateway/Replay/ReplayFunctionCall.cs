// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.ModelGateway.Replay;

/// <summary>
/// A tool call stored in a replay turn.
/// </summary>
public sealed class ReplayFunctionCall
{
    public ReplayFunctionCall(string callId, string name, IReadOnlyDictionary<string, object?>? arguments)
    {
        if (string.IsNullOrWhiteSpace(callId))
        {
            throw new ArgumentException("Call id is required.", nameof(callId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Function name is required.", nameof(name));
        }

        CallId = callId.Trim();
        Name = name.Trim();
        Arguments = arguments ?? new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    public string CallId { get; }

    public string Name { get; }

    public IReadOnlyDictionary<string, object?> Arguments { get; }
}
