// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.ClientModel;

using Microsoft.Extensions.AI;

using OpenAI;
using OpenAI.Chat;

namespace Mps.ModelGateway.Adapters;

/// <summary>
/// Azure OpenAI adapter. It targets the resource's OpenAI v1 endpoint and the deployment name.
/// Construction does not call the network. The API key is an argument and is never written to source.
/// </summary>
public static class AzureOpenAiChatClient
{
    public static IChatClient Create(Uri endpoint, string deployment, string apiKey)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri)
        {
            throw new ArgumentException("Azure OpenAI endpoint must be an absolute URI.", nameof(endpoint));
        }

        if (string.IsNullOrWhiteSpace(deployment))
        {
            throw new ArgumentException("Azure OpenAI deployment name is required.", nameof(deployment));
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("Azure OpenAI API key is required.", nameof(apiKey));
        }

        var root = endpoint.ToString().TrimEnd('/');
        var v1 = root.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase)
            ? new Uri(root + "/")
            : new Uri(root + "/openai/v1/");
        var client = new ChatClient(
            deployment.Trim(),
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { Endpoint = v1 });
        return client.AsIChatClient();
    }
}
