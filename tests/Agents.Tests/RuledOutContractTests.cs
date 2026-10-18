// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Mps.Agents.Evaluation;
using Mps.Agents.Evidence;
using Mps.Agents.Explain;
using Mps.Agents.Grounding;
using Mps.Agents.Impact;
using Mps.Agents.Ranking;
using Mps.Agents.Resolution;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;
using Mps.Domain.Impact;

namespace Mps.Agents.Tests;

public sealed class RuledOutContractTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void PackListsDefaultToEmptyAndStayOnIncluding()
    {
        var cited = EvidenceSamples.Id(1);
        var ruledOut = EvidenceSamples.Id(2);
        var pack = new EvidencePack(
            [EvidenceSamples.Item(1, EvidenceSource.Mes, "Run")],
            [],
            [],
            [],
            [],
            [],
            [],
            false,
            SignalShape.Unknown,
            null,
            [new Candidate(cited, RootCauseCategory.ProcessDrift, "userEvidence")],
            [new RuledOutRecord(ruledOut, RootCauseCategory.SupplyChainMaterial, "Matches the specification.", "noticeMatchesPurchaseSpecification")]);

        pack.Candidates.Should().ContainSingle(candidate => candidate.Id == cited && candidate.Kind == "userEvidence");
        pack.RuledOut.Should().ContainSingle(record => record.Id == ruledOut && record.RuleId == "noticeMatchesPurchaseSpecification");

        var expanded = pack.Including([EvidenceSamples.Item(3, EvidenceSource.Telemetry, "Added trace")], WhyItMattersCatalog.Shared);

        expanded.Items.Should().HaveCount(2);
        expanded.Candidates.Should().Equal(pack.Candidates);
        expanded.RuledOut.Should().Equal(pack.RuledOut);
        EvidenceSamples.Pack().Candidates.Should().BeEmpty();
        EvidenceSamples.Pack().RuledOut.Should().BeEmpty();
    }

    [Fact]
    public void ARecordIdIsInAtMostOneList()
    {
        var id = EvidenceSamples.Id(4);
        var act = () => new EvidencePack(
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            false,
            SignalShape.Unknown,
            null,
            [new Candidate(id, RootCauseCategory.EngineeringChange, "eco")],
            [new RuledOutRecord(id, RootCauseCategory.EngineeringChange, "Document only.", "documentOnlyEco")]);

        act.Should().Throw<ArgumentException>().WithMessage("*at most one*");
    }

    [Fact]
    public void ResolutionClaimKeepsRuledOutIds()
    {
        var bare = new ResolutionClaim(null, null, null, [], [], [], []);
        bare.RuledOutIds.Should().BeEmpty();

        var id = EvidenceId.New();
        var claim = new ResolutionClaim(
            null,
            null,
            null,
            [],
            [],
            [],
            [],
            ruledOutIds: [new RuledOutClaim(id, "It does not fit.")]);

        claim.RuledOutIds.Should().ContainSingle(item => item.Id == id && item.Reason == "It does not fit.");
    }

    [Fact]
    public void NoteStatusDefaultsToContextAndFromMarksSignals()
    {
        new EvidenceNote().Status.Should().Be("context");
        JsonSerializer.Deserialize<EvidenceNote>("""{"status":null}""", Json)!.Status.Should().Be("context");
        JsonSerializer.Deserialize<EvidenceNote>("{}", Json)!.Status.Should().Be("context");

        var note = new EvidenceNote { Status = "ruledOut" };
        note.Status.Should().Be("ruledOut");
        note.Status = "  ";
        note.Status.Should().Be("context");

        var request = ResolutionRequest.From(
            EvidenceSamples.Pack(
            [
                EvidenceSamples.Item(1, EvidenceSource.Spc, "Rule break"),
                EvidenceSamples.Item(2, EvidenceSource.Telemetry, "Trace"),
                EvidenceSamples.Item(3, EvidenceSource.Cmms, "Work order"),
            ]),
            new ImpactFeatures(ImpactScopeKind.Tool, "E-02", SignalShape.Unknown, null, null));

        request.Notes.Select(item => item.Status).Should().Equal("context", "context", "candidate");
        request.Candidates.Should().BeEmpty();
        request.RuledOut.Should().BeEmpty();
    }

    [Fact]
    public void CandidateAndRuledOutNotesUseCamelCaseNames()
    {
        var candidates = JsonSerializer.Serialize(
            new CandidateNote { Id = "1", Class = "processDrift", Kind = "workOrder" },
            Json);
        candidates.Should().Contain("\"class\":\"processDrift\"").And.Contain("\"kind\":\"workOrder\"");

        var ruledOut = JsonSerializer.Serialize(
            new RuledOutNote { Id = "2", Class = "supplyChainMaterial", Reason = "Matches the specification." },
            Json);
        ruledOut.Should().Contain("\"class\":\"supplyChainMaterial\"").And.Contain("\"reason\":\"Matches the specification.\"");
    }

    [Fact]
    public void WithHandlesReplacesPackIdsAndResolvesThem()
    {
        var first = EvidenceSamples.Id(1).ToString();
        var second = EvidenceSamples.Id(2).ToString();
        var outside = EvidenceSamples.Id(9).ToString();
        var request = new ResolutionRequest
        {
            EcoIds = ["ECO-4471"],
            LotIds = ["SIH4-088"],
            NoticeIds = ["SN-SIH4-088"],
            PmSummaries = ["WO-1 inspection"],
            EcoEvidenceIds = [first],
            LotEvidenceIds = [second, outside],
            SignalEvidenceIds = [first],
            UserEvidenceIds = [second],
            PmEvidenceIds = [outside],
            OverdueEvidenceIds = [first],
            AlignmentEvidenceIds = [second],
            Feedback = "Name the bearing.",
            Notes =
            [
                new EvidenceNote { Id = first, Source = "cmms", Title = "Bearing", Status = "candidate" },
                new EvidenceNote { Id = second, Source = "erp", Title = "Notice", Status = "ruledOut" },
            ],
            Candidates = [new CandidateNote { Id = first, Class = "equipmentCalibrationOrAlignment", Kind = "workOrder" }],
            RuledOut = [new RuledOutNote { Id = second, Class = "supplyChainMaterial", Reason = "Matches the specification." }],
        };

        var handled = request.WithHandles();

        request.Notes[0].Id.Should().Be(first);
        handled.Notes.Select(note => note.Id).Should().Equal("E01", "E02");
        handled.Notes[0].Status.Should().Be("candidate");
        handled.Notes[1].Title.Should().Be("Notice");
        handled.Candidates.Should().ContainSingle(candidate =>
            candidate.Id == "E01" && candidate.Class == "equipmentCalibrationOrAlignment" && candidate.Kind == "workOrder");
        handled.RuledOut.Should().ContainSingle(record =>
            record.Id == "E02" && record.Class == "supplyChainMaterial" && record.Reason == "Matches the specification.");
        handled.EcoEvidenceIds.Should().Equal("E01");
        handled.SignalEvidenceIds.Should().Equal("E01");
        handled.OverdueEvidenceIds.Should().Equal("E01");
        handled.UserEvidenceIds.Should().Equal("E02");
        handled.AlignmentEvidenceIds.Should().Equal("E02");
        handled.LotEvidenceIds.Should().Equal("E02", outside);
        handled.PmEvidenceIds.Should().Equal(outside);
        handled.EcoIds.Should().Equal("ECO-4471");
        handled.LotIds.Should().Equal("SIH4-088");
        handled.NoticeIds.Should().Equal("SN-SIH4-088");
        handled.PmSummaries.Should().Equal("WO-1 inspection");
        handled.Feedback.Should().Be("Name the bearing.");

        handled.ResolveHandle("E01").Should().Be(first);
        handled.ResolveHandle("e02").Should().Be(second);
        handled.ResolveHandle(first).Should().Be(first);
        handled.ResolveHandle(outside).Should().Be(outside);
        handled.ResolveHandle("E03").Should().BeNull();
        handled.ResolveHandle("ECO-4471").Should().BeNull();
        handled.ResolveHandle("  ").Should().BeNull();
        request.ResolveHandle(first).Should().Be(first);
        request.ResolveHandle("E01").Should().BeNull();

        var json = ResolutionJson.SerializeRequest(handled);
        json.Should().Contain("\"E01\"").And.NotContain("handleMap").And.NotContain(first);
    }

    [Fact]
    public void HandlesPadToThreeDigitsAfterNinetyNine()
    {
        var notes = Enumerable.Range(1, 100)
            .Select(number => new EvidenceNote { Id = EvidenceSamples.Id(number).ToString() })
            .ToArray();

        var handled = new ResolutionRequest { Notes = notes }.WithHandles();

        handled.Notes[0].Id.Should().Be("E01");
        handled.Notes[98].Id.Should().Be("E99");
        handled.Notes[99].Id.Should().Be("E100");
        handled.ResolveHandle("E100").Should().Be(notes[99].Id);
    }

    [Fact]
    public void RejectedAnswerKeepsRuledOutIds()
    {
        new Mps.Application.Evidence.RejectedAnswer("cause", RootCauseCategory.ProcessDrift, [Guid.NewGuid()]).RuledOutIds.Should().BeEmpty();
        new Mps.Application.Evidence.RejectedAnswer("cause", null, [], null).RuledOutIds.Should().BeEmpty();

        var id = Guid.NewGuid();
        var rejected = new Mps.Application.Evidence.RejectedAnswer(
            "cause",
            null,
            [],
            [new Mps.Application.Evidence.RejectedRuledOut(id, "It does not fit.")]);
        rejected.RuledOutIds.Should().ContainSingle(item => item.EvidenceId == id && item.Reason == "It does not fit.");
    }

    [Fact]
    public void EvalExpectationCarriesSupportAndRuledOutLists()
    {
        var expected = new EvalExpectation();
        expected.ForbiddenInSupport.Should().BeEmpty();
        expected.RequiredRuledOut.Should().BeEmpty();
        expected.RequiredSupport.Should().BeEmpty();

        expected.ForbiddenInSupport = ["SN-1"];
        expected.RequiredRuledOut = ["SN-1"];
        expected.RequiredSupport = ["WO-1"];
        var json = JsonSerializer.Serialize(expected, Json);
        json.Should().Contain("\"forbiddenInSupport\"");
        json.Should().Contain("\"requiredRuledOut\"");
        json.Should().Contain("\"requiredSupport\"");
    }
}
