// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

using Mps.ModelGateway.Adapters;
using Mps.ModelGateway.Cost;
using Mps.ModelGateway.Replay;

namespace Mps.ModelGateway.Routing;

/// <summary>
/// Routes each completion to Azure OpenAI, OpenAI, replay, or the mock reasoner.
/// Live calls stop when the UTC-day ledger reaches the cap, and again while the circuit is open.
/// Completions run one at a time so two callers cannot both pass the cap.
/// </summary>
public sealed class ModelGateway : IChatClient
{
    private readonly ModelGatewayOptions _options;
    private readonly ICostLedger _ledger;
    private readonly IReplayCatalog _catalog;
    private readonly ReplayChatClient _replayClient;
    private readonly MockReasonerChatClient _mockClient;
    private readonly IChatClient? _azureClient;
    private readonly IChatClient? _openAiClient;
    private readonly IScriptedReplayCatalog _scripted;
    private readonly IScenarioHint? _scenarioHint;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private int _consecutiveFailures;

    public ModelGateway(
        ModelGatewayOptions options,
        ICostLedger ledger,
        IReplayCatalog catalog,
        IMockReasoner reasoner,
        TimeProvider timeProvider,
        IChatClient? azureClient = null,
        IChatClient? openAiClient = null,
        IScriptedReplayCatalog? scriptedReplay = null,
        IScenarioHint? scenarioHint = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(reasoner);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (options.DailySpendCapUsd < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Daily spend cap cannot be negative.");
        }

        if (options.InputUsdPerMillionTokens < 0 || options.OutputUsdPerMillionTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Token prices cannot be negative.");
        }

        if (options.LiveRetryCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Live retry count cannot be negative.");
        }

        if (options.CircuitBreakerFailures < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Circuit breaker threshold must be at least 1.");
        }

        if (!float.IsFinite(options.Temperature))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Temperature must be finite.");
        }

        _options = options;
        _ledger = ledger;
        _catalog = catalog;
        _replayClient = new ReplayChatClient(catalog);
        _mockClient = new MockReasonerChatClient(reasoner);
        _azureClient = azureClient;
        _openAiClient = openAiClient;
        _scripted = scriptedReplay ?? new InMemoryScriptedReplayCatalog();
        _scenarioHint = scenarioHint;
        _time = timeProvider;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = Copy(messages);
        var hash = PromptHasher.Hash(snapshot, options);
        var scenarioId = ReadScenarioId(options) ?? _scenarioHint?.CurrentScenarioId;

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (TryScripted(scenarioId, hash, out var scripted))
            {
                return await RecordAsync(
                    new ServedCall(scripted, ModelRoutes.Replay, LiveRefused: false, FellBack: false),
                    snapshot,
                    options,
                    hash,
                    cancellationToken).ConfigureAwait(false);
            }

            var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
            var decision = await DecideAsync(today, cancellationToken).ConfigureAwait(false);
            var served = await DispatchAsync(decision, snapshot, options, cancellationToken).ConfigureAwait(false);
            return await RecordAsync(served, snapshot, options, hash, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }
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
        if (serviceKey is not null)
        {
            return null;
        }

        if (serviceType.IsInstanceOfType(this))
        {
            return this;
        }

        if (serviceType == typeof(ICostLedger))
        {
            return _ledger;
        }

