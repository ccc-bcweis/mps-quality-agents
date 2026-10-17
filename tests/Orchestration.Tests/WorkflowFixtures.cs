// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Diagnostics.CodeAnalysis;

using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

using Mps.Agents.Incidents;
using Mps.Agents.Investigation;
using Mps.Agents.Prompts;
using Mps.Agents.Ranking;
using Mps.Agents.Reasoning;
using Mps.Agents.Resolution;
using Mps.Agents.Tools;
using Mps.Application.Tools;
using Mps.Domain.Enterprise;
using Mps.Domain.Identity;
using Mps.Domain.Impact;
using Mps.Domain.Incidents;
using Mps.Domain.Telemetry;
using Mps.ModelGateway.Adapters;
using Mps.ModelGateway.Cost;
using Mps.ModelGateway.Replay;
using Mps.ModelGateway.Routing;
using Mps.Orchestration;

namespace Mps.Orchestration.Tests;

internal enum ScenarioKind
{
    EngineeringChange,
    ConfigConflict,
}

internal sealed class FixedClock : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FixedClock(DateTimeOffset now)
    {
        _now = now;
    }

    public override DateTimeOffset GetUtcNow()
    {
        return _now;
    }
}

internal sealed class MemoryIncidents : IIncidentSource
{
    private readonly Dictionary<Guid, Incident> _items = [];

    public void Add(Incident incident)
    {
        _items[incident.Id.Value] = incident;
    }

    public Task<Incident?> FindAsync(IncidentId incidentId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(_items.TryGetValue(incidentId.Value, out var incident) ? incident : null);
    }
}

internal sealed class CountingResolutionAgent : IResolutionAgent
{
    private readonly IResolutionAgent _inner;

    public CountingResolutionAgent(IResolutionAgent inner)
    {
        _inner = inner;
    }

    public int Calls { get; private set; }

    public Task<ResolutionResult> ResolveAsync(ResolutionRequest request, CancellationToken cancellationToken)
    {
        Calls++;
        return _inner.ResolveAsync(request, cancellationToken);
    }
}

internal sealed class ThrowingReasoner : IMockReasoner
{
    public int Calls { get; private set; }

    public ChatResponse Complete(IReadOnlyList<ChatMessage> messages, ChatOptions? options)
    {
        Calls++;
        throw new InvalidOperationException("A replay hit must not call the reasoner.");
    }
}

internal sealed class ScriptedChatClient : IChatClient
{
    private readonly Queue<ChatResponse> _responses;

    public ScriptedChatClient(params ChatResponse[] responses)
    {
        _responses = new Queue<ChatResponse>(responses);
    }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        _ = messages;
        _ = options;
        _ = cancellationToken;
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

internal sealed class WorkflowRig
{
    public WorkflowRig(
        ScenarioKind kind,
        IChatClient investigationChat,
        IChatClient resolutionChat,
        InvestigationSessionStore? sessions = null,
        CheckpointManager? checkpoints = null,
        MemoryIncidents? incidents = null,
        ScenarioTools? tools = null)
    {
        Tools = tools ?? new ScenarioTools(kind);
        Incidents = incidents ?? new MemoryIncidents();
        Sessions = sessions ?? new InvestigationSessionStore();
        Checkpoints = checkpoints ?? CheckpointManager.CreateInMemory();
        Resolution = new CountingResolutionAgent(new ResolutionAgent(resolutionChat, PromptCatalog.Load()));
        var bound = new ReadOnlyToolSession(Tools, Tools, Tools, Tools, Tools, Tools, Tools, Tools, Tools);
        var investigation = new InvestigationAgent(bound, investigationChat, PromptCatalog.Load(), WorkflowRig.Clock);
        Workflow = new MafInvestigationWorkflow(
            Incidents,
            investigation,
            Resolution,
            WorkflowRig.SigningKey,
            WorkflowRig.Clock,
            Sessions,
            Checkpoints);
        InvestigationChat = investigationChat;
        ResolutionChat = resolutionChat;
    }

