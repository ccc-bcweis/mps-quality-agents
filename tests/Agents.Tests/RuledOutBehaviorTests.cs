// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.RegularExpressions;

using Microsoft.Extensions.AI;

using Mps.Agents.Evidence;
using Mps.Agents.Explain;
using Mps.Agents.Grounding;
using Mps.Agents.Impact;
using Mps.Agents.Prompts;
using Mps.Agents.Ranking;
using Mps.Agents.Reasoning;
using Mps.Agents.Resolution;
using Mps.Agents.Tools;
using Mps.Domain.Enterprise;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;
using Mps.Domain.Impact;
using Mps.Domain.Incidents;
using Mps.Domain.Telemetry;
using Mps.ModelGateway.Adapters;

namespace Mps.Agents.Tests;

/// <summary>
/// SC-7 D-02 shape: the silane notice is ruled out from the plant data, and the bearing work order stays a candidate.
/// </summary>
public sealed class RuledOutBehaviorTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private static readonly Regex GuidText = new(
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private const string NoticeReason = "Lot SIH4-088 (SN-SIH4-088) matches its specification.";

    private const string DocumentReason = "ECO-61234 is a document-only change. Setpoints were not changed.";

    private const string InspectionReason = "Work order WO-71234 (preventive maintenance) was an inspection with no findings. Nothing was changed.";

    [Fact]
    public void ThePackPutsEachRecordInOneListAndLeavesSignalsOut()
    {
        var pack = ChamberPack();

        pack.Items.Should().OnlyContain(item => !item.Title.StartsWith(RuledOut.Prefix, StringComparison.Ordinal));
        pack.Items.Single(item => item.ExternalRef == "SN-SIH4-088").Title.Should().Be("Silane lot SIH4-088 matches the purchase specification.");
        pack.Items.Single(item => item.ExternalRef == "WO-PMP-D02-BRG").Title.Should().Contain("past the service interval");

        var listed = pack.Candidates.Select(candidate => candidate.Id).Concat(pack.RuledOut.Select(record => record.Id)).ToArray();
        listed.Should().OnlyHaveUniqueItems();
        foreach (var reference in new[] { "ECO-61234", "ECO-4471", "WO-PMP-D02-BRG", "WO-71234", "WO-ALN-D02", "CAL-D02-OVER", "CAL-D02-OK", "SIH4-088", "SN-SIH4-088", "USER-R1" })
        {
            listed.Should().Contain(Id(pack, reference));
        }

        foreach (var item in pack.Items.Where(item => item.Source is EvidenceSource.Spc or EvidenceSource.Telemetry or EvidenceSource.Document))
        {
            listed.Should().NotContain(item.Id);
        }

        Candidate(pack, "WO-PMP-D02-BRG", RootCauseCategory.EquipmentCalibrationOrAlignment, "workOrder");
        Candidate(pack, "ECO-4471", RootCauseCategory.EngineeringChange, "eco");
        Candidate(pack, "WO-ALN-D02", RootCauseCategory.EquipmentCalibrationOrAlignment, "alignment");
        Candidate(pack, "CAL-D02-OVER", RootCauseCategory.EquipmentCalibrationOrAlignment, "calibration");
        Candidate(pack, "CAL-D02-OK", RootCauseCategory.EquipmentCalibrationOrAlignment, "calibration");
        Candidate(pack, "SIH4-088", RootCauseCategory.SupplyChainMaterial, "materialLot");
        Candidate(pack, "USER-R1", RootCauseCategory.EquipmentCalibrationOrAlignment, "userEvidence");

        Ruled(pack, "SN-SIH4-088", RootCauseCategory.SupplyChainMaterial, "noticeMatchesPurchaseSpecification", NoticeReason);
        Ruled(pack, "ECO-61234", RootCauseCategory.EngineeringChange, "documentOnlyEco", DocumentReason);
        Ruled(pack, "WO-71234", RootCauseCategory.EquipmentCalibrationOrAlignment, "inspectionWithNoFindings", InspectionReason);
        pack.Candidates.Should().HaveCount(7);
        pack.RuledOut.Should().HaveCount(3);
    }

    [Fact]
    public void TheRequestListsCandidatesAndRuledOutAndDropsRuledOutIdsFromTheClassLists()
    {
        var pack = ChamberPack();
        var request = ResolutionRequest.From(pack, new ImpactFeatures(ImpactScopeKind.Chamber, "D-02", pack.SignalShape, null, null));

        request.Candidates.Should().Contain(note =>
            note.Id == Id(pack, "WO-PMP-D02-BRG").ToString()
            && note.Class == "equipmentCalibrationOrAlignment"
            && note.Kind == "workOrder");
        request.RuledOut.Should().Contain(note =>
            note.Id == Id(pack, "SN-SIH4-088").ToString()
            && note.Class == "supplyChainMaterial"
            && note.Reason == NoticeReason);
        request.NoticeIds.Should().NotContain("SN-SIH4-088");
        request.EcoIds.Should().Contain("ECO-61234").And.Contain("ECO-4471");
        request.EcoEvidenceIds.Should().Contain(Id(pack, "ECO-4471").ToString()).And.NotContain(Id(pack, "ECO-61234").ToString());
        request.PmEvidenceIds.Should().Contain(Id(pack, "WO-PMP-D02-BRG").ToString()).And.NotContain(Id(pack, "WO-71234").ToString());
        request.AlignmentEvidenceIds.Should().Equal(Id(pack, "WO-ALN-D02").ToString());
        request.LotEvidenceIds.Should().Equal(Id(pack, "SIH4-088").ToString());
        request.LotIds.Should().Contain("SIH4-088");
        request.OverdueEvidenceIds.Should().Equal(Id(pack, "CAL-D02-OVER").ToString());
        request.Notes.Single(note => note.ExternalRef == "SN-SIH4-088").Status.Should().Be("ruledOut");
        request.Notes.Single(note => note.ExternalRef == "WO-71234").Status.Should().Be("ruledOut");
        request.Notes.Single(note => note.ExternalRef == "ECO-61234").Status.Should().Be("ruledOut");
        request.Notes.Single(note => note.ExternalRef == "WO-PMP-D02-BRG").Status.Should().Be("candidate");
        request.Notes.Single(note => note.ExternalRef == "USER-R1").Status.Should().Be("candidate");
        request.Notes.Single(note => note.Source == "spc").Status.Should().Be("context");
        request.Notes.Single(note => note.Source == "telemetry").Status.Should().Be("context");

        var signals = EvidenceSamples.Pack(
        [
            EvidenceSamples.Item(1, EvidenceSource.ManualMeasurement, "Bench reading"),
            EvidenceSamples.Item(2, EvidenceSource.Ticketing, "The open ticket"),
            EvidenceSamples.Item(3, EvidenceSource.Cmms, "Work order"),
        ]);
        var statuses = ResolutionRequest.From(signals, new ImpactFeatures(ImpactScopeKind.Tool, "D-02", SignalShape.Unknown, null, null));
        statuses.Notes.Select(note => note.Status).Should().Equal("context", "context", "candidate");
    }

    [Fact]
    public void TheParserKeepsRuledOutIdsAndDoesNotDropABadEntry()
    {
        var id = EvidenceSamples.Id(2);
        var json = "{"
            + "\"category\":\"processDrift\",\"cause\":\"The chamber seasons after a clean.\",\"confidence\":0.7,\"shape\":\"ramp\","
            + "\"evidenceIds\":[\"" + EvidenceSamples.Id(1) + "\"],"
            + "\"ruledOutIds\":[{\"evidenceId\":\"" + id + "\",\"reason\":\"The notice matches its specification.\"}],"
            + "\"counterEvidence\":[],\"actions\":[]}";

        ResolutionJson.TryParseClaim(json, out var claim, out var escalate, out var error).Should().BeTrue(error);
        escalate.Should().BeNull();
        claim!.RuledOutIds.Should().ContainSingle(item => item.Id == id && item.Reason == "The notice matches its specification.");

        var legacy = json.Replace(",\"ruledOutIds\":[{\"evidenceId\":\"" + id + "\",\"reason\":\"The notice matches its specification.\"}]", string.Empty, StringComparison.Ordinal);
        ResolutionJson.TryParseClaim(legacy, out var old, out _, out var oldError).Should().BeTrue(oldError);
        old!.RuledOutIds.Should().BeEmpty();

        var broken = json.Replace(
            "{\"evidenceId\":\"" + id + "\",\"reason\":\"The notice matches its specification.\"}",
            "null",
            StringComparison.Ordinal);
        ResolutionJson.TryParseClaim(broken, out var kept, out _, out var brokenError).Should().BeTrue(brokenError);
        kept!.RuledOutIds.Should().ContainSingle();
        var failures = GroundingValidator.Validate(kept, [EvidenceSamples.Item(1, EvidenceSource.Plm, "Change", "ECO-4471")]).Failures;
        failures.Should().Contain(failure => failure.StartsWith("ruled-out-unknown-id:", StringComparison.Ordinal));
        failures.Should().Contain(failure => failure.StartsWith("ruled-out-reason:", StringComparison.Ordinal));
    }

    [Fact]
    public void GroundingRejectsABadRuledOutEntryAndAllowsACandidateOrAPlantReason()
    {
        var cited = EvidenceSamples.Item(1, EvidenceSource.Plm, "Edge ring revision C", "ECO-4471");
        var other = EvidenceSamples.Item(2, EvidenceSource.Cmms, "Scheduled maintenance", "WO-19");
        var spare = EvidenceSamples.Item(3, EvidenceSource.Erp, "Silane lot matches the purchase specification.", "SN-SIH4-088");
        var pack = new[] { cited, other, spare };
        var plain = "The maintenance window did not change the chamber.";

        var allowed = Claim(cited.Id, [new RuledOutClaim(other.Id, plain)]);
        var allowedResult = GroundingValidator.Validate(allowed, pack);
        allowedResult.Failures.Should().NotContain(failure => failure.StartsWith("ruled-out-", StringComparison.Ordinal));
        allowedResult.Accepted.Should().BeTrue();

        var uncovered = Claim(cited.Id, []);
        GroundingValidator.Validate(uncovered, pack).Failures.Should().NotContain(failure => failure.StartsWith("ruled-out-", StringComparison.Ordinal));
        GroundingValidator.Validate(uncovered, pack).Accepted.Should().BeTrue();

        var plant = Claim(cited.Id, [new RuledOutClaim(other.Id, DocumentReason)]);
        GroundingValidator.Validate(plant, pack).Failures.Should().NotContain(failure => failure.StartsWith("ruled-out-", StringComparison.Ordinal));

        var both = Claim(cited.Id, [new RuledOutClaim(cited.Id, plain)]);
        both.EvidenceIds.Should().Contain(cited.Id);
        both.RuledOutIds.Should().Contain(item => item.Id == cited.Id);
        GroundingValidator.Validate(both, pack).Failures.Should().Contain("ruled-out-and-cited:" + cited.Id);

        GroundingValidator.Validate(Claim(cited.Id, [new RuledOutClaim(EvidenceSamples.Id(99), plain)]), pack).Failures
            .Should().Contain("ruled-out-unknown-id:" + EvidenceSamples.Id(99));
        GroundingValidator.Validate(Claim(cited.Id, [new RuledOutClaim(other.Id, "not a sentence")]), pack).Failures
            .Should().Contain("ruled-out-reason:" + other.Id);
        GroundingValidator.Validate(Claim(cited.Id, [new RuledOutClaim(other.Id, "The lot is fine. The chamber was not touched.")]), pack).Failures
            .Should().Contain("ruled-out-reason:" + other.Id);
        GroundingValidator.Validate(Claim(cited.Id, [new RuledOutClaim(other.Id, " ")]), pack).Failures
            .Should().Contain("ruled-out-reason:" + other.Id);

        var longReason = "A" + new string('a', PlainSentence.MaxLength - 1) + ".";
        longReason.Length.Should().Be(PlainSentence.MaxLength + 1);
        GroundingValidator.Validate(Claim(cited.Id, [new RuledOutClaim(other.Id, longReason)]), pack).Failures
            .Should().Contain("ruled-out-reason:" + other.Id);
    }

    [Fact]
    public void TheRankerAndTheMockEmitRuledOutIdsAndDoNotCiteThem()
    {
        var (request, bearing, notice, reason) = BearingRequest();
        var items = new[]
        {
            EvidenceSamples.Item(11, EvidenceSource.Cmms, "Foreline pump bearing", "WO-PMP-D02-BRG"),
            EvidenceSamples.Item(12, EvidenceSource.Erp, "Silane lot SIH4-088 matches the purchase specification.", "SN-SIH4-088"),
        };

        var decision = RootCauseRanker.Rank(request);
        decision.Escalate.Should().BeFalse();
        decision.Claim!.Category.Should().Be(RootCauseCategory.EquipmentCalibrationOrAlignment);
        decision.Claim.EvidenceIds.Should().Contain(bearing).And.NotContain(notice);
        decision.Claim.RuledOutIds.Should().ContainSingle(item => item.Id == notice && item.Reason == reason);
        GroundingValidator.Validate(decision.Claim, items).Accepted.Should().BeTrue();

        var reasoner = new QualityMockReasoner();
        var response = reasoner.Complete(
            [
                new ChatMessage(ChatRole.System, "You are the resolution agent."),
                new ChatMessage(ChatRole.User, ResolutionJson.SerializeRequest(request)),
            ],
            null);
        ResolutionJson.TryParseClaim(response.Text, out var claim, out var escalate, out var error).Should().BeTrue(error);
        escalate.Should().BeNull();
        claim!.EvidenceIds.Should().NotContain(notice);
        claim.RuledOutIds.Should().ContainSingle(item => item.Id == notice && item.Reason == reason);
    }

    [Fact]
    public async Task TheLiveModelSeesHandlesAndAnswersMapBackBeforeGrounding()
    {
        var bearing = EvidenceSamples.Id(1);
        var notice = EvidenceSamples.Id(2);
        var request = HandleRequest(bearing, notice);
        var handled = request.WithHandles();
        handled.Notes.Select(note => note.Id).Should().Equal("E01", "E02");
        var answer = Answer("E01", "E02");
        var chat = new ScriptedChatClient(Text(answer));
        var agent = new ResolutionAgent(chat, PromptCatalog.Load());

        var result = await agent.ResolveAsync(request, CancellationToken.None);

        result.Escalated.Should().BeFalse();
        var user = string.Join('\n', chat.Seen.Where(message => message.Role == ChatRole.User).Select(message => message.Text));
        GuidText.IsMatch(user).Should().BeFalse();
        user.Should().NotContain(bearing.ToString()).And.NotContain(notice.ToString());
        user.Should().Contain("\"E01\"").And.Contain("\"E02\"");
        result.Claim!.EvidenceIds.Should().ContainSingle().Which.Should().Be(bearing);
        result.Claim.RuledOutIds.Should().ContainSingle(item => item.Id == notice && item.Reason == "The silane lot matches its specification.");
        result.Claim.Explanations.Should().ContainSingle(item => item.EvidenceId == bearing);
        result.Claim.EvidenceMatrix.Should().OnlyContain(item => item.EvidenceId == bearing);

        var items = new[]
        {
            EvidenceSamples.Item(1, EvidenceSource.Cmms, "Foreline pump bearing", "WO-PMP-D02-BRG"),
            EvidenceSamples.Item(2, EvidenceSource.Erp, "Silane lot matches the purchase specification.", "SN-SIH4-088"),
        };
        GroundingValidator.Validate(result.Claim, items).Accepted.Should().BeTrue();
    }

    [Fact]
    public async Task AnUnknownHandleStaysUnresolvableAndFailsGrounding()
    {
        var bearing = EvidenceSamples.Id(1);
        var notice = EvidenceSamples.Id(2);
        var request = HandleRequest(bearing, notice);
        request.WithHandles().ResolveHandle("E99").Should().BeNull();
        var chat = new ScriptedChatClient(Text(Answer("E99", "E02")));
        var agent = new ResolutionAgent(chat, PromptCatalog.Load());

        var result = await agent.ResolveAsync(request, CancellationToken.None);

        result.Escalated.Should().BeFalse();
        result.Claim!.EvidenceIds.Should().ContainSingle();
        result.Claim.EvidenceIds[0].Should().NotBe(bearing).And.NotBe(notice);
        result.Claim.RuledOutIds.Should().ContainSingle(item => item.Id == notice);
        var items = new[]
        {
            EvidenceSamples.Item(1, EvidenceSource.Cmms, "Foreline pump bearing", "WO-PMP-D02-BRG"),
            EvidenceSamples.Item(2, EvidenceSource.Erp, "Silane lot matches the purchase specification.", "SN-SIH4-088"),
        };
        GroundingValidator.Validate(result.Claim, items).Failures.Should().Contain("unknown-evidence-id:" + result.Claim.EvidenceIds[0]);

        var ruledOut = new ScriptedChatClient(Text(Answer("E01", "E99")));
        var ruled = await new ResolutionAgent(ruledOut, PromptCatalog.Load()).ResolveAsync(request, CancellationToken.None);
        ruled.Escalated.Should().BeFalse();
        ruled.Claim!.EvidenceIds.Should().ContainSingle().Which.Should().Be(bearing);
        ruled.Claim.RuledOutIds.Should().ContainSingle();
        ruled.Claim.RuledOutIds[0].Id.Should().NotBe(notice).And.NotBe(bearing);
        GroundingValidator.Validate(ruled.Claim, items).Failures.Should().Contain("ruled-out-unknown-id:" + ruled.Claim.RuledOutIds[0].Id);
    }

    private static ResolutionClaim Claim(EvidenceId cited, IReadOnlyList<RuledOutClaim> ruledOut) =>
        new(
            RootCauseCategory.EngineeringChange,
            "Edge ring revision C changed the chamber.",
            0.8d,
            [cited],
            ["The maintenance window is a confounder."],
            ["Review the change before opening a corrective action."],
            [],
            null,
            null,
            "oscillation",
            ruledOut);

    private static (ResolutionRequest Request, EvidenceId Bearing, EvidenceId Notice, string Reason) BearingRequest()
    {
        var bearing = EvidenceSamples.Id(11);
        var notice = EvidenceSamples.Id(12);
        var request = new ResolutionRequest
        {
            ScopeKind = "chamber",
            ScopeKey = "D-02",
            SignalShape = "ramp",
            PmSummaries = ["WO-PMP-D02-BRG maintenance: Foreline pump bearing hours are past the service interval on D-02. Vibration has been rising."],
            PmEvidenceIds = [bearing.ToString(), notice.ToString()],
            LotIds = ["SIH4-088"],
            RuledOut = [new RuledOutNote { Id = notice.ToString(), Class = "supplyChainMaterial", Reason = NoticeReason }],
        };
        return (request, bearing, notice, NoticeReason);
    }

    private static ResolutionRequest HandleRequest(EvidenceId bearing, EvidenceId notice) =>
        new()
        {
            ScopeKind = "chamber",
            ScopeKey = "D-02",
            SignalShape = "ramp",
            PmEvidenceIds = [bearing.ToString()],
            Notes =
            [
                new EvidenceNote
                {
                    Id = bearing.ToString(),
                    Source = "cmms",
                    Title = "Foreline pump bearing",
                    Detail = "Foreline pump bearing hours are past the service interval on D-02.",
                    ExternalRef = "WO-PMP-D02-BRG",
                    Status = "candidate",
                },
                new EvidenceNote
                {
                    Id = notice.ToString(),
                    Source = "erp",
                    Title = "Silane lot matches the purchase specification.",
                    Detail = "Silane lot SIH4-088 matches the purchase specification.",
                    ExternalRef = "SN-SIH4-088",
                    Status = "ruledOut",
                },
            ],
            Candidates = [new CandidateNote { Id = bearing.ToString(), Class = "equipmentCalibrationOrAlignment", Kind = "workOrder" }],
            RuledOut = [new RuledOutNote { Id = notice.ToString(), Class = "supplyChainMaterial", Reason = NoticeReason }],
        };

    private static string Answer(string evidenceHandle, string ruledOutHandle) =>
        "{"
        + "\"category\":\"equipmentCalibrationOrAlignment\",\"shape\":\"ramp\","
        + "\"cause\":\"The pump bearing is past its service interval and vibration is rising.\","
        + "\"confidence\":0.83,"
        + "\"evidenceIds\":[\"" + evidenceHandle + "\"],"
        + "\"ruledOutIds\":[{\"evidenceId\":\"" + ruledOutHandle + "\",\"reason\":\"The silane lot matches its specification.\"}],"
        + "\"counterEvidence\":[],"
        + "\"actions\":[\"Containment: Hold the chamber.\",\"Preventive: Replace the bearing.\"],"
        + "\"whyItMatters\":[{\"evidenceId\":\"" + evidenceHandle + "\",\"whyItMatters\":\"The bearing hours are past the service interval.\"}],"
        + "\"evidenceMatrix\":["
        + "{\"category\":\"engineeringChange\",\"evidenceId\":\"" + evidenceHandle + "\",\"stance\":\"rulesOut\"},"
        + "{\"category\":\"equipmentCalibrationOrAlignment\",\"evidenceId\":\"" + evidenceHandle + "\",\"stance\":\"supports\"},"
        + "{\"category\":\"supplyChainMaterial\",\"evidenceId\":\"" + evidenceHandle + "\",\"stance\":\"rulesOut\"},"
        + "{\"category\":\"processDrift\",\"evidenceId\":\"" + evidenceHandle + "\",\"stance\":\"rulesOut\"}"
        + "]}";

    private static ChatResponse Text(string text) => new(new ChatMessage(ChatRole.Assistant, text));

    private static void Candidate(EvidencePack pack, string reference, RootCauseCategory category, string kind)
    {
        var id = Id(pack, reference);
        pack.Candidates.Should().ContainSingle(candidate => candidate.Id == id && candidate.Class == category && candidate.Kind == kind);
        pack.RuledOut.Should().NotContain(record => record.Id == id);
    }

    private static void Ruled(EvidencePack pack, string reference, RootCauseCategory category, string ruleId, string reason)
    {
        var id = Id(pack, reference);
        pack.RuledOut.Should().ContainSingle(record => record.Id == id && record.Class == category && record.RuleId == ruleId && record.Reason == reason);
        pack.Candidates.Should().NotContain(candidate => candidate.Id == id);
    }

    private static EvidenceId Id(EvidencePack pack, string reference) => pack.Items.Single(item => item.ExternalRef == reference).Id;

    private static EvidencePack ChamberPack()
    {
        var tool = new ToolId("D-02");
        var incident = Incident.Open(
            IncidentId.New(),
            tool,
            "D-02-A",
            "vibration",
            IncidentChannel.IotSpcAlert,
            Severity.High,
            "Pump bearing vibration",
            "corr-sc7",
            At);
        var harvest = new ToolHarvest();
        harvest.Violations.Add(new SpcViolation("Western Electric", tool, "D-02-A", "vibration", At, 4d, 1d, Severity.High));
        harvest.Telemetry = new TelemetryWindow(tool, "vibration", At.AddHours(-1), At, [new TelemetryPoint(At.AddMinutes(-2), 1d), new TelemetryPoint(At, 4d)]);
        harvest.Ecos.Add(new EcoRecord("ECO-61234", "Operating note update on D-02", "D-02", null, At.AddDays(-3), "Document-only update to the D-02 operating note. Setpoints were not changed."));
        harvest.Ecos.Add(new EcoRecord("ECO-4471", "Edge ring rev C", "D-02", "OXIDE-ETCH", At.AddDays(-2), "Install edge ring rev C."));
        harvest.WorkOrders.Add(new WorkOrder(
            "WO-PMP-D02-BRG",
            tool,
            "D-02-A",
            "maintenance",
            At.AddDays(-1),
            "Foreline pump bearing hours are past the service interval on D-02. Vibration has been rising. The bearing was not replaced."));
        harvest.WorkOrders.Add(new WorkOrder(
            "WO-71234",
            tool,
            "D-02-A",
            "preventive maintenance",
            At.AddDays(-1),
            "Routine preventive maintenance the same week. Inspection only: no parts changed and no settings changed. No findings."));
        harvest.WorkOrders.Add(new WorkOrder("WO-ALN-D02", tool, "D-02-A", "alignment", At.AddDays(-4), "Chamber alignment checked and left as found."));
        harvest.Calibrations.Add(new CalibrationRecord("CAL-D02-OVER", tool, "vibration probe", At.AddDays(-10), null, true));
        harvest.Calibrations.Add(new CalibrationRecord("CAL-D02-OK", tool, "temperature probe", At.AddDays(20), At.AddDays(-5), false));
        harvest.MaterialLots.Add(new MaterialLot("SIH4-088", "GAS-SIH4", "Cinderwell Specialty Gases", At.AddDays(-6), "SN-SIH4-088"));
        harvest.Notices.Add(new SupplierNotice("SN-SIH4-088", "Cinderwell", "GAS-SIH4", "SIH4-088", "Silane lot SIH4-088 matches the purchase specification.", At.AddDays(-6)));
        harvest.Documents.Add(new DocumentHit("DOC-1", "Chamber procedure", "How to vent the chamber.", "deposition", "A"));
        harvest.CalibrationLookupCompleted = true;

        var person = new EvidenceItem(
            EvidenceIds.Create(EvidenceSource.UserProvided, "USER-R1"),
            EvidenceSource.UserProvided,
            "Line note",
            "The pump bearing is worn past its service interval.",
            At,
            "D-02",
            "USER-R1",
            null,
            null,
            "the line");
        return EvidencePackFactory.Build(incident, harvest).Pack.Including([person], WhyItMattersCatalog.Shared);
    }
}
