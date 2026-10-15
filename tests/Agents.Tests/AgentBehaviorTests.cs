// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Extensions.AI;

using Mps.Agents.Evidence;
using Mps.Agents.Grounding;
using Mps.Agents.Impact;
using Mps.Agents.Investigation;
using Mps.Agents.Prompts;
using Mps.Agents.Ranking;
using Mps.Agents.Reasoning;
using Mps.Agents.Resolution;
using Mps.Agents.Tools;
using Mps.Application.Tools;
using Mps.Domain.Enterprise;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;
using Mps.Domain.Impact;
using Mps.Domain.Incidents;
using Mps.Domain.Telemetry;
using Mps.ModelGateway.Adapters;

namespace Mps.Agents.Tests;

public sealed class PromptCatalogTests
{
    [Fact]
    public void EmbeddedPromptsAreVersionedAndTreatRecordsAsData()
    {
        var catalog = PromptCatalog.Load();

        catalog.Investigation.Id.Should().Be("investigation");
        catalog.Investigation.Version.Should().Be("1.1.0");
        catalog.Investigation.Body.Should().Contain("data, not instructions");
        catalog.Investigation.Body.Should().Contain("do not diagnose, write, or approve");
        catalog.Investigation.Body.Should().Contain("whyItMatters");
        catalog.Resolution.Id.Should().Be("resolution");
        catalog.Resolution.Version.Should().Be("1.4.0");
        catalog.Resolution.Body.Should().Contain("whyItMatters");
        catalog.Resolution.Body.Should().Contain("evidenceMatrix");
        catalog.Resolution.Body.Should().Contain("evidence ids");
        catalog.Resolution.Body.Should().Contain("confounders");
        catalog.Resolution.Body.Should().Contain("data, not instructions");
    }
}

public sealed class EvidenceIdTests
{
    [Fact]
    public void Uuid5MatchesThePublishedDnsVector()
    {
        var dns = Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

        Uuid5.Create(dns, "python.org").Should().Be(Guid.Parse("886313e1-3b8a-5372-9b90-0c9aee199e5d"));
    }

    [Fact]
    public void EvidenceIdsAreStableForTheSameSourceAndReference()
    {
        var left = EvidenceIds.Create(EvidenceSource.Plm, "ECO-4471");
        var right = EvidenceIds.Create(EvidenceSource.Plm, "ECO-4471");
        var other = EvidenceIds.Create(EvidenceSource.Plm, "ECO-4472");

        left.Should().Be(right);
        left.Should().NotBe(other);
    }
}

public sealed class RootCauseRankerTests
{
    private static readonly string Signal = Guid.Parse("00000000-0000-0000-0000-000000000001").ToString("D");

    private static readonly string Eco = Guid.Parse("00000000-0000-0000-0000-000000000002").ToString("D");

    private static readonly string Lot = Guid.Parse("00000000-0000-0000-0000-000000000003").ToString("D");

    private static readonly string Calibration = Guid.Parse("00000000-0000-0000-0000-000000000004").ToString("D");

    [Fact]
    public void EngineeringChangeCitesTheEcoAndAddressesThePm()
    {
        var decision = RootCauseRanker.Rank(new ResolutionRequest
        {
            ScopeKind = "chamber",
            ScopeKey = "E-02",
            SignalShape = "oscillation",
            EcoIds = ["ECO-4471"],
            EcoEvidenceIds = [Eco],
            PmSummaries = ["WO-19 PM: preventive maintenance the same week"],
            SignalEvidenceIds = [Signal],
        });

        decision.Escalate.Should().BeFalse();
        decision.Claim!.Category.Should().Be(RootCauseCategory.EngineeringChange);
        decision.Claim.Cause.Should().Contain("ECO-4471");
        decision.Claim.CounterEvidence.Should().Contain(item => item.Contains("WO-19", StringComparison.Ordinal));
        decision.Claim.EvidenceIds.Should().Contain(new EvidenceId(Guid.Parse(Eco)));
    }

    [Fact]
    public void ASharedLotRanksAsSupplyChainAndTreatsTheEcoAsAConfounder()
    {
        var decision = RootCauseRanker.Rank(new ResolutionRequest
        {
            ScopeKind = "materialLot",
            ScopeKey = "LOT-NF3",
            SignalShape = "step",
            EcoIds = ["ECO-9"],
            LotIds = ["LOT-NF3"],
            NoticeIds = ["SN-4"],
            LotEvidenceIds = [Lot],
        });

        decision.Claim!.Category.Should().Be(RootCauseCategory.SupplyChainMaterial);
        decision.Claim.CounterEvidence.Should().Contain(item => item.Contains("ECO-9", StringComparison.Ordinal));
        decision.Claim.Cause.Should().Contain("SN-4");
    }

