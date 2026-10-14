// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Extensions.AI;

namespace Mps.ModelGateway.Adapters;

/// <summary>
/// Deterministic stand-in for a live model. The gateway has no business rules; callers supply the reasoner.
/// </summary>
public interface IMockReasoner
{
    ChatResponse Complete(IReadOnlyList<ChatMessage> messages, ChatOptions? options);
}
