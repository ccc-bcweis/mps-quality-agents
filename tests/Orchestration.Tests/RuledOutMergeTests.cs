// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Evidence;
using Mps.Agents.Grounding;
using Mps.Agents.Impact;
using Mps.Agents.Ranking;
using Mps.Agents.Resolution;
using Mps.Application.Evidence;
using Mps.Domain;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;
using Mps.Orchestration;

namespace Mps.Orchestration.Tests;

public sealed class RuledOutMergeTests
{
    [Fact]
    public void RecordEntriesKeepTheirClassAndAgentEntriesTakeTheCandidateClass()
    {
        var recordId = EvidenceId.New();
        var candidateId = EvidenceId.New();
        var freeId = EvidenceId.New();
        var pack = Pack(
            [Item(recordId), Item(candidateId), Item(freeId)],
            [new Candidate(candidateId, RootCauseCategory.EquipmentCalibrationOrAlignment, "workOrder")],
            [new RuledOutRecord(recordId, RootCauseCategory.SupplyChainMaterial, "It matches the specification.", "noticeMatchesPurchaseSpecification")]);
        var claim = Claim(
            [EvidenceId.New()],
            [
                new RuledOutClaim(candidateId, "The work order changed nothing."),
                new RuledOutClaim(freeId, "The answer dismissed it."),
            ]);

        var merged = MafInvestigationWorkflow.MergeRuledOut(pack, claim);

        merged.Should().HaveCount(3);
        merged[0].Id.Should().Be(recordId);
        merged[0].Source.Should().Be(RuledOutSource.Record);
        merged[0].Class.Should().Be(RootCauseCategory.SupplyChainMaterial);
        merged[0].Reason.Should().Be("It matches the specification.");
        merged[1].Id.Should().Be(candidateId);
        merged[1].Source.Should().Be(RuledOutSource.Agent);
        merged[1].Class.Should().Be(RootCauseCategory.EquipmentCalibrationOrAlignment);
        merged[1].Reason.Should().Be("The work order changed nothing.");
        merged[2].Id.Should().Be(freeId);
        merged[2].Source.Should().Be(RuledOutSource.Agent);
        merged[2].Class.Should().BeNull();
        merged[2].Reason.Should().Be("The answer dismissed it.");
    }

    [Fact]
    public void TheSameIdKeepsTheRecordAndDropsTheAgentCopy()
    {
        var id = EvidenceId.New();
        var pack = Pack(
            [Item(id)],
            [],
            [new RuledOutRecord(id, RootCauseCategory.EngineeringChange, "Document only. Setpoints were not changed.", "documentOnlyEco")]);
        var claim = Claim([], [new RuledOutClaim(id, "The answer gave a different reason.")]);

        var merged = MafInvestigationWorkflow.MergeRuledOut(pack, claim);

        merged.Should().ContainSingle();
        merged[0].Source.Should().Be(RuledOutSource.Record);
        merged[0].Class.Should().Be(RootCauseCategory.EngineeringChange);
        merged[0].Reason.Should().Be("Document only. Setpoints were not changed.");
    }

    [Fact]
    public void AnIdInBothListsIsKeptAndTheHypothesisRejectsIt()
    {
        var id = EvidenceId.New();
        var pack = Pack([Item(id)]);
        var claim = Claim([id], [new RuledOutClaim(id, "The answer also cited it.")]);

        var merged = MafInvestigationWorkflow.MergeRuledOut(pack, claim);

        merged.Should().ContainSingle(entry => entry.Id == id && entry.Source == RuledOutSource.Agent);
        var act = () => new Hypothesis(
            HypothesisId.New(),
            RootCauseCategory.ProcessDrift,
            "The process drifted.",
            0.6,
            [id],
            ["A confounder."],
            ["Watch the next lot."],
            merged);

        act.Should().Throw<DomainRuleException>().WithMessage("*both cited and ruled out*");
        MafInvestigationWorkflow.ListHit(claim, pack)!.List.Should().Be(GuardListHit.EvidenceIds);
        MafInvestigationWorkflow.ListHit(claim, pack)!.EvidenceId.Should().Be(id.Value);
    }

