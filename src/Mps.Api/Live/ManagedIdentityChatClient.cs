// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.ClientModel;
using System.ClientModel.Primitives;

using Azure.Core;

using Microsoft.Extensions.AI;

using OpenAI;
using OpenAI.Chat;

namespace Mps.Api.Live;

/// <summary>
/// Azure OpenAI chat client authenticated with a managed identity token. No key exists; local auth is off on the account.
/// </summary>
public static class ManagedIdentityChatClient
{
    private const string Scope = "https://cognitiveservices.azure.com/.default";

    public static IChatClient Create(Uri endpoint, string deployment, TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(deployment);
        ArgumentNullException.ThrowIfNull(credential);
        var root = endpoint.ToString().TrimEnd('/');
        var options = new OpenAIClientOptions { Endpoint = new Uri(root + "/openai/v1/") };
        options.AddPolicy(new BearerPolicy(credential), PipelinePosition.PerTry);

        // The placeholder key is replaced by the bearer policy on every attempt.
        var client = new ChatClient(deployment.Trim(), new ApiKeyCredential("managed-identity"), options);
        return client.AsIChatClient();
    }

    private sealed class BearerPolicy : PipelinePolicy
    {
        private readonly TokenCredential _credential;
        private AccessToken _token;

        public BearerPolicy(TokenCredential credential)
        {
            _credential = credential;
        }

        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            Apply(message, Token(CancellationToken.None));
            ProcessNext(message, pipeline, currentIndex);
        }

        public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            Apply(message, await TokenAsync(message.CancellationToken).ConfigureAwait(false));
            await ProcessNextAsync(message, pipeline, currentIndex).ConfigureAwait(false);
        }

        private static void Apply(PipelineMessage message, string token)
        {
            message.Request.Headers.Set("Authorization", "Bearer " + token);
            message.Request.Headers.Remove("api-key");
        }

        private string Token(CancellationToken cancellationToken)
        {
            if (Fresh())
            {
                return _token.Token;
            }

            _token = _credential.GetToken(new TokenRequestContext([Scope]), cancellationToken);
            return _token.Token;
        }

        private async ValueTask<string> TokenAsync(CancellationToken cancellationToken)
        {
            if (Fresh())
            {
                return _token.Token;
            }

            _token = await _credential.GetTokenAsync(new TokenRequestContext([Scope]), cancellationToken).ConfigureAwait(false);
            return _token.Token;
        }

        private bool Fresh() => !string.IsNullOrEmpty(_token.Token) && _token.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5);
    }
}
