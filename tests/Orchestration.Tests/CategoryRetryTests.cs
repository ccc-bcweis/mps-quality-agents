// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json.Nodes;

using Microsoft.Extensions.AI;

using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;

namespace Mps.Orchestration.Tests;

public sealed class CategoryRetryTests
{
    [Fact]
    public async Task ACategoryTheCitedWorkOrderContradictsIsRetriedOnceThenRecommends()
    {
        var resolution = new MiscategorizingChat(WorkflowRig.MockChat(), wrongCalls: 1);
        var rig = Rig(resolution);
        var incident = Open("corr-category-retry");
        rig.Incidents.Add(incident);

        var started = await rig.Workflow.StartAsync(incident.Id, CancellationToken.None);

        started.Stage.Should().Be(InvestigationStage.AwaitingApproval);
        rig.Resolution.Calls.Should().Be(2);
        resolution.Requests[0].Should().Contain("categoryRules").And.Contain("past its service interval");
        resolution.Requests[1].Should().Contain("Your previous answer set category to").And.Contain("WO-PMP-E02-BRG").And.Contain("equipmentCalibrationOrAlignment");
    }

    [Fact]
    public async Task ACategoryContradictionThatPersistsEscalates()
    {
        var resolution = new MiscategorizingChat(WorkflowRig.MockChat(), wrongCalls: 2);
        var rig = Rig(resolution);
        var incident = Open("corr-category-persist");
        rig.Incidents.Add(incident);

        var started = await rig.Workflow.StartAsync(incident.Id, CancellationToken.None);

        started.Stage.Should().Be(InvestigationStage.Escalated);
        started.EscalationReason.Should().Contain("processDrift").And.Contain("WO-PMP-E02-BRG").And.Contain("equipmentCalibrationOrAlignment");
        rig.Resolution.Calls.Should().Be(1 + MafInvestigationWorkflow.ShapeRetries);
    }

    private static WorkflowRig Rig(IChatClient resolution) =>
        new(ScenarioKind.EngineeringChange, WorkflowRig.MockChat(), resolution, tools: new ScenarioTools(ScenarioKind.EngineeringChange) { BearingWorkOrder = true });

    private static Incident Open(string correlationId) =>
        Incident.Open(IncidentId.New(), new ToolId("MPS-E7"), "E-02", "esc_temp", IncidentChannel.IotSpcAlert, Severity.High, "ESC temperature oscillation", correlationId, WorkflowRig.At);

    /// <summary>The first calls cite the bearing work order but file the cause as process drift, like the live SC-7 miss.</summary>
    private sealed class MiscategorizingChat : IChatClient
    {
        private readonly IChatClient _inner;
        private readonly int _wrongCalls;

        public MiscategorizingChat(IChatClient inner, int wrongCalls)
        {
            _inner = inner;
            _wrongCalls = wrongCalls;
        }

        public List<string> Requests { get; } = [];

        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var list = messages.ToList();
            var request = list.Where(message => message.Role == ChatRole.User).Select(message => message.Text).First();
            Requests.Add(request);
            var response = await _inner.GetResponseAsync(list, options, cancellationToken);
            if (Requests.Count > _wrongCalls)
            {
                return response;
            }

            var bearing = JsonNode.Parse(request)!["notes"]!.AsArray()
                .First(note => note!["externalRef"]?.GetValue<string>() == "WO-PMP-E02-BRG")!["id"]!.GetValue<string>();
            var claim = JsonNode.Parse(response.Text)!.AsObject();
            claim["category"] = "processDrift";
            claim["evidenceIds"]!.AsArray().Add(bearing);
            claim["counterEvidence"] = new JsonArray();
            claim["evidenceMatrix"] = new JsonArray();
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, claim.ToJsonString()));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            if (serviceKey is null && serviceType.IsInstanceOfType(this))
            {
                return this;
            }

            return _inner.GetService(serviceType, serviceKey);
        }

        public void Dispose()
        {
            _inner.Dispose();
        }
    }
}
