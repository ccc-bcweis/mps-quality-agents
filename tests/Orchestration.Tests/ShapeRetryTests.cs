// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json.Nodes;

using Microsoft.Extensions.AI;

using Mps.Domain.Evidence;
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
        resolution.Requests[0].Should().NotContain("Your previous answer");
        resolution.Requests[0].Should().Contain("shapeOptions");
        resolution.Requests[1].Should().Contain("Your previous answer set shape to");
        resolution.Requests[1].Should().Contain("measured shape");
        resolution.Requests[1].Should().Contain("not even to say the signal is not like that", "a contrast such as \"a step, not a drift\" still reads as the wrong shape");
        resolution.Requests[1].Should().Contain("gradual, drift");
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

    [Fact]
    public async Task ContrastingProseWithTheMeasuredShapeFieldRecommendsWithoutARetry()
    {
        var resolution = new ProseContrastChat(WorkflowRig.MockChat());
        var rig = new WorkflowRig(ScenarioKind.EngineeringChange, WorkflowRig.MockChat(), resolution);
        var incident = Oscillation("corr-shape-prose");
        rig.Incidents.Add(incident);

        var started = await rig.Workflow.StartAsync(incident.Id, CancellationToken.None);

        started.Stage.Should().Be(InvestigationStage.AwaitingApproval);
        started.Hypotheses[0].Cause.Should().Contain("rather than a gradual drift");
        rig.Resolution.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ARoundWithUserEvidenceSendsItToTheAiAndKeepsTheShapeRetry()
    {
        var resolution = new ShapeContradictingChat(WorkflowRig.MockChat(), contradictCalls: 1);
        var rig = new WorkflowRig(ScenarioKind.EngineeringChange, WorkflowRig.MockChat(), resolution);
        var incident = Oscillation("corr-user-evidence-round");
        rig.Incidents.Add(incident);
        var note = InvestigationRounds.UserEvidence("The chiller loop was serviced yesterday and the flow valve was left half open.", "Bryan Weis", WorkflowRig.At, "E-02", closedRound: 1);

        var started = await rig.Workflow.StartRoundAsync(incident.Id, [note], CancellationToken.None);

        started.Stage.Should().Be(InvestigationStage.AwaitingApproval);
        started.Evidence.Should().Contain(item => item.Source == EvidenceSource.UserProvided && item.SuppliedBy == "Bryan Weis");
        rig.Resolution.Calls.Should().Be(1 + MafInvestigationWorkflow.ShapeRetries, "the single shape retry still applies on a user-evidence round");
        resolution.Requests.Should().HaveCount(2);
        resolution.Requests.Should().OnlyContain(request => request.Contains("flow valve was left half open", StringComparison.Ordinal));
        resolution.Requests[0].Should().Contain("userEvidence");
    }

    [Fact]
    public async Task UserEvidenceDoesNotLetAPersistentShapeMismatchThrough()
    {
        var resolution = new ShapeContradictingChat(WorkflowRig.MockChat(), contradictCalls: 2);
        var rig = new WorkflowRig(ScenarioKind.EngineeringChange, WorkflowRig.MockChat(), resolution);
        var incident = Oscillation("corr-user-evidence-guard");
        rig.Incidents.Add(incident);
        var note = InvestigationRounds.UserEvidence("Operators saw the clamp chatter.", "Bryan Weis", WorkflowRig.At, "E-02", closedRound: 1);

        var started = await rig.Workflow.StartRoundAsync(incident.Id, [note], CancellationToken.None);

        started.Stage.Should().Be(InvestigationStage.Escalated);
        started.Hypotheses.Should().BeEmpty();
        started.Evidence.Should().Contain(item => item.Source == EvidenceSource.UserProvided);
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

    /// <summary>Keeps the measured shape in the shape field but adds a contrast sentence naming another shape.</summary>
    private sealed class ProseContrastChat : IChatClient
    {
        private readonly IChatClient _inner;

        public ProseContrastChat(IChatClient inner)
        {
            _inner = inner;
        }

        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var response = await _inner.GetResponseAsync(messages, options, cancellationToken);
            var claim = JsonNode.Parse(response.Text)!.AsObject();
            claim["cause"] = claim["cause"]!.GetValue<string>() + " The reading shows " + claim["shape"]!.GetValue<string>() + " behaviour rather than a gradual drift.";
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

    /// <summary>Returns the real mock answer, but the first calls report a shape the pack does not show in the shape field.</summary>
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
            var claim = JsonNode.Parse(response.Text)!.AsObject();
            claim["shape"] = shape == "ramp" ? "step" : "ramp";
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