    [Fact]
    public void ASingleChamberWithOverdueCalibrationRanksAsEquipment()
    {
        var decision = RootCauseRanker.Rank(new ResolutionRequest
        {
            ScopeKind = "chamber",
            ScopeKey = "D-01",
            SignalShape = "step",
            OverdueInstruments = ["manometer"],
            OverdueEvidenceIds = [Calibration],
            LotIds = ["LOT-9"],
        });

        decision.Claim!.Category.Should().Be(RootCauseCategory.EquipmentCalibrationOrAlignment);
        decision.Claim.Cause.Should().Contain("manometer");
        decision.Claim.CounterEvidence.Should().Contain(item => item.Contains("LOT-9", StringComparison.Ordinal));
    }

    [Fact]
    public void ARampWithNoDiscreteEventRecommendsACleanAndNotACapa()
    {
        var decision = RootCauseRanker.Rank(new ResolutionRequest
        {
            ScopeKind = "chamber",
            ScopeKey = "S-03",
            SignalShape = "ramp",
            SignalEvidenceIds = [Signal],
        });

        decision.Claim!.Category.Should().Be(RootCauseCategory.ProcessDrift);
        decision.Claim.Actions.Should().Contain(action => action.Contains("chamber clean", StringComparison.Ordinal) && action.Contains("CAPA", StringComparison.Ordinal));
    }

    [Fact]
    public void OverlappingPrimariesOnAnAmbiguousScopeEscalate()
    {
        var decision = RootCauseRanker.Rank(new ResolutionRequest
        {
            ScopeKind = "tool",
            ScopeKey = "MPS-E7",
            EcoIds = ["ECO-4471"],
            EcoEvidenceIds = [Eco],
            OverdueInstruments = ["manometer"],
            OverdueEvidenceIds = [Calibration],
            LotIds = ["LOT-NF3"],
            LotEvidenceIds = [Lot],
        });

        decision.Escalate.Should().BeTrue();
        decision.Claim.Should().BeNull();
        decision.Reason.Should().Contain("overlapping");
    }

    [Fact]
    public void MissingPrimariesEscalate()
    {
        var decision = RootCauseRanker.Rank(new ResolutionRequest
        {
            ScopeKind = "tool",
            ScopeKey = "MPS-E7",
            SignalShape = "oscillation",
            SignalEvidenceIds = [Signal],
        });

        decision.Escalate.Should().BeTrue();
        decision.Reason.Should().Contain("no primary");
    }
}

