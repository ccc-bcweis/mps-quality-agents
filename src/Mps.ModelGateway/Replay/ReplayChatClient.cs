// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

namespace Mps.ModelGateway.Replay;

/// <summary>
/// Serves recorded completions. A miss throws <see cref="ReplayMissException"/> and does not call a network.
/// </summary>
public sealed class ReplayChatClient : IChatClient
{
    private readonly IReplayCatalog _catalog;

    public ReplayChatClient(IReplayCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = messages.ToList();
        var hash = PromptHasher.Hash(snapshot, options);
        if (!_catalog.TryGet(hash, out var turn))
        {
            throw new ReplayMissException(hash);
        }

        return Task.FromResult(turn.ToChatResponse());
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