    public static readonly DateTimeOffset At = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);

    public static readonly byte[] SigningKey = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

    public static readonly TimeProvider Clock = new FixedClock(At);

    public MemoryIncidents Incidents { get; }

    public ScenarioTools Tools { get; }

    public InvestigationSessionStore Sessions { get; }

    public CheckpointManager Checkpoints { get; }

    public CountingResolutionAgent Resolution { get; }

    public MafInvestigationWorkflow Workflow { get; }

    public IChatClient InvestigationChat { get; }

    public IChatClient ResolutionChat { get; }

    public WorkflowRig ContinueWith(IChatClient investigationChat, IChatClient resolutionChat)
    {
        return new WorkflowRig(
            ScenarioKind.EngineeringChange,
            investigationChat,
            resolutionChat,
            Sessions,
            Checkpoints,
            Incidents,
            Tools);
    }

    public static Mps.ModelGateway.Routing.ModelGateway Gateway(IReplayCatalog catalog, IMockReasoner reasoner, bool recordMisses)
    {
        return new Mps.ModelGateway.Routing.ModelGateway(
            new ModelGatewayOptions { Mode = ModelRoute.Replay, RecordReplayMisses = recordMisses },
            new InMemoryCostLedger(),
            catalog,
            reasoner,
            Clock);
    }

    public static IChatClient MockChat()
    {
        return new MockReasonerChatClient(new QualityMockReasoner());
    }
}

