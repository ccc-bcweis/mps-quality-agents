// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Extensions.AI;

namespace Mps.ModelGateway.Adapters;

/// <summary>
/// Adapts a function to <see cref="IMockReasoner"/>.
/// </summary>
public sealed class DelegateMockReasoner : IMockReasoner
{
    private readonly Func<IReadOnlyList<ChatMessage>, ChatOptions?, ChatResponse> _complete;

    public DelegateMockReasoner(Func<IReadOnlyList<ChatMessage>, ChatOptions?, ChatResponse> complete)
    {
        ArgumentNullException.ThrowIfNull(complete);
        _complete = complete;
    }

    public ChatResponse Complete(IReadOnlyList<ChatMessage> messages, ChatOptions? options)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return _complete(messages, options);
    }
}
