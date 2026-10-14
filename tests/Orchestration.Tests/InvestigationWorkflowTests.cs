// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Extensions.AI;

using Mps.Agents.Grounding;
using Mps.Agents.Reasoning;
using Mps.Domain.Approval;
using Mps.Domain.Capa;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;
using Mps.ModelGateway.Adapters;
using Mps.ModelGateway.Replay;
using Mps.Orchestration;

namespace Mps.Orchestration.Tests;

public sealed class InvestigationWorkflowTests
{
    [Fact]
    public async Task S1ReplayPausesForApprovalAndASecondRunDoesNotCallTheReasoner()
    {
        var catalog = new InMemoryReplayCatalog();
        using var recording = WorkflowRig.Gateway(catalog, new QualityMockReasoner(), recordMisses: true);
        var rig = new WorkflowRig(ScenarioKind.EngineeringChange, recording, recording);
        var firstIncident = EngineeringIncident(Severity.High, "corr-s1");
        rig.Incidents.Add(firstIncident);

        var started = await rig.Workflow.StartAsync(firstIncident.Id, CancellationToken.None);

        started.Stage.Should().Be(InvestigationStage.AwaitingApproval);
        started.Hypotheses.Should().ContainSingle();
        started.Hypotheses[0].Category.Should().Be(RootCauseCategory.EngineeringChange);
        started.Hypotheses[0].Cause.Should().Contain("ECO-4471");
        started.Hypotheses[0].CounterEvidence.Should().Contain(item => item.Contains("WO-19", StringComparison.Ordinal));
        started.Evidence.Should().NotBeEmpty();
        rig.Resolution.Calls.Should().Be(1);

        var reader = rig.ContinueWith(recording, recording);
        var visible = await reader.Workflow.GetStateAsync(started.Id, CancellationToken.None);
        visible.Stage.Should().Be(InvestigationStage.AwaitingApproval);
        visible.Hypotheses[0].Cause.Should().Contain("ECO-4471");

        var reasoner = new ThrowingReasoner();
        using var replay = WorkflowRig.Gateway(catalog, reasoner, recordMisses: false);
        var secondIncident = EngineeringIncident(Severity.High, "corr-s1-replay");
        rig.Incidents.Add(secondIncident);
        var replayedRig = rig.ContinueWith(replay, replay);
        var replayed = await replayedRig.Workflow.StartAsync(secondIncident.Id, CancellationToken.None);

        reasoner.Calls.Should().Be(0);
        replayed.Stage.Should().Be(InvestigationStage.AwaitingApproval);
        replayed.Hypotheses[0].Category.Should().Be(RootCauseCategory.EngineeringChange);
        replayed.Hypotheses[0].Cause.Should().Contain("ECO-4471");
    }

    [Fact]
    public async Task S2ReplayEscalatesOnTheRevisionConflictAndDoesNotRankACause()
    {
        var catalog = new InMemoryReplayCatalog();
        using var recording = WorkflowRig.Gateway(catalog, new QualityMockReasoner(), recordMisses: true);
        var rig = new WorkflowRig(ScenarioKind.ConfigConflict, recording, recording);
        var incident = Incident.Open(
            IncidentId.New(),
            new ToolId("MPS-D7"),
            "D-01",
            "pressure",
            IncidentChannel.IotSpcAlert,
            Severity.High,
            "Pressure step",
            "corr-s2",
            WorkflowRig.At);
        rig.Incidents.Add(incident);

        var started = await rig.Workflow.StartAsync(incident.Id, CancellationToken.None);

        started.Stage.Should().Be(InvestigationStage.Escalated);
        started.EscalationReason.Should().Contain("rev C");
        started.EscalationReason.Should().Contain("rev B");
        started.EscalationReason.Should().Contain("calibration records missing");
        started.Hypotheses.Should().BeEmpty();
        rig.Resolution.Calls.Should().Be(0);

        var reasoner = new ThrowingReasoner();
        using var replay = WorkflowRig.Gateway(catalog, reasoner, recordMisses: false);
        var again = Incident.Open(
            IncidentId.New(),
            new ToolId("MPS-D7"),
            "D-01",
            "pressure",
            IncidentChannel.IotSpcAlert,
            Severity.High,
            "Pressure step",
            "corr-s2-replay",
            WorkflowRig.At);
        rig.Incidents.Add(again);
        var replayedRig = rig.ContinueWith(replay, replay);
        var replayed = await replayedRig.Workflow.StartAsync(again.Id, CancellationToken.None);

        reasoner.Calls.Should().Be(0);
        replayed.Stage.Should().Be(InvestigationStage.Escalated);
        replayed.Hypotheses.Should().BeEmpty();
        replayedRig.Resolution.Calls.Should().Be(0);
    }

