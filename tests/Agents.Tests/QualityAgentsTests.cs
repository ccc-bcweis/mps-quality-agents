// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using Mps.Agents.Hosting;

namespace Mps.Agents.Tests;

/// <summary>The agents run as Agent Framework ChatClientAgents and do not auto-invoke tools.</summary>
public sealed class QualityAgentsTests
{
    [Fact]
    public async Task ATurnGoesThroughAChatClientAgentAndLeavesFunctionCallsToTheCaller()
    {
        var chat = new CallReturningClient();
        var agent = QualityAgents.Create(chat, QualityAgents.InvestigationName, "test");

        agent.Should().BeOfType<ChatClientAgent>();
        agent.Name.Should().Be(QualityAgents.InvestigationName);
        var (messages, _) = await QualityAgents.TurnAsync(agent, [new ChatMessage(ChatRole.User, "hi")], new ChatOptions(), CancellationToken.None);

        chat.Calls.Should().Be(1);
        messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().Should().ContainSingle();
    }

    private sealed class CallReturningClient : IChatClient
    {
        public int Calls { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("c1", "get_runs")])));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
