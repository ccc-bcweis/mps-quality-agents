// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

namespace Mps.ModelGateway.Adapters;

/// <summary>
/// Rule-based chat client. The same request yields the same response because the reasoner is deterministic.
/// </summary>
public sealed class MockReasonerChatClient : IChatClient
{
    private readonly IMockReasoner _reasoner;

    public MockReasonerChatClient(IMockReasoner reasoner)
    {
        ArgumentNullException.ThrowIfNull(reasoner);
        _reasoner = reasoner;
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        cancellationToken.ThrowIfCancellationRequested();
        var response = _reasoner.Complete(messages.ToList(), options);
        if (response is null)
        {
            throw new InvalidOperationException("The mock reasoner returned no response.");
        }

        response.ModelId ??= "mock-reasoner";
        return Task.FromResult(response);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        foreach (var update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
    }
}