    [Fact]
    public async Task CriticalSeverityStillGathersEvidenceBeforeApproval()
    {
        var rig = MockRig(ScenarioKind.EngineeringChange);
        var incident = EngineeringIncident(Severity.Critical, "corr-critical");
        rig.Incidents.Add(incident);

        var started = await rig.Workflow.StartAsync(incident.Id, CancellationToken.None);

        started.Stage.Should().Be(InvestigationStage.AwaitingApproval);
        started.Evidence.Should().NotBeEmpty();
        started.Hypotheses.Should().ContainSingle();
        started.Hypotheses[0].Category.Should().Be(RootCauseCategory.EngineeringChange);
        rig.Resolution.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ApproveConsumesTheTokenOnANewWorkflowInstance()
    {
        var rig = MockRig(ScenarioKind.EngineeringChange);
        var incident = EngineeringIncident(Severity.High, "corr-approve");
        rig.Incidents.Add(incident);
        var started = await rig.Workflow.StartAsync(incident.Id, CancellationToken.None);
        var summary = rig.Workflow.ProposedActionSummary(started.Id);
        summary.Should().Be(string.Join("; ", started.Hypotheses[0].Actions));
        var token = ApprovalToken.Issue(
            ApprovalTokenId.New(),
            "qa-lead",
            CapaAction.ComputeActionHash(started.IncidentId, summary),
            WorkflowRig.At,
            TimeSpan.FromHours(1),
            WorkflowRig.SigningKey);
        var resumedBy = rig.ContinueWith(rig.InvestigationChat, rig.ResolutionChat);

        var approved = await resumedBy.Workflow.ResumeWithHumanInputAsync(
            started.Id,
            new HumanDecision(HumanDecisionKind.Approve, "Ship the recommendation.", token),
            CancellationToken.None);

        approved.Stage.Should().Be(InvestigationStage.Approved);
        token.IsConsumed.Should().BeTrue();
        approved.Hypotheses.Should().ContainSingle();
        approved.Hypotheses[0].Cause.Should().Contain("ECO-4471");
    }

    [Fact]
    public async Task RejectKeepsTheHypothesis()
    {
        var rig = MockRig(ScenarioKind.EngineeringChange);
        var incident = EngineeringIncident(Severity.High, "corr-reject");
        rig.Incidents.Add(incident);
        var started = await rig.Workflow.StartAsync(incident.Id, CancellationToken.None);
        var resumedBy = rig.ContinueWith(rig.InvestigationChat, rig.ResolutionChat);

        var rejected = await resumedBy.Workflow.ResumeWithHumanInputAsync(
            started.Id,
            new HumanDecision(HumanDecisionKind.Reject, "Not this week.", null),
            CancellationToken.None);

        rejected.Stage.Should().Be(InvestigationStage.Rejected);
        rejected.Hypotheses.Should().ContainSingle();
    }

    [Fact]
    public async Task RequestInfoClearsTheHypothesisAndNamesTheComment()
    {
        var rig = MockRig(ScenarioKind.EngineeringChange);
        var incident = EngineeringIncident(Severity.High, "corr-info");
        rig.Incidents.Add(incident);
        var started = await rig.Workflow.StartAsync(incident.Id, CancellationToken.None);
        var resumedBy = rig.ContinueWith(rig.InvestigationChat, rig.ResolutionChat);

        var escalated = await resumedBy.Workflow.ResumeWithHumanInputAsync(
            started.Id,
            new HumanDecision(HumanDecisionKind.RequestInfo, "Need the edge-ring traveler.", null),
            CancellationToken.None);

        escalated.Stage.Should().Be(InvestigationStage.Escalated);
        escalated.EscalationReason.Should().Be("request_info: Need the edge-ring traveler.");
        escalated.Hypotheses.Should().BeEmpty();
    }

    [Fact]
    public async Task ABlankRequestInfoStillEscalates()
    {
        var rig = MockRig(ScenarioKind.EngineeringChange);
        var incident = EngineeringIncident(Severity.High, "corr-info-blank");
        rig.Incidents.Add(incident);
        var started = await rig.Workflow.StartAsync(incident.Id, CancellationToken.None);

        var escalated = await rig.Workflow.ResumeWithHumanInputAsync(
            started.Id,
            new HumanDecision(HumanDecisionKind.RequestInfo, "  ", null),
            CancellationToken.None);

        escalated.Stage.Should().Be(InvestigationStage.Escalated);
        escalated.EscalationReason.Should().Be("request_info: reviewer requested more information");
        escalated.Hypotheses.Should().BeEmpty();
    }

    [Fact]
    public async Task ResumeBeforeApprovalThrows()
    {
        var rig = MockRig(ScenarioKind.ConfigConflict);
        var incident = Incident.Open(
            IncidentId.New(),
            new ToolId("MPS-D7"),
            "D-01",
            "pressure",
            IncidentChannel.IotSpcAlert,
            Severity.High,
            "Pressure step",
            "corr-illegal",
            WorkflowRig.At);
        rig.Incidents.Add(incident);
        var started = await rig.Workflow.StartAsync(incident.Id, CancellationToken.None);
        started.Stage.Should().Be(InvestigationStage.Escalated);

        var act = async () => await rig.Workflow.ResumeWithHumanInputAsync(
            started.Id,
            new HumanDecision(HumanDecisionKind.Reject, "too soon", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AMissingIncidentFailsWithoutAHypothesis()
    {
        var rig = MockRig(ScenarioKind.EngineeringChange);

        var started = await rig.Workflow.StartAsync(IncidentId.New(), CancellationToken.None);

        started.Stage.Should().Be(InvestigationStage.Failed);
        started.EscalationReason.Should().Be("Incident was not found.");
        started.CorrelationId.Should().Be("missing");
        started.Hypotheses.Should().BeEmpty();
        rig.Resolution.Calls.Should().Be(0);
    }

    [Fact]
    public async Task AToolFailureFailsWithoutCallingResolution()
    {
        var rig = MockRig(ScenarioKind.EngineeringChange);
        rig.Tools.ThrowOnSpc = true;
        var incident = EngineeringIncident(Severity.High, "corr-tool");
        rig.Incidents.Add(incident);

        var started = await rig.Workflow.StartAsync(incident.Id, CancellationToken.None);

        started.Stage.Should().Be(InvestigationStage.Failed);
        started.EscalationReason.Should().Contain("Spc.GetViolations");
        started.Hypotheses.Should().BeEmpty();
        rig.Resolution.Calls.Should().Be(0);
    }

    [Fact]
    public async Task AnUngroundedClaimEscalatesWithoutAHypothesis()
    {
        var chat = WorkflowRig.MockChat();
        var json = """{"category":"engineeringChange","cause":"guess","confidence":0.4,"evidenceIds":["00000000-0000-0000-0000-000000000099"],"counterEvidence":["none"],"actions":["inspect"]}""";
        var resolutionChat = new ScriptedChatClient(new ChatResponse(new ChatMessage(ChatRole.Assistant, json)));
        var rig = new WorkflowRig(ScenarioKind.EngineeringChange, chat, resolutionChat);
        var incident = EngineeringIncident(Severity.High, "corr-ground");
        rig.Incidents.Add(incident);

        var started = await rig.Workflow.StartAsync(incident.Id, CancellationToken.None);

        started.Stage.Should().Be(InvestigationStage.Escalated);
        started.EscalationReason.Should().Contain("grounding:");
        started.EscalationReason.Should().Contain("unknown-evidence-id:");
        started.Hypotheses.Should().BeEmpty();
    }

    [Fact]
    public void ProposedSummaryJoinsActions()
    {
        var claim = new ResolutionClaim(
            RootCauseCategory.EngineeringChange,
            "ECO ECO-4471 changed the affected scope shortly before onset.",
            0.86d,
            [new EvidenceId(Guid.Parse("00000000-0000-0000-0000-000000000001"))],
            ["WO-19 PM: same week"],
            ["Review ECO-4471 before any corrective action."],
            []);

        MafInvestigationWorkflow.ProposedSummary(claim).Should().Be("Review ECO-4471 before any corrective action.");
    }

    [Fact]
    public async Task TheLangGraphAdapterStaysAStub()
    {
        var adapter = new LangGraphWorkflowAdapter();

        var act = async () => await adapter.StartAsync(IncidentId.New(), CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<NotSupportedException>();
        thrown.Which.Message.Should().Contain("docs/langgraph-compat.md");
    }

    [Fact]
    public async Task GetStateThrowsWhenTheInvestigationIsUnknown()
    {
        var rig = MockRig(ScenarioKind.EngineeringChange);

        var act = async () => await rig.Workflow.GetStateAsync(InvestigationId.New(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static WorkflowRig MockRig(ScenarioKind kind)
    {
        var chat = WorkflowRig.MockChat();
        return new WorkflowRig(kind, chat, chat);
    }

    private static Incident EngineeringIncident(Severity severity, string correlationId)
    {
        return Incident.Open(
            IncidentId.New(),
            new ToolId("MPS-E7"),
            "E-02",
            "esc_temp",
            IncidentChannel.IotSpcAlert,
            severity,
            "ESC temperature oscillation",
            correlationId,
            WorkflowRig.At);
    }
}
