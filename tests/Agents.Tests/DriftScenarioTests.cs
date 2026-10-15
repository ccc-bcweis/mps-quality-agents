// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Extensions.AI;

using Mps.Agents.Prompts;
using Mps.Agents.Ranking;
using Mps.Agents.Resolution;
using Mps.Domain.Hypotheses;
using Mps.ModelGateway.Replay;

namespace Mps.Agents.Tests;

/// <summary>
/// SC-4 must stay process drift when a document-only liner note and an in-spec gas lot sit in the window.
/// The replay engine serves that decision without calling a network model.
/// </summary>
public sealed class DriftScenarioTests
{
    private static readonly string Signal = Guid.Parse("00000000-0000-0000-0000-0000000000a4").ToString("D");

    private static readonly string EcoEvidence = Guid.Parse("00000000-0000-0000-0000-0000000000a5").ToString("D");

    private static readonly string LotEvidence = Guid.Parse("00000000-0000-0000-0000-0000000000a6").ToString("D");

    [Fact]
    public void ADocumentOnlyNoteAndAnInSpecLotDoNotStealProcessDrift()
    {
        var decision = RootCauseRanker.Rank(SeasoningRequest());

        decision.Escalate.Should().BeFalse();
        decision.Claim!.Category.Should().Be(RootCauseCategory.ProcessDrift);
        decision.Claim.Category.Should().NotBe(RootCauseCategory.EngineeringChange);
        decision.Claim.Cause.Should().Contain("seasoning");
        decision.Claim.Cause.Should().Contain("clean");
        decision.Claim.Cause.Should().NotContain("ECO-4502");
        decision.Claim.CounterEvidence.Should().Contain(item => item.Contains("document-only", StringComparison.OrdinalIgnoreCase));
        decision.Claim.CounterEvidence.Should().Contain(item => item.Contains("ECO-4502", StringComparison.Ordinal));
        decision.Claim.Actions.Should().Contain(action => action.Contains("chamber clean", StringComparison.Ordinal) && action.Contains("CAPA", StringComparison.Ordinal));
    }

    [Fact]
    public void AHardwareChangeWithUnchangedSetpointsStaysAnEngineeringChange()
    {
        var decision = RootCauseRanker.Rank(new ResolutionRequest
        {
            ScopeKind = "chamber",
            ScopeKey = "E-02",
            SignalShape = "oscillation",
            EcoIds = ["ECO-4471"],
            EcoEvidenceIds = [EcoEvidence],
            Notes =
            [
                new EvidenceNote
                {
                    Id = EcoEvidence,
                    ExternalRef = "ECO-4471",
                    Title = "Edge ring revision C on etch E-02",
                    Detail = "Replace the chamber A edge ring with revision C. OXIDE-ETCH setpoints are unchanged.",
                },
            ],
        });

        decision.Claim!.Category.Should().Be(RootCauseCategory.EngineeringChange);
        decision.Claim.Cause.Should().Contain("ECO-4471");
    }

    [Fact]
    public void AStepOnTwoToolsWithAnOutOfSpecLotBeatsARealEngineeringChange()
    {
        var decision = RootCauseRanker.Rank(new ResolutionRequest
        {
            ScopeKind = "chamber",
            ScopeKey = "E-02",
            SignalShape = "step",
            EcoIds = ["ECO-4471"],
            EcoEvidenceIds = [EcoEvidence],
            LotIds = ["CF4-014", "PL-2044"],
            NoticeIds = ["SN-PL-2044"],
            LotEvidenceIds = [LotEvidence],
            Notes =
            [
                new EvidenceNote
                {
                    Id = EcoEvidence,
                    ExternalRef = "ECO-4471",
                    Title = "Edge ring revision C",
                    Detail = "Replace the edge ring. Setpoints are unchanged.",
                },
                new EvidenceNote
                {
                    ExternalRef = "SN-PL-2044",
                    Title = "Lot PL-2044 purity certificate is out of spec",
                    Detail = "Lot PL-2044 went into service on E-01 and E-02. The certificate is out of specification.",
                },
                new EvidenceNote
                {
                    ExternalRef = "CF4-014",
                    Title = "GAS-CF4 CF4-014",
                    Detail = "Material lot CF4-014 received; supplier notice none.",
                },
            ],
        });

        decision.Claim!.Category.Should().Be(RootCauseCategory.SupplyChainMaterial);
        decision.Claim.Cause.Should().Contain("PL-2044");
        decision.Claim.Cause.Should().Contain("SN-PL-2044");
        decision.Claim.Cause.Should().NotContain("CF4-014");
        decision.Claim.CounterEvidence.Should().Contain(item => item.Contains("ECO-4471", StringComparison.Ordinal));
    }

