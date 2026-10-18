// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.ClientModel;
using System.ClientModel.Primitives;

using Azure.Identity;

using Microsoft.Extensions.AI;

using OpenAI;
using OpenAI.Chat;

namespace Mps.Api.Live;

/// <summary>
/// Builds the live chat client for the model mode. Construction never calls the network. Every live client has a
/// network timeout and no SDK retries, so an unreachable model fails over to saved answers within seconds.
/// Keys come only from configuration (the untracked .env or the environment) and are never logged.
/// </summary>
public static class ModelClientFactory
{
    /// <summary>The live client for OpenAI or Azure modes, wrapped so failures reach the gateway as fall-back signals; null offline.</summary>
    public static IChatClient? Create(ModelModeSettings settings, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);
        var inner = settings.Mode switch
        {
            ModelMode.OpenAI => OpenAi(settings),
            ModelMode.Azure => Azure(settings),
            _ => null,
        };
        return inner is null ? null : new LiveFailureTranslatingChatClient(inner, logger);
    }

    /// <summary>Options shared by every live client: the configured timeout and no SDK-level retries (the gateway decides).</summary>
    public static OpenAIClientOptions Options(ModelModeSettings settings, Uri? endpoint)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var options = new OpenAIClientOptions
        {
            NetworkTimeout = settings.Timeout,
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
        };
        if (endpoint is not null)
        {
            options.Endpoint = endpoint;
        }

        return options;
    }

    private static IChatClient OpenAi(ModelModeSettings settings)
    {
        var endpoint = string.IsNullOrWhiteSpace(settings.BaseUrl) ? null : new Uri(settings.BaseUrl);
        var client = new ChatClient(settings.Model!, new ApiKeyCredential(settings.ApiKey!), Options(settings, endpoint));
        return client.AsIChatClient();
    }

    private static IChatClient Azure(ModelModeSettings settings)
    {
        var root = settings.Endpoint!.TrimEnd('/');
        var v1 = root.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase) ? new Uri(root + "/") : new Uri(root + "/openai/v1/");
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return new ChatClient(settings.Deployment!, new ApiKeyCredential(settings.ApiKey), Options(settings, v1)).AsIChatClient();
        }

        // Managed identity in Azure (AZURE_CLIENT_ID picks the user-assigned identity), or `az login` on a laptop.
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ManagedIdentityClientId = settings.ManagedIdentityClientId,
        });
        return ManagedIdentityChatClient.Create(new Uri(root), settings.Deployment!, credential, Options(settings, null));
    }
}
