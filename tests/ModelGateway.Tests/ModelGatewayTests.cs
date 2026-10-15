// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Extensions.AI;

using Mps.ModelGateway.Adapters;
using Mps.ModelGateway.Cost;
using Mps.ModelGateway.Replay;
using Mps.ModelGateway.Routing;

namespace Mps.ModelGateway.Tests;

public sealed class ModelGatewayTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReplayReturnsTheSameTextForTheSamePrompt()
    {
        var messages = User("classify the etch excursion");
        var catalog = CatalogWith(messages, "recorded-once");
        var gateway = Gateway(ModelRoute.Replay, catalog, reasoner: ThrowingReasoner());

        var first = await gateway.GetResponseAsync(messages);
        var second = await gateway.GetResponseAsync(messages);

        second.Text.Should().Be(first.Text);
        first.Text.Should().Be("recorded-once");
        first.AdditionalProperties![GatewayMetadata.Route].Should().Be(ModelRoutes.Replay);
        first.AdditionalProperties[GatewayMetadata.PromptHash].Should().Be(second.AdditionalProperties![GatewayMetadata.PromptHash]);
    }

    [Fact]
    public async Task MockReasonerIsDeterministicAndChangesWhenThePromptChanges()
    {
        var gateway = Gateway(
            ModelRoute.MockReasoner,
            new InMemoryReplayCatalog(),
            reasoner: new DelegateMockReasoner((messages, _) =>
                TextResponse("mock:" + PromptHasher.Hash(messages, null))));

        var first = await gateway.GetResponseAsync(User("same"));
        var second = await gateway.GetResponseAsync(User("same"));
        var other = await gateway.GetResponseAsync(User("different"));

        second.Text.Should().Be(first.Text);
        other.Text.Should().NotBe(first.Text);
        first.AdditionalProperties![GatewayMetadata.Route].Should().Be(ModelRoutes.Mock);
    }

    [Fact]
    public async Task EachCallRecordsItsCost()
    {
        var messages = User("cost me");
        var catalog = new InMemoryReplayCatalog();
        catalog.Store(
            PromptHasher.Hash(messages, null),
            new ReplayTurn("replay-model", "priced", [], inputTokens: 2_000, outputTokens: 1_000));
        var ledger = new InMemoryCostLedger();
        var gateway = Gateway(ModelRoute.Replay, catalog, ledger, ThrowingReasoner(), inputPrice: 0.15m, outputPrice: 0.60m);

        var response = await gateway.GetResponseAsync(messages, new ChatOptions { ConversationId = "inv-1" });

        var entries = await ledger.GetEntriesAsync(DateOnly.FromDateTime(Now.UtcDateTime), CancellationToken.None);
        var entry = entries.Should().ContainSingle().Subject;
        entry.Route.Should().Be(ModelRoutes.Replay);
        entry.Model.Should().Be("replay-model");
        entry.InputTokens.Should().Be(2_000);
        entry.OutputTokens.Should().Be(1_000);
        entry.CostUsd.Should().Be(0.0009m);
        entry.ConversationId.Should().Be("inv-1");
        entry.LiveCallRefused.Should().BeFalse();
        response.AdditionalProperties![GatewayMetadata.CostUsd].Should().Be(0.0009m);
    }

    [Fact]
    public async Task SpendAtTheCapRefusesTheLiveClientAndReplays()
    {
        var messages = User("over the cap");
        var live = new ScriptedChatClient("live-answer", inputTokens: 1_000_000, outputTokens: 0);
        var catalog = CatalogWith(messages, "replayed");
        var ledger = new InMemoryCostLedger();
        var gateway = Gateway(
            ModelRoute.AzureOpenAI,
            catalog,
            ledger,
            ThrowingReasoner(),
            cap: 1.00m,
            inputPrice: 1.00m,
            outputPrice: 0m,
            azure: live);

        var first = await gateway.GetResponseAsync(messages);
        var second = await gateway.GetResponseAsync(messages);

        first.Text.Should().Be("live-answer");
        first.AdditionalProperties![GatewayMetadata.LiveRefused].Should().Be(false);
        second.Text.Should().Be("replayed");
        second.AdditionalProperties![GatewayMetadata.Route].Should().Be(ModelRoutes.Replay);
        second.AdditionalProperties[GatewayMetadata.LiveRefused].Should().Be(true);
        live.Calls.Should().Be(1);
        var entries = await ledger.GetEntriesAsync(DateOnly.FromDateTime(Now.UtcDateTime), CancellationToken.None);
        entries.Should().HaveCount(2);
        entries[0].CostUsd.Should().Be(1.00m);
        entries[0].Route.Should().Be(ModelRoutes.AzureOpenAI);
        entries[1].Route.Should().Be(ModelRoutes.Replay);
        entries[1].LiveCallRefused.Should().BeTrue();
    }

    [Fact]
    public async Task SpendFromYesterdayDoesNotTripTodaysCap()
    {
        var ledger = new InMemoryCostLedger();
        await ledger.RecordAsync(
            new CostEntry("yesterday", ModelRoutes.AzureOpenAI, "fake", 1, 1, 50m, Now.AddDays(-1), false, null),
            CancellationToken.None);
        var live = new ScriptedChatClient("still-live", 10, 10);
        var gateway = Gateway(
            ModelRoute.OpenAI,
            new InMemoryReplayCatalog(),
            ledger,
            ThrowingReasoner(),
            cap: 1.00m,
            openAi: live);

        var response = await gateway.GetResponseAsync(User("today"));

        response.Text.Should().Be("still-live");
        response.AdditionalProperties![GatewayMetadata.Route].Should().Be(ModelRoutes.OpenAI);
        live.Calls.Should().Be(1);
    }

    [Fact]
    public async Task TransientLiveFailureFallsBackAndTheCircuitStopsCallingLive()
    {
        var live = new ScriptedChatClient("unused", 10, 10) { Failure = new HttpRequestException("offline") };
        var gateway = Gateway(
            ModelRoute.AzureOpenAI,
            new InMemoryReplayCatalog(),
            new InMemoryCostLedger(),
            new DelegateMockReasoner((_, _) => TextResponse("mock-fallback")),
            azure: live,
            retries: 0,
            breaker: 1);

        var first = await gateway.GetResponseAsync(User("fail"));
        var second = await gateway.GetResponseAsync(User("fail"));

        first.Text.Should().Be("mock-fallback");
        first.AdditionalProperties![GatewayMetadata.FellBack].Should().Be(true);
        second.Text.Should().Be("mock-fallback");
        second.AdditionalProperties![GatewayMetadata.LiveRefused].Should().Be(true);
        live.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ReplayMissCanBeRecordedAndThenReplayedWithoutTheReasoner()
    {
        var catalog = new InMemoryReplayCatalog();
        var calls = 0;
        var reasoner = new DelegateMockReasoner((messages, _) =>
        {
            calls++;
            return TextResponse("reasoned:" + messages[0].Text);
        });
        var options = new ModelGatewayOptions
        {
            Mode = ModelRoute.Replay,
            RecordReplayMisses = true,
            InputUsdPerMillionTokens = 0m,
            OutputUsdPerMillionTokens = 0m,
        };
        var gateway = new Routing.ModelGateway(options, new InMemoryCostLedger(), catalog, reasoner, Clock());

        var first = await gateway.GetResponseAsync(User("etch"));
        var second = await gateway.GetResponseAsync(User("etch"));

        first.Text.Should().Be("reasoned:etch");
        first.AdditionalProperties![GatewayMetadata.Route].Should().Be(ModelRoutes.Mock);
        second.Text.Should().Be("reasoned:etch");
        second.AdditionalProperties![GatewayMetadata.Route].Should().Be(ModelRoutes.Replay);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task StreamingRecordsASingleCostEntry()
    {
        var messages = User("stream");
        var catalog = CatalogWith(messages, "streamed");
        var ledger = new InMemoryCostLedger();
        var gateway = Gateway(ModelRoute.Replay, catalog, ledger, ThrowingReasoner());

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in gateway.GetStreamingResponseAsync(messages))
        {
            updates.Add(update);
        }

        updates.Should().NotBeEmpty();
        string.Concat(updates.Select(update => update.Text)).Should().Be("streamed");
        var entries = await ledger.GetEntriesAsync(DateOnly.FromDateTime(Now.UtcDateTime), CancellationToken.None);
        entries.Should().ContainSingle();
    }

    [Fact]
    public async Task LiveCallsArePinnedToTemperatureZeroAndTheConfiguredSeed()
    {
        var live = new ScriptedChatClient("pinned", 4, 4);
        var gateway = Gateway(ModelRoute.OpenAI, new InMemoryReplayCatalog(), reasoner: ThrowingReasoner(), openAi: live, seed: 4242);

        var response = await gateway.GetResponseAsync(User("pin me"), new ChatOptions { Temperature = 0.9f, Seed = 99 });

        response.Text.Should().Be("pinned");
        live.LastOptions!.Temperature.Should().Be(0f);
        live.LastOptions.Seed.Should().Be(4242);
    }

    [Fact]
    public async Task ScriptedReplayUsesTheScenarioAndPromptHashAndFallsBackToLive()
    {
        var messages = User("rank the etch excursion");
        var hash = PromptHasher.Hash(messages, null);
        var scripted = new InMemoryScriptedReplayCatalog();
        scripted.Store("SC-1", hash, new ReplayTurn("recorded", "same words", [], 4, 4));
        var live = new ScriptedChatClient("live wording", 4, 4);
        var hit = Gateway(
            ModelRoute.OpenAI,
            new InMemoryReplayCatalog(),
            reasoner: ThrowingReasoner(),
            openAi: live,
            scriptedReplay: true,
            scripted: scripted);
        var miss = Gateway(
            ModelRoute.OpenAI,
            new InMemoryReplayCatalog(),
            reasoner: ThrowingReasoner(),
            openAi: live,
            scriptedReplay: true,
            scripted: scripted);

        var recorded = await hit.GetResponseAsync(messages, Scenario("SC-1"));
        var liveCall = await miss.GetResponseAsync(messages, Scenario("SC-3"));
        var ignored = Gateway(
            ModelRoute.OpenAI,
            new InMemoryReplayCatalog(),
            reasoner: ThrowingReasoner(),
            openAi: live,
            scriptedReplay: false,
            scripted: scripted);
        var uncached = await ignored.GetResponseAsync(messages, Scenario("SC-1"));

        recorded.Text.Should().Be("same words");
        recorded.AdditionalProperties![GatewayMetadata.ScriptedReplay].Should().Be(true);
        recorded.AdditionalProperties[GatewayMetadata.Route].Should().Be(ModelRoutes.Replay);
        liveCall.Text.Should().Be("live wording");
        liveCall.AdditionalProperties!.ContainsKey(GatewayMetadata.ScriptedReplay).Should().BeFalse();
        uncached.Text.Should().Be("live wording");
        live.Calls.Should().Be(2);
        live.LastOptions!.Temperature.Should().Be(0f);
        live.LastOptions.Seed.Should().Be(4242);
    }

    [Fact]
    public async Task AzureModeWithoutAClientThrowsBeforeAnyCall()
    {
        var gateway = Gateway(ModelRoute.AzureOpenAI, new InMemoryReplayCatalog(), reasoner: ThrowingReasoner());

        var act = async () => await gateway.GetResponseAsync(User("live"));

        await act.Should().ThrowAsync<ModelGatewayException>().WithMessage("*Azure OpenAI*");
    }

    private static Routing.ModelGateway Gateway(
        ModelRoute mode,
        IReplayCatalog catalog,
        ICostLedger? ledger = null,
        IMockReasoner? reasoner = null,
        decimal cap = 2m,
        decimal inputPrice = 0.15m,
        decimal outputPrice = 0.60m,
        IChatClient? azure = null,
        IChatClient? openAi = null,
        int retries = 2,
        int breaker = 5,
        bool scriptedReplay = false,
        long seed = 4242,
        IScriptedReplayCatalog? scripted = null)
    {
        var options = new ModelGatewayOptions
        {
            Mode = mode,
            DailySpendCapUsd = cap,
            InputUsdPerMillionTokens = inputPrice,
            OutputUsdPerMillionTokens = outputPrice,
            LiveRetryCount = retries,
            CircuitBreakerFailures = breaker,
            ScriptedReplay = scriptedReplay,
            Temperature = 0f,
            Seed = seed,
        };
        return new Routing.ModelGateway(
            options,
            ledger ?? new InMemoryCostLedger(),
            catalog,
            reasoner ?? ThrowingReasoner(),
            Clock(),
            azure,
            openAi,
            scripted);
    }

    private static ChatOptions Scenario(string scenarioId)
    {
        return new ChatOptions
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                [GatewayMetadata.ScenarioId] = scenarioId,
            },
        };
    }

    private static InMemoryReplayCatalog CatalogWith(IReadOnlyList<ChatMessage> messages, string text)
    {
        var catalog = new InMemoryReplayCatalog();
        catalog.Store(PromptHasher.Hash(messages, null), new ReplayTurn("replay-model", text, [], 4, 4));
        return catalog;
    }

    private static List<ChatMessage> User(string text)
    {
        return [new ChatMessage(ChatRole.User, text)];
    }

    private static ChatResponse TextResponse(string text)
    {
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
        {
            ModelId = "mock-reasoner",
            Usage = new UsageDetails { InputTokenCount = 4, OutputTokenCount = 4, TotalTokenCount = 8 },
        };
    }

    private static DelegateMockReasoner ThrowingReasoner()
    {
        return new DelegateMockReasoner((_, _) => throw new InvalidOperationException("The mock reasoner should not run."));
    }

    private static FixedTimeProvider Clock()
    {
        return new FixedTimeProvider(Now);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }
    }

    private sealed class ScriptedChatClient : IChatClient
    {
        public ScriptedChatClient(string text, int inputTokens, int outputTokens)
        {
            Text = text;
            InputTokens = inputTokens;
            OutputTokens = outputTokens;
        }

        public string Text { get; }

        public int InputTokens { get; }

        public int OutputTokens { get; }

        public int Calls { get; private set; }

        public ChatOptions? LastOptions { get; private set; }

        public Exception? Failure { get; set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastOptions = options;
            if (Failure is not null)
            {
                throw Failure;
            }

            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, Text))
            {
                ModelId = "fake-live",
                Usage = new UsageDetails
                {
                    InputTokenCount = InputTokens,
                    OutputTokenCount = OutputTokens,
                    TotalTokenCount = InputTokens + OutputTokens,
                },
            };
            return Task.FromResult(response);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return null;
        }

        public void Dispose()
        {
        }
    }
}
