// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.ClientModel;

using Microsoft.Extensions.AI;

namespace Mps.Api.Live;

/// <summary>
/// The OpenAI SDK reports transport and service failures as ClientResultException. The gateway falls back on
/// HttpRequestException, so this client logs the real failure and translates it. A demo never breaks on a model outage.
/// </summary>
public sealed partial class LiveFailureTranslatingChatClient : DelegatingChatClient
{
    [LoggerMessage(EventId = 7001, Level = LogLevel.Information, Message = "Live model call served by {Model}: {Input} tokens in, {Output} tokens out.")]
    private static partial void Served(ILogger logger, string? model, long? input, long? output);

    [LoggerMessage(EventId = 7002, Level = LogLevel.Warning, Message = "Live model call failed with status {Status}; the gateway will fall back.")]
    private static partial void Failed(ILogger logger, Exception error, int? status);

    private readonly ILogger _logger;

    public LiveFailureTranslatingChatClient(IChatClient inner, ILogger logger)
        : base(inner)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await base.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
            Served(_logger, response.ModelId, response.Usage?.InputTokenCount, response.Usage?.OutputTokenCount);
            return response;
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested && error is ClientResultException or AggregateException)
        {
            var status = (error as ClientResultException)?.Status ?? (error.InnerException as ClientResultException)?.Status;
            Failed(_logger, error, status);
            throw new HttpRequestException("Live model call failed: " + error.Message, error);
        }
    }
}