    [Fact]
    public void AnInSpecNoticeDoesNotCountAsAMaterialPrimary()
    {
        var decision = RootCauseRanker.Rank(new ResolutionRequest
        {
            ScopeKind = "chamber",
            ScopeKey = "E-01",
            SignalShape = "ramp",
            LotIds = ["N2H2-190"],
            NoticeIds = ["SN-N2H2-190"],
            SignalEvidenceIds = [Signal],
            Notes =
            [
                new EvidenceNote
                {
                    ExternalRef = "SN-N2H2-190",
                    Title = "Certificate density is still inside the widened spec",
                    Detail = "This is not a moisture failure.",
                },
            ],
        });

        decision.Claim!.Category.Should().Be(RootCauseCategory.ProcessDrift);
        decision.Claim.Cause.Should().Contain("ramps");
    }

    [Fact]
    public async Task ReplayServesTheSeasoningDecisionWithoutANetworkModel()
    {
        var request = SeasoningRequest();
        var ranked = RootCauseRanker.Rank(request);
        var json = ResolutionJson.SerializeDecision(ranked);
        json.Should().Contain("processDrift");
        json.Should().Contain("seasoning");
        json.Should().NotContain("ECO-4502 changed");

        var prompts = PromptCatalog.Load();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, prompts.Resolution.Body),
            new(ChatRole.User, ResolutionJson.SerializeRequest(request)),
        };
        var catalog = new InMemoryReplayCatalog();
        catalog.Store(
            PromptHasher.Hash(messages, options: null),
            new ReplayTurn("replay", json, [], 32, 32));

        var agent = new ResolutionAgent(new ReplayChatClient(catalog), prompts);
        var result = await agent.ResolveAsync(request, CancellationToken.None);

        result.Escalated.Should().BeFalse();
        result.Claim!.Category.Should().Be(RootCauseCategory.ProcessDrift);
        result.Claim.Cause.Should().Contain("clean");
        result.Claim.Cause.Should().NotContain("ECO-4502");
        result.Claim.Category.Should().NotBe(RootCauseCategory.EngineeringChange);
    }

    private static ResolutionRequest SeasoningRequest()
    {
        return new ResolutionRequest
        {
            ScopeKind = "chamber",
            ScopeKey = "E-01",
            SignalShape = "ramp",
            EcoIds = ["ECO-4502"],
            EcoEvidenceIds = [EcoEvidence],
            LotIds = ["CF4-014"],
            LotEvidenceIds = [LotEvidence],
            PmSummaries = ["WO-CLN-E01-220 clean: Last chamber clean. Chamber A is past 420 RF-hours since that clean; etch rate typically falls as the chamber seasons."],
            PmEvidenceIds = [Guid.Parse("00000000-0000-0000-0000-0000000000a7").ToString("D")],
            SignalEvidenceIds = [Signal],
            Notes =
            [
                new EvidenceNote
                {
                    Id = EcoEvidence,
                    ExternalRef = "ECO-4502",
                    Title = "Liner note on etch E-01",
                    Detail = "Document-only liner note on E-01. Setpoints were not changed. E-02 is outside this note.",
                },
                new EvidenceNote
                {
                    Id = LotEvidence,
                    ExternalRef = "CF4-014",
                    Title = "GAS-CF4 CF4-014",
                    Detail = "Material lot CF4-014 from Cinderwell Specialty Gases; supplier notice none.",
                },
            ],
        };
    }
}