        return null;
    }

    public void Dispose()
    {
        _mutex.Dispose();
        _replayClient.Dispose();
        _mockClient.Dispose();
    }

    private async Task<RouteDecision> DecideAsync(DateOnly today, CancellationToken cancellationToken)
    {
        if (_options.Mode == ModelRoute.MockReasoner)
        {
            return RouteDecision.Mock();
        }

        if (_options.Mode == ModelRoute.Replay)
        {
            return RouteDecision.Replay(liveRefused: false);
        }

        var spend = await _ledger.GetSpendAsync(today, cancellationToken).ConfigureAwait(false);
        if (spend >= _options.DailySpendCapUsd || _consecutiveFailures >= _options.CircuitBreakerFailures)
        {
            return RouteDecision.Replay(liveRefused: true);
        }

        if (_options.Mode == ModelRoute.AzureOpenAI)
        {
            if (_azureClient is null)
            {
                throw new ModelGatewayException("Azure OpenAI client is not configured.");
            }

            return RouteDecision.Live(_azureClient, ModelRoutes.AzureOpenAI);
        }

        if (_openAiClient is null)
        {
            throw new ModelGatewayException("OpenAI client is not configured.");
        }

        return RouteDecision.Live(_openAiClient, ModelRoutes.OpenAI);
    }

    private async Task<ServedCall> DispatchAsync(
        RouteDecision decision,
        IReadOnlyList<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        if (decision.LiveClient is not null)
        {
            var live = await TryLiveAsync(decision.LiveClient, messages, options, cancellationToken).ConfigureAwait(false);
            if (live is not null)
            {
                _consecutiveFailures = 0;
                return new ServedCall(live, decision.Route, LiveRefused: false, FellBack: false);
            }

            var fallback = await ServeReplayOrMockAsync(messages, options, preferReplay: true, cancellationToken).ConfigureAwait(false);
            return fallback with { LiveRefused = false, FellBack = true };
        }

        var served = await ServeReplayOrMockAsync(messages, options, decision.PreferReplay, cancellationToken).ConfigureAwait(false);
        return served with { LiveRefused = decision.LiveRefused };
    }

    private async Task<ChatResponse?> TryLiveAsync(
        IChatClient client,
        IReadOnlyList<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        var attempts = _options.LiveRetryCount + 1;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                return await client.GetResponseAsync(messages, Pin(options), cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException) when (!cancellationToken.IsCancellationRequested)
            {
                _consecutiveFailures++;
            }
            catch (TimeoutException) when (!cancellationToken.IsCancellationRequested)
            {
                _consecutiveFailures++;
            }
            catch (IOException) when (!cancellationToken.IsCancellationRequested)
            {
                _consecutiveFailures++;
            }
        }

        return null;
    }

    private async Task<ServedCall> ServeReplayOrMockAsync(
        IReadOnlyList<ChatMessage> messages,
        ChatOptions? options,
        bool preferReplay,
        CancellationToken cancellationToken)
    {
        if (preferReplay)
        {
            try
            {
                var replay = await _replayClient.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
                return new ServedCall(replay, ModelRoutes.Replay, LiveRefused: false, FellBack: false);
            }
            catch (ReplayMissException)
            {
                // The mock reasoner is the labeled fallback when no recording exists.
            }
        }

        var mock = await _mockClient.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        if (_options.RecordReplayMisses)
        {
            _catalog.Store(PromptHasher.Hash(messages, options), ReplayTurn.Capture(mock, messages));
        }

        return new ServedCall(mock, ModelRoutes.Mock, LiveRefused: false, FellBack: preferReplay);
    }

    private async Task<ChatResponse> RecordAsync(
        ServedCall served,
        IReadOnlyList<ChatMessage> messages,
        ChatOptions? options,
        string hash,
        CancellationToken cancellationToken)
    {
        var (input, output) = TokenCost.Count(served.Response, messages);
        var cost = TokenCost.Compute(input, output, _options.InputUsdPerMillionTokens, _options.OutputUsdPerMillionTokens);
        var entry = new CostEntry(
            Guid.NewGuid().ToString("D"),
            served.Route,
            served.Response.ModelId ?? served.Route,
            input,
            output,
            cost,
            _time.GetUtcNow(),
            served.LiveRefused,
            options?.ConversationId);
        await _ledger.RecordAsync(entry, cancellationToken).ConfigureAwait(false);

        served.Response.ModelId ??= served.Route;
        served.Response.CreatedAt ??= _time.GetUtcNow();
        served.Response.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        served.Response.AdditionalProperties[GatewayMetadata.Route] = served.Route;
        served.Response.AdditionalProperties[GatewayMetadata.LiveRefused] = served.LiveRefused;
        served.Response.AdditionalProperties[GatewayMetadata.FellBack] = served.FellBack;
        served.Response.AdditionalProperties[GatewayMetadata.PromptHash] = hash;
        served.Response.AdditionalProperties[GatewayMetadata.CostUsd] = cost;
        return served.Response;
    }

    private bool TryScripted(string? scenarioId, string hash, [NotNullWhen(true)] out ChatResponse? response)
    {
        response = null;
        if (!_options.ScriptedReplay || string.IsNullOrWhiteSpace(scenarioId))
        {
            return false;
        }

        if (!_scripted.TryGet(scenarioId, hash, out var turn))
        {
            return false;
        }

        response = turn.ToChatResponse();
        response.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        response.AdditionalProperties[GatewayMetadata.ScriptedReplay] = true;
        response.AdditionalProperties[GatewayMetadata.ScenarioId] = scenarioId.Trim();
        return true;
    }

    private ChatOptions Pin(ChatOptions? options)
    {
        var pinned = options?.Clone() ?? new ChatOptions();
        pinned.Temperature = _options.Temperature;
        pinned.Seed = _options.Seed;
        return pinned;
    }

    private static string? ReadScenarioId(ChatOptions? options)
    {
        if (options?.AdditionalProperties is null)
        {
            return null;
        }

        if (!options.AdditionalProperties.TryGetValue(GatewayMetadata.ScenarioId, out var value) || value is null)
        {
            return null;
        }

        var text = value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static List<ChatMessage> Copy(IEnumerable<ChatMessage> messages)
    {
        var copy = new List<ChatMessage>();
        foreach (var message in messages)
        {
            if (message is null)
            {
                throw new ArgumentException("Chat messages cannot contain null entries.", nameof(messages));
            }

            copy.Add(message);
        }

        return copy;
    }

    private sealed record RouteDecision(IChatClient? LiveClient, string Route, bool PreferReplay, bool LiveRefused)
    {
        public static RouteDecision Live(IChatClient client, string route)
        {
            return new RouteDecision(client, route, false, false);
        }

        public static RouteDecision Replay(bool liveRefused)
        {
            return new RouteDecision(null, ModelRoutes.Replay, true, liveRefused);
        }

        public static RouteDecision Mock()
        {
            return new RouteDecision(null, ModelRoutes.Mock, false, false);
        }
    }

    private sealed record ServedCall(ChatResponse Response, string Route, bool LiveRefused, bool FellBack);
}