public sealed class InvestigationAgentTests
{
    [Fact]
    public async Task DefaultPlanExecutesTenReadOnlyToolsAndBuildsStableEvidence()
    {
        var tools = new ScriptedTools();
        var agent = Agent(tools, new MockReasonerChatClient(new QualityMockReasoner()));

        var first = await agent.GatherAsync(SampleIncident(), CancellationToken.None);
        var second = await agent.GatherAsync(SampleIncident(), CancellationToken.None);

        first.Failed.Should().BeFalse();
        first.ToolCalls.Select(call => call.Name).Should().Equal(AgentToolNames.DefaultPlan);
        first.ToolCalls.Should().OnlyContain(call => call.Executed);
        first.PromptId.Should().Be("investigation");
        first.PromptVersion.Should().Be("1.1.0");
        first.Pack!.Ecos.Should().Contain(eco => eco.EcoId == "ECO-4471");
        first.Features!.Kind.Should().Be(ImpactScopeKind.Chamber);
        first.Features.Key.Should().Be("E-02");
        first.ReportedScope!.Kind.Should().Be(ImpactScopeKind.Tool);
        var ecoId = first.Pack.Items.Single(item => item.ExternalRef == "ECO-4471").Id;
        second.Pack!.Items.Single(item => item.ExternalRef == "ECO-4471").Id.Should().Be(ecoId);

        var decision = RootCauseRanker.Rank(ResolutionRequest.From(first.Pack, first.Features));
        decision.Claim!.Category.Should().Be(RootCauseCategory.EngineeringChange);
        decision.Claim.CounterEvidence.Should().Contain(item => item.Contains("WO-19", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateCapaIsFlaggedAndNotExecuted()
    {
        var tools = new ScriptedTools();
        var chat = new ScriptedChatClient(
            Function("CreateCapa"),
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")));
        var agent = Agent(tools, chat);

        var result = await agent.GatherAsync(SampleIncident(), CancellationToken.None);

        result.Failed.Should().BeFalse();
        result.ToolCalls.Should().ContainSingle();
        result.ToolCalls[0].Executed.Should().BeFalse();
        result.ToolCalls[0].Flag.Should().Be("disallowed");
        tools.Touched.Should().BeFalse();
    }

    [Fact]
    public async Task AToolFailureStopsWithoutAPack()
    {
        var tools = new ScriptedTools { ThrowOnSpc = true };
        var agent = Agent(tools, new MockReasonerChatClient(new QualityMockReasoner()));

        var result = await agent.GatherAsync(SampleIncident(), CancellationToken.None);

        result.Failed.Should().BeTrue();
        result.Pack.Should().BeNull();
        result.FailureReason.Should().Contain("Spc.GetViolations");
    }

    [Fact]
    public async Task TheToolBudgetStopsAtTheConfiguredCount()
    {
        var tools = new ScriptedTools();
        var agent = Agent(tools, new MockReasonerChatClient(new QualityMockReasoner()), limits: new InvestigationLimits { MaxToolCalls = 2 });

        var result = await agent.GatherAsync(SampleIncident(), CancellationToken.None);

        result.ToolCalls.Select(call => call.Name).Should().Equal(AgentToolNames.SpcGetViolations, AgentToolNames.TelemetryGetWindow);
    }

    [Fact]
    public async Task TheClockStopsTheRunAtSixtySeconds()
    {
        var start = new DateTimeOffset(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);
        var clock = new SequenceTimeProvider(start, start.AddSeconds(61));
        var tools = new ScriptedTools();
        var agent = Agent(tools, new MockReasonerChatClient(new QualityMockReasoner()), clock);

        var result = await agent.GatherAsync(SampleIncident(), CancellationToken.None);

        result.Failed.Should().BeTrue();
        result.FailureReason.Should().Contain("60");
        tools.Touched.Should().BeFalse();
    }

    [Fact]
    public async Task ADisallowedNameIsNotExecutedByTheSession()
    {
        var session = new ReadOnlyToolSession(
            new ScriptedTools(),
            new ScriptedTools(),
            new ScriptedTools(),
            new ScriptedTools(),
            new ScriptedTools(),
            new ScriptedTools(),
            new ScriptedTools(),
            new ScriptedTools(),
            new ScriptedTools());

        var act = async () => await session.ExecuteAsync("CreateCapa", SampleIncident(), EvidenceSamples.At, EvidenceSamples.At, new ToolHarvest(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void ABroaderLotScopeIsUsedWhenTheRowsNameThatLot()
    {
        var calculated = new ImpactFeatures(ImpactScopeKind.Chamber, "E-02", SignalShape.Step, null, null);
        var reported = new ImpactScope(ImpactScopeKind.MaterialLot, "LOT-1", null, "shared lot");
        var observations = new[] { new ImpactObservation("MPS-E7", "E-02", null, "LOT-1") };

        var selected = ScopeSelector.Select(calculated, reported, observations);

        selected.Kind.Should().Be(ImpactScopeKind.MaterialLot);
        selected.Key.Should().Be("LOT-1");
    }

    [Fact]
    public void CalculatedScopeWinsWhenTheToolDisagrees()
    {
        var calculated = new ImpactFeatures(ImpactScopeKind.Chamber, "E-02", SignalShape.Step, null, null);
        var reported = new ImpactScope(ImpactScopeKind.Tool, "MPS-E7", null, null);

        var selected = ScopeSelector.Select(calculated, reported, [new ImpactObservation("MPS-E7", "E-02", null, null)]);

        selected.Kind.Should().Be(ImpactScopeKind.Chamber);
        selected.Key.Should().Be("E-02");
    }

    private static InvestigationAgent Agent(ScriptedTools tools, IChatClient chat, TimeProvider? time = null, InvestigationLimits? limits = null)
    {
        var session = new ReadOnlyToolSession(tools, tools, tools, tools, tools, tools, tools, tools, tools);
        return new InvestigationAgent(session, chat, PromptCatalog.Load(), time, limits);
    }

    private static Incident SampleIncident()
    {
        return Incident.Open(
            IncidentId.New(),
            new ToolId("MPS-E7"),
            "E-02",
            "esc_temp",
            IncidentChannel.IotSpcAlert,
            Severity.High,
            "ESC temperature oscillation",
            "corr-s1",
            EvidenceSamples.At);
    }

    private static ChatResponse Function(string name)
    {
        var call = new FunctionCallContent("c1", name, new Dictionary<string, object?>());
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, [call]));
    }
}

public sealed class ResolutionAgentTests
{
    [Fact]
    public async Task ASchemaValidReplyBecomesAGroundedClaim()
    {
        var id = EvidenceSamples.Id(1);
        var json = ResolutionJson.SerializeDecision(RootCauseRanker.Rank(EngineeringRequest(id)));
        var agent = new ResolutionAgent(new ScriptedChatClient(Text(json)), PromptCatalog.Load());

        var result = await agent.ResolveAsync(EngineeringRequest(id), CancellationToken.None);

        result.Escalated.Should().BeFalse();
        result.ModelCalls.Should().Be(1);
        result.PromptVersion.Should().Be("1.4.0");
        result.Claim!.Category.Should().Be(RootCauseCategory.EngineeringChange);
        result.Claim.EvidenceIds.Should().Contain(id);
        result.Claim.CounterEvidence.Should().NotBeEmpty();
        var pack = new[] { EvidenceSamples.Item(1, EvidenceSource.Plm, "ECO-4471", "ECO-4471") };
        GroundingValidator.Validate(result.Claim, pack).Accepted.Should().BeTrue();
    }

    [Fact]
    public async Task AFunctionCallIsRejectedAndALaterJsonReplyIsAccepted()
    {
        var id = EvidenceSamples.Id(1);
        var json = ResolutionJson.SerializeDecision(RootCauseRanker.Rank(EngineeringRequest(id)));
        var call = new FunctionCallContent("c1", "CreateCapa", new Dictionary<string, object?>());
        var chat = new ScriptedChatClient(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, [call])),
            Text(json));
        var agent = new ResolutionAgent(chat, PromptCatalog.Load());

        var result = await agent.ResolveAsync(EngineeringRequest(id), CancellationToken.None);

        result.Escalated.Should().BeFalse();
        result.ModelCalls.Should().Be(2);
        chat.Calls.Should().Be(2);
    }

    [Fact]
    public async Task InvalidJsonEscalatesAfterOneRetry()
    {
        var chat = new ScriptedChatClient(Text("not json"), Text("still not json"));
        var agent = new ResolutionAgent(chat, PromptCatalog.Load());

        var result = await agent.ResolveAsync(EngineeringRequest(EvidenceSamples.Id(1)), CancellationToken.None);

        result.Escalated.Should().BeTrue();
        result.ModelCalls.Should().Be(2);
        result.Claim.Should().BeNull();
        result.Reason.Should().Contain("JSON");
    }

    [Fact]
    public async Task TwoFunctionCallsEscalate()
    {
        var call = new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("c1", "CreateCapa", new Dictionary<string, object?>())]));
        var agent = new ResolutionAgent(new ScriptedChatClient(call, call), PromptCatalog.Load());

        var result = await agent.ResolveAsync(EngineeringRequest(EvidenceSamples.Id(1)), CancellationToken.None);

        result.Escalated.Should().BeTrue();
        result.ModelCalls.Should().Be(2);
        result.Reason.Should().Contain("function call");
    }

