// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json.Nodes;

using Microsoft.Extensions.AI;

using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;

namespace Mps.Orchestration.Tests;

public sealed class ShapeRetryTests
{
    [Fact]
    public async Task AShapeMismatchIsRetriedOnceWithTheMeasuredShapeAndThenRecommends()
    {
        var resolution = new ShapeContradictingChat(WorkflowRig.MockChat(), contradictCalls: 1);
        var rig = new WorkflowRig(ScenarioKind.EngineeringChange, WorkflowRig.MockChat(), resolution);
        var incident = Oscillation("corr-shape-retry");
        rig.Incidents.Add(incident);

        var started = await rig.Workflow.StartAsync(incident.Id, CancellationToken.None);

        started.Stage.Should().Be(InvestigationStage.AwaitingApproval);
        started.Hypotheses.Should().ContainSingle();
        rig.Resolution.Calls.Should().Be(2);
        resolution.Requests.Should().HaveCount(2);
        resolution.Requests[0].Should().NotContain("Your previous cause");
        resolution.Requests[1].Should().Contain("Your previous cause described the signal as");
        resolution.Requests[1].Should().Contain("measured shape");
    }

    [Fact]
    public async Task AShapeMismatchThatPersistsAfterTheRetryEscalates()
    {
        var resolution = new ShapeContradictingChat(WorkflowRig.MockChat(), contradictCalls: 2);
        var rig = new WorkflowRig(ScenarioKind.EngineeringChange, WorkflowRig.MockChat(), resolution);
        var incident = Oscillation("corr-shape-retry-fails");
        rig.Incidents.Add(incident);

        var started = await rig.Workflow.StartAsync(incident.Id, CancellationToken.None);

        started.Stage.Should().Be(InvestigationStage.Escalated);
        started.EscalationReason.Should().Contain("The AI described the signal as");
        started.Hypotheses.Should().BeEmpty();
        rig.Resolution.Calls.Should().Be(1 + MafInvestigationWorkflow.ShapeRetries);
    }

    [Fact]
    public async Task AMatchingShapeNeedsNoRetry()
    {
        var resolution = new ShapeContradictingChat(WorkflowRig.MockChat(), contradictCalls: 0);
        var rig = new WorkflowRig(ScenarioKind.EngineeringChange, WorkflowRig.MockChat(), resolution);
        var incident = Oscillation("corr-shape-no-retry");
        rig.Incidents.Add(incident);

        var started = await rig.Workflow.StartAsync(incident.Id, CancellationToken.None);

        started.Stage.Should().Be(InvestigationStage.AwaitingApproval);
        rig.Resolution.Calls.Should().Be(1);
    }

    private static Incident Oscillation(string correlationId)
    {
        return Incident.Open(
            IncidentId.New(),
            new ToolId("MPS-E7"),
            "E-02",
            "esc_temp",
            IncidentChannel.IotSpcAlert,
            Severity.High,
            "ESC temperature oscillation",
            correlationId,
            WorkflowRig.At);
    }

    /// <summary>Returns the real mock answer, but the first calls describe the signal with a shape the pack does not show.</summary>
    private sealed class ShapeContradictingChat : IChatClient
    {
        private readonly IChatClient _inner;
        private readonly int _contradictCalls;

        public ShapeContradictingChat(IChatClient inner, int contradictCalls)
        {
            _inner = inner;
            _contradictCalls = contradictCalls;
        }

        public List<string> Requests { get; } = [];

        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var list = messages.ToList();
            var request = list.Where(message => message.Role == ChatRole.User).Select(message => message.Text).First();
            Requests.Add(request);
            var response = await _inner.GetResponseAsync(list, options, cancellationToken);
            if (Requests.Count > _contradictCalls)
            {
                return response;
            }

            var shape = JsonNode.Parse(request)?["signalShape"]?.GetValue<string>() ?? "unknown";
            var wrong = shape == "ramp" ? "a sudden step" : "a gradual drift";
            var claim = JsonNode.Parse(response.Text)!.AsObject();
            claim["cause"] = claim["cause"]!.GetValue<string>() + " The signal shows " + wrong + ".";
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, claim.ToJsonString()));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return serviceType.IsInstanceOfType(this) ? this : null;
        }

        public void Dispose()
        {
            _inner.Dispose();
        }
    }
}