    [Fact]
    public void AFailureCodeNamesTheListAndACitedUnknownIdDoesNotInventOne()
    {
        var known = EvidenceId.New();
        var cited = EvidenceId.New();
        var missing = EvidenceId.New();
        var pack = Pack([Item(known)]);
        var citedOnly = Claim([cited], [new RuledOutClaim(known, "In the pack.")]);
        var claim = Claim([known], [new RuledOutClaim(missing, "Not in the pack.")]);

        MafInvestigationWorkflow.ListHit(citedOnly, pack, ["unknown-evidence-id:" + cited.Value]).Should().BeNull();
        var unknown = MafInvestigationWorkflow.ListHit(claim, pack, ["ruled-out-unknown-id:" + missing.Value]);
        unknown!.EvidenceId.Should().Be(missing.Value);
        unknown.List.Should().Be(GuardListHit.RuledOutIds);
        var reason = MafInvestigationWorkflow.ListHit(claim, pack, ["ruled-out-reason:" + missing.Value]);
        reason!.List.Should().Be(GuardListHit.RuledOutIds);
    }

    [Fact]
    public async Task ARefusedAnswerKeepsTheRuledOutIdsItNamed()
    {
        var ruledOutId = EvidenceId.New();
        var unknownCited = EvidenceId.New();
        var claim = Claim(
            [unknownCited],
            [new RuledOutClaim(ruledOutId, "It does not fit the window.")]);
        var rig = new WorkflowRig(
            ScenarioKind.EngineeringChange,
            WorkflowRig.MockChat(),
            WorkflowRig.MockChat(),
            resolution: new FixedResolutionAgent(claim));
        var incident = Incident.Open(
            IncidentId.New(),
            new ToolId("MPS-E7"),
            "E-02",
            "esc_temp",
            IncidentChannel.IotSpcAlert,
            Severity.High,
            "ESC temperature oscillation",
            "corr-ruled-out-refused",
            WorkflowRig.At);
        rig.Incidents.Add(incident);

        var started = await rig.Workflow.StartAsync(incident.Id, CancellationToken.None);

        started.Stage.Should().Be(InvestigationStage.Escalated);
        started.Hypotheses.Should().BeEmpty();
        rig.Resolution.Calls.Should().Be(1);
        var rejected = rig.Workflow.RejectedAnswerFor(started.Id);
        rejected.Should().NotBeNull();
        rejected!.RuledOutIds.Should().ContainSingle(item =>
            item.EvidenceId == ruledOutId.Value && item.Reason == "It does not fit the window.");
        rejected.EvidenceIds.Should().Contain(unknownCited.Value);
        var hit = rig.Workflow.GuardHitFor(started.Id);
        hit.Should().NotBeNull();
        hit!.EvidenceId.Should().Be(ruledOutId.Value);
        hit.List.Should().Be(GuardListHit.RuledOutIds);
    }

    private static EvidencePack Pack(
        IReadOnlyList<EvidenceItem>? items = null,
        IReadOnlyList<Candidate>? candidates = null,
        IReadOnlyList<RuledOutRecord>? ruledOut = null) =>
        new(
            items ?? [],
            [],
            [],
            [],
            [],
            [],
            [],
            false,
            SignalShape.Unknown,
            null,
            candidates,
            ruledOut);

    private static EvidenceItem Item(EvidenceId id) =>
        new(id, EvidenceSource.Cmms, "Record " + id.Value.ToString("N")[..8], "A plant record.", WorkflowRig.At, "E-02", "REC");

    private static ResolutionClaim Claim(IReadOnlyList<EvidenceId> evidenceIds, IReadOnlyList<RuledOutClaim>? ruledOut = null) =>
        new(
            RootCauseCategory.ProcessDrift,
            "The process drifted.",
            0.6,
            evidenceIds,
            ["A confounder."],
            ["Watch the next lot."],
            evidenceIds.Count == 0 ? [] : [new GroundedStatement("The process drifted.", evidenceIds)],
            ruledOutIds: ruledOut);

    private sealed class FixedResolutionAgent : IResolutionAgent
    {
        private readonly ResolutionClaim _claim;

        public FixedResolutionAgent(ResolutionClaim claim) => _claim = claim;

        public Task<ResolutionResult> ResolveAsync(ResolutionRequest request, CancellationToken cancellationToken)
        {
            _ = request;
            _ = cancellationToken;
            return Task.FromResult(ResolutionResult.Ready(_claim, 1, "test", "0"));
        }
    }
}