    [Fact]
    public async Task TheMockReasonerReturnsTheRankedJson()
    {
        var request = EngineeringRequest(EvidenceSamples.Id(1));
        var agent = new ResolutionAgent(new MockReasonerChatClient(new QualityMockReasoner()), PromptCatalog.Load());

        var result = await agent.ResolveAsync(request, CancellationToken.None);

        result.Escalated.Should().BeFalse();
        result.Claim!.Cause.Should().Contain("ECO-4471");
        result.Claim.Explanations.Should().NotBeEmpty();
        result.Claim.EvidenceMatrix.Select(entry => entry.Category).Distinct().Should().HaveCount(4);
        result.ModelCalls.Should().Be(1);
    }

    private static ResolutionRequest EngineeringRequest(EvidenceId id)
    {
        return new ResolutionRequest
        {
            ScopeKind = "chamber",
            ScopeKey = "E-02",
            SignalShape = "oscillation",
            EcoIds = ["ECO-4471"],
            EcoEvidenceIds = [id.ToString()],
            PmSummaries = ["WO-19 PM: same week"],
        };
    }

    private static ChatResponse Text(string text)
    {
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
    }
}

internal sealed class ScriptedChatClient : IChatClient
{
    private readonly Queue<ChatResponse> _responses;

    public ScriptedChatClient(params ChatResponse[] responses)
    {
        _responses = new Queue<ChatResponse>(responses);
    }