internal sealed class ScenarioTools :
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
    private readonly ScenarioKind _kind;

    /// <summary>Adds a maintenance work order that says the pump bearing is past its service interval (an equipment record).</summary>
    public bool BearingWorkOrder { get; init; }

    public ScenarioTools(ScenarioKind kind)
    {
        _kind = kind;
    }

    public bool ThrowOnSpc { get; set; }

    public Task<TelemetryWindow> GetWindowAsync(ToolId toolId, string? chamberId, string parameter, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        TelemetryPoint[] points = _kind == ScenarioKind.ConfigConflict
            ? [new(from, 1d), new(from.AddMinutes(1), 1d), new(from.AddMinutes(2), 1d), new(from.AddMinutes(3), 10d), new(from.AddMinutes(4), 10d), new(from.AddMinutes(5), 10d)]
            : [new(from, 1d), new(from.AddMinutes(1), 5d), new(from.AddMinutes(2), 1d), new(from.AddMinutes(3), 5d), new(from.AddMinutes(4), 1d), new(from.AddMinutes(5), 5d)];
        return Task.FromResult(new TelemetryWindow(toolId, parameter, from, until, points));
    }

    public Task<IReadOnlyList<SpcViolation>> GetViolationsAsync(ToolId toolId, string? chamberId, string parameter, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        _ = from;
        _ = until;
        _ = cancellationToken;
        if (ThrowOnSpc)
        {
            throw new InvalidOperationException("spc down");
        }

        var chamber = _kind == ScenarioKind.ConfigConflict ? "D-01" : "E-02";
        return Task.FromResult<IReadOnlyList<SpcViolation>>([
            new SpcViolation("Western Electric", toolId, chamber, parameter, WorkflowRig.At, 4d, 2d, Severity.High)]);
    }

    public Task<IReadOnlyList<ProcessRun>> GetRunsAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        _ = from;
        _ = until;
        _ = cancellationToken;
        if (_kind == ScenarioKind.ConfigConflict)
        {
            return Task.FromResult<IReadOnlyList<ProcessRun>>([
                new ProcessRun("RUN-1", toolId, "showerhead", "B", "L1", WorkflowRig.At.AddHours(-2))]);
        }

        return Task.FromResult<IReadOnlyList<ProcessRun>>([
            new ProcessRun("RUN-1", toolId, "EDGE-ETCH", "12", "L1", WorkflowRig.At.AddHours(-2))]);
    }

    public Task<IReadOnlyList<LotReference>> GetLotsAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        _ = toolId;
        _ = from;
        _ = until;
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<LotReference>>([]);
    }

    public Task<IReadOnlyList<NcrRecord>> SearchNcrsAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        _ = toolId;
        _ = from;
        _ = until;
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<NcrRecord>>([]);
    }

    public Task<IReadOnlyList<EcoRecord>> GetEcosAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        _ = from;
        _ = until;
        _ = cancellationToken;
        if (_kind == ScenarioKind.ConfigConflict)
        {
            return Task.FromResult<IReadOnlyList<EcoRecord>>([]);
        }

        return Task.FromResult<IReadOnlyList<EcoRecord>>([
            new EcoRecord("ECO-4471", "Edge ring rev C", toolId.Value, "EDGE-ETCH", WorkflowRig.At.AddDays(-2), "Edge ring revision C")]);
    }

    public Task<ConfigRevision> GetConfigAsync(ToolId toolId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (_kind == ScenarioKind.ConfigConflict)
        {
            return Task.FromResult(new ConfigRevision("CFG-1", toolId, "C", "showerhead"));
        }

        return Task.FromResult(new ConfigRevision("CFG-1", toolId, "C", "edge ring"));
    }

    public Task<IReadOnlyList<MaterialLot>> GetMaterialLotsAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        _ = toolId;
        _ = from;
        _ = until;
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<MaterialLot>>([]);
    }

    public Task<IReadOnlyList<SupplierNotice>> GetSupplierNoticesAsync(string? materialCode, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        _ = materialCode;
        _ = from;
        _ = until;
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<SupplierNotice>>([]);
    }

    public Task<IReadOnlyList<WorkOrder>> GetWorkOrdersAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        _ = from;
        _ = until;
        _ = cancellationToken;
        if (_kind == ScenarioKind.ConfigConflict)
        {
            return Task.FromResult<IReadOnlyList<WorkOrder>>([]);
        }

        WorkOrder routine = new("WO-19", toolId, "E-02", "PM", WorkflowRig.At.AddDays(-1), "Preventive maintenance the same week");
        return Task.FromResult<IReadOnlyList<WorkOrder>>(BearingWorkOrder
            ? [routine, new WorkOrder("WO-PMP-E02-BRG", toolId, "E-02", "maintenance", WorkflowRig.At.AddDays(-3), "Foreline pump bearing hours are past the service interval. Vibration has been rising.")]
            : [routine]);
    }

    public Task<IReadOnlyList<CalibrationRecord>> GetCalibrationsAsync(ToolId toolId, CancellationToken cancellationToken)
    {
        _ = toolId;
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<CalibrationRecord>>([]);
    }

    public Task<IReadOnlyList<DocumentHit>> SearchAsync(string query, string? toolFamily, CancellationToken cancellationToken)
    {
        _ = query;
        _ = toolFamily;
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<DocumentHit>>([]);
    }

    public Task<ImpactScope> GetScopeAsync(ToolId toolId, string parameter, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        _ = parameter;
        _ = from;
        _ = until;
        _ = cancellationToken;
        if (_kind == ScenarioKind.ConfigConflict)
        {
            return Task.FromResult(new ImpactScope(ImpactScopeKind.Chamber, "D-01", null, "chamber reported scope"));
        }

        return Task.FromResult(new ImpactScope(ImpactScopeKind.Tool, toolId.Value, null, "tool reported scope"));
    }
}

internal sealed class RecordingReplayCatalog : IReplayCatalog
{
    private readonly InMemoryReplayCatalog _inner = new();

    public List<ReplayTurn> Stored { get; } = [];

    public bool TryGet(string promptHash, [NotNullWhen(true)] out ReplayTurn? turn)
    {
        return _inner.TryGet(promptHash, out turn);
    }

    public void Store(string promptHash, ReplayTurn turn)
    {
        Stored.Add(turn);
        _inner.Store(promptHash, turn);
    }
}
