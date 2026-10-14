// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.ClientModel;

using Microsoft.Extensions.AI;

using OpenAI.Chat;

namespace Mps.ModelGateway.Adapters;

/// <summary>
/// OpenAI adapter. Construction does not call the network. The API key is an argument and is never written to source.
/// </summary>
public static class OpenAiChatClient
{
    public static IChatClient Create(string model, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("OpenAI model name is required.", nameof(model));
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("OpenAI API key is required.", nameof(apiKey));
        }

        var client = new ChatClient(model.Trim(), new ApiKeyCredential(apiKey));
        return client.AsIChatClient();
    }
}