    public int Calls { get; private set; }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(_responses.Dequeue());
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
    }
}

internal sealed class SequenceTimeProvider : TimeProvider
{
    private readonly DateTimeOffset[] _times;
    private int _index;

    public SequenceTimeProvider(params DateTimeOffset[] times)
    {
        _times = times;
    }

    public override DateTimeOffset GetUtcNow()
    {
        var value = _times[Math.Min(_index, _times.Length - 1)];
        _index++;
        return value;
    }
}

internal sealed class ScriptedTools :
    ITelemetryTool,
    ISpcTool,
    IMesTool,
    IQmsQueryTool,
    IPlmTool,
    IErpTool,
    ICmmsTool,
    IDocumentSearchTool,
    IImpactScopeTool
{
    public bool ThrowOnSpc { get; set; }

    public bool Touched { get; private set; }

    public Task<TelemetryWindow> GetWindowAsync(ToolId toolId, string parameter, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        Touch();
        TelemetryPoint[] points = [new(from, 1d), new(from.AddMinutes(1), 5d), new(from.AddMinutes(2), 1d), new(from.AddMinutes(3), 5d), new(from.AddMinutes(4), 1d), new(from.AddMinutes(5), 5d)];
        return Task.FromResult(new TelemetryWindow(toolId, parameter, from, until, points));
    }

    public Task<IReadOnlyList<SpcViolation>> GetViolationsAsync(ToolId toolId, string parameter, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        Touch();
        if (ThrowOnSpc)
        {
            throw new InvalidOperationException("spc down");
        }

        return Task.FromResult<IReadOnlyList<SpcViolation>>([new SpcViolation("Western Electric", toolId, "E-02", parameter, EvidenceSamples.At, 4d, 2d, Severity.High)]);
    }

    public Task<IReadOnlyList<ProcessRun>> GetRunsAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        Touch();
        return Task.FromResult<IReadOnlyList<ProcessRun>>([new ProcessRun("RUN-1", toolId, "EDGE-ETCH", "12", "L1", EvidenceSamples.At.AddHours(-2))]);
    }

    public Task<IReadOnlyList<LotReference>> GetLotsAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        Touch();
        return Task.FromResult<IReadOnlyList<LotReference>>([]);
    }

    public Task<IReadOnlyList<NcrRecord>> SearchNcrsAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        Touch();
        return Task.FromResult<IReadOnlyList<NcrRecord>>([]);
    }

    public Task<IReadOnlyList<EcoRecord>> GetEcosAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        Touch();
        return Task.FromResult<IReadOnlyList<EcoRecord>>([new EcoRecord("ECO-4471", "Edge ring rev C", toolId.Value, "EDGE-ETCH", EvidenceSamples.At.AddDays(-2), "Edge ring revision C")]);
    }

    public Task<ConfigRevision> GetConfigAsync(ToolId toolId, CancellationToken cancellationToken)
    {
        Touch();
        return Task.FromResult(new ConfigRevision("CFG-1", toolId, "C", "edge ring"));
    }

    public Task<IReadOnlyList<MaterialLot>> GetMaterialLotsAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        Touch();
        return Task.FromResult<IReadOnlyList<MaterialLot>>([]);
    }

    public Task<IReadOnlyList<SupplierNotice>> GetSupplierNoticesAsync(string? materialCode, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        Touch();
        return Task.FromResult<IReadOnlyList<SupplierNotice>>([]);
    }

    public Task<IReadOnlyList<WorkOrder>> GetWorkOrdersAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        Touch();
        return Task.FromResult<IReadOnlyList<WorkOrder>>([new WorkOrder("WO-19", toolId, "E-02", "PM", EvidenceSamples.At.AddDays(-1), "Preventive maintenance the same week")]);
    }

    public Task<IReadOnlyList<CalibrationRecord>> GetCalibrationsAsync(ToolId toolId, CancellationToken cancellationToken)
    {
        Touch();
        return Task.FromResult<IReadOnlyList<CalibrationRecord>>([]);
    }

    public Task<IReadOnlyList<DocumentHit>> SearchAsync(string query, string? toolFamily, CancellationToken cancellationToken)
    {
        Touch();
        return Task.FromResult<IReadOnlyList<DocumentHit>>([]);
    }

    public Task<ImpactScope> GetScopeAsync(ToolId toolId, string parameter, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        Touch();
        return Task.FromResult(new ImpactScope(ImpactScopeKind.Tool, toolId.Value, null, "tool reported scope"));
    }

    private void Touch()
    {
        Touched = true;
    }
}
