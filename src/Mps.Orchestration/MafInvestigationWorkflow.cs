// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.InProc;

using Mps.Agents.Evidence;
using Mps.Agents.Explain;
using Mps.Agents.Grounding;
using Mps.Agents.Incidents;
using Mps.Agents.Investigation;
using Mps.Agents.Ranking;
using Mps.Agents.Resolution;
using Mps.Application.Workflow;
using Mps.Domain.Capa;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;
using Mps.Domain.Investigations;

namespace Mps.Orchestration;

/// <summary>
/// Microsoft Agent Framework investigation flow. The model never writes. A person approves before any later CAPA.
/// </summary>
public sealed class MafInvestigationWorkflow : IInvestigationWorkflow
{
    public const string ApprovalPortId = "human-approval";

    /// <summary>Retries the resolution allows when its shape or category disagrees with the evidence.</summary>
    public const int ShapeRetries = 1;

    private readonly IIncidentSource _incidents;
    private readonly IInvestigationAgent _investigation;
    private readonly IResolutionAgent _resolution;
    private readonly byte[] _signingKey;
    private readonly TimeProvider _time;
    private readonly InvestigationSessionStore _sessions;
    private readonly InProcessExecutionEnvironment _environment;

    public MafInvestigationWorkflow(
        IIncidentSource incidents,
        IInvestigationAgent investigation,
        IResolutionAgent resolution,
        ReadOnlyMemory<byte> signingKey,
        TimeProvider? timeProvider = null,
        InvestigationSessionStore? sessions = null,
        CheckpointManager? checkpoints = null)
    {
        ArgumentNullException.ThrowIfNull(incidents);
        ArgumentNullException.ThrowIfNull(investigation);
        ArgumentNullException.ThrowIfNull(resolution);
        if (signingKey.Length == 0)
        {
            throw new ArgumentException("A signing key is required.", nameof(signingKey));
        }

        _incidents = incidents;
        _investigation = investigation;
        _resolution = resolution;
        _signingKey = signingKey.ToArray();
        _time = timeProvider ?? TimeProvider.System;
        _sessions = sessions ?? new InvestigationSessionStore();
        _environment = InProcessExecution.Default.WithCheckpointing(checkpoints ?? CheckpointManager.CreateInMemory());
    }

    public async Task<InvestigationSnapshot> StartAsync(IncidentId incidentId, CancellationToken cancellationToken)
    {
        if (incidentId.IsEmpty)
        {
            throw new ArgumentException("Incident id is required.", nameof(incidentId));
        }

        var investigationId = InvestigationId.New();
        var session = _sessions.Create(investigationId, incidentId);
        var workflow = BuildGraph();
        var input = incidentId.ToString() + "|" + investigationId.ToString();
        await using var run = await _environment
            .RunAsync(workflow, input, investigationId.ToString(), cancellationToken)
            .ConfigureAwait(false);
        var observed = ReadEvents(run.OutgoingEvents);
        if (observed.Request is not null)
        {
            session.Checkpoint = observed.Checkpoint ?? throw new InvalidOperationException(
                "The approval halt did not produce a checkpoint. Events: " + observed.EventNames);
        }

        return session.ToSnapshot();
    }

    public async Task<InvestigationSnapshot> ResumeWithHumanInputAsync(
        InvestigationId investigationId,
        HumanDecision decision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var session = _sessions.Require(investigationId);
        if (session.Stage != InvestigationStage.AwaitingApproval)
        {
            throw new InvalidOperationException("Human input is accepted only while the investigation is awaiting approval.");
        }

        if (session.Checkpoint is null)
        {
            throw new InvalidOperationException("The investigation has no checkpoint to resume.");
        }

        session.PendingDecision = decision;
        var workflow = BuildGraph();
        await using var run = await _environment
            .ResumeAsync(workflow, session.Checkpoint, cancellationToken)
            .ConfigureAwait(false);
        var pending = ReadEvents(run.OutgoingEvents);
        if (pending.Request is null)
        {
            throw new InvalidOperationException(
                "Resume did not re-emit the approval request. Events: " + pending.EventNames);
        }

        await run.ResumeAsync([pending.Request.CreateResponse(investigationId.ToString())], cancellationToken)
            .ConfigureAwait(false);
        _ = ReadEvents(run.OutgoingEvents);
        return session.ToSnapshot();
    }

    public Task<InvestigationSnapshot> GetStateAsync(InvestigationId investigationId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(_sessions.Require(investigationId).ToSnapshot());
    }

    /// <summary>
    /// Stable text bound into the approval token. Actions are joined in order. A claim with no actions uses the cause.
    /// </summary>
    public static string ProposedSummary(ResolutionClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        if (claim.Actions.Count > 0)
        {
            return string.Join("; ", claim.Actions);
        }

        if (string.IsNullOrWhiteSpace(claim.Cause))
        {
            throw new ArgumentException("A proposed action needs an action or a cause.", nameof(claim));
        }

        return claim.Cause;
    }

    /// <summary>
    /// The summary stored when the recommendation paused. Tests hash this same string.
    /// </summary>
    public string ProposedActionSummary(InvestigationId investigationId)
    {
        var summary = _sessions.Require(investigationId).ProposedSummary;
        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new InvalidOperationException("No proposed action is waiting for approval.");
        }

        return summary;
    }

    internal async Task<bool> RunToGateAsync(string message, CancellationToken cancellationToken)
    {
        var (incidentId, investigationId) = ParseInput(message);
        var session = _sessions.Require(investigationId);
        var incident = await _incidents.FindAsync(incidentId, cancellationToken).ConfigureAwait(false);
        if (incident is null)
        {
            session.Stage = InvestigationStage.Failed;
            session.EscalationReason = "Incident was not found.";
            session.CorrelationId = "missing";
            return false;
        }

        session.CorrelationId = incident.CorrelationId;
        var gathered = await _investigation.GatherAsync(incident, cancellationToken).ConfigureAwait(false);
        session.InvestigationPromptVersion = gathered.PromptVersion;
        if (gathered.Failed || gathered.Pack is null || gathered.Features is null)
        {
            session.Stage = InvestigationStage.Failed;
            session.EscalationReason = string.IsNullOrWhiteSpace(gathered.FailureReason)
                ? "Evidence gathering failed."
                : gathered.FailureReason;
            return false;
        }

        session.ReplaceEvidence(gathered.Pack.Items);
        session.ReplaceExplanations(gathered.Pack.Explanations);
        session.ReplaceMatrix(EvidenceMatrixBuilder.FromPack(gathered.Pack, null, WhyItMattersCatalog.Shared));
        var gate = EvidenceGate.Evaluate(gathered.Pack);
        if (gate.Outcome != GateOutcome.Sufficient)
        {
            session.Stage = InvestigationStage.Escalated;
            session.EscalationReason = gate.EscalationReason;
            session.ClearHypotheses();
            return false;
        }

        // A reported shape (the structured "shape" field) that is not the measured shape, or a category the cited records
        // contradict (data/metadata/category-evidence.yaml), gets one retry with feedback. Grounding and both guards run on
        // every answer; a second miss escalates.
        string? feedback = null;
        ResolutionClaim? accepted;
        for (var attempt = 0; ; attempt++)
        {
            session.ResolutionCalls++;
            var request = ResolutionRequest.From(gathered.Pack, gathered.Features);
            request.Feedback = feedback;
            var resolution = await _resolution.ResolveAsync(request, cancellationToken).ConfigureAwait(false);
            session.ResolutionPromptVersion = resolution.PromptVersion;
            if (resolution.Escalated || resolution.Claim is null)
            {
                session.Stage = InvestigationStage.Escalated;
                session.EscalationReason = string.IsNullOrWhiteSpace(resolution.Reason)
                    ? "Resolution escalated."
                    : resolution.Reason;
                session.ClearHypotheses();
                return false;
            }

            var grounding = GroundingValidator.Validate(resolution.Claim, gathered.Pack.Items);
            accepted = grounding.Claim;
            if (!grounding.Accepted || accepted is null || accepted.Category is null || accepted.Cause is null || accepted.Confidence is null)
            {
                var detail = grounding.Failures.Count == 0
                    ? "category-without-evidence"
                    : string.Join(", ", grounding.Failures);
                session.Stage = InvestigationStage.Escalated;
                session.EscalationReason = "grounding: " + detail;
                session.ClearHypotheses();
                return false;
            }

            // Ruled-out record or shape (CauseGuard), then the category against the cited records (CategoryGuard).
            var guarded = CauseGuard.Check(accepted, gathered.Pack);
            var shapeMiss = CauseGuard.ShapeMismatch(accepted.Shape, gathered.Pack);
            var categoryMiss = CategoryGuard.Check(accepted, request.Notes);
            if (guarded is null && categoryMiss is null)
            {
                break;
            }

            var retryable = guarded is null || string.Equals(guarded, shapeMiss, StringComparison.Ordinal);
            if (attempt < ShapeRetries && retryable)
            {
                feedback = string.Join(
                    " ",
                    new[]
                    {
                        shapeMiss is null ? null : CauseGuard.ShapeFeedback(accepted.Shape, gathered.Pack),
                        categoryMiss is null ? null : CategoryGuard.Feedback(accepted, request.Notes),
                    }.Where(note => note is not null));
                continue;
            }

            session.Stage = InvestigationStage.Escalated;
            session.EscalationReason = guarded ?? categoryMiss;
            session.ClearHypotheses();
            return false;
        }

        session.ReplaceExplanations(MergeExplanations(gathered.Pack.Explanations, accepted.Explanations));
        session.ReplaceMatrix(accepted.EvidenceMatrix);
        var hypothesis = new Hypothesis(
            new HypothesisId(Uuid5.Create(EvidenceIds.Namespace, incident.Id + "|" + accepted.Category.Value)),
            accepted.Category.Value,
            accepted.Cause,
            accepted.Confidence.Value,
            accepted.EvidenceIds,
            WithRuledOut(Readable(accepted.CounterEvidence, gathered.Pack.Items), RuledOut.All(gathered.Pack)),
            Mps.Agents.Resolution.RecommendedActions.Complete(
                accepted.Category.Value,
                accepted.Actions,
                gathered.Pack.Items.Where(item => accepted.EvidenceIds.Contains(item.Id)).ToArray(),
                incident.ToolId.Value));
        session.ClearHypotheses();
        session.AddHypothesis(hypothesis);
        session.ProposedSummary = hypothesis.Actions.Count > 0 ? string.Join("; ", hypothesis.Actions) : ProposedSummary(accepted);
        session.Stage = InvestigationStage.AwaitingApproval;
        return true;
    }

    internal void ApplyHumanDecision(string investigationIdText)
    {
        if (!Guid.TryParse(investigationIdText, out var parsed))
        {
            throw new InvalidOperationException("The approval step expected an investigation id.");
        }

        var session = _sessions.Require(new InvestigationId(parsed));
        var decision = session.PendingDecision ?? throw new InvalidOperationException("No human decision is waiting.");
        session.PendingDecision = null;
        switch (decision.Kind)
        {
            case HumanDecisionKind.Approve:
                var summary = session.ProposedSummary ?? throw new InvalidOperationException("No proposed action is waiting for approval.");
                var hash = CapaAction.ComputeActionHash(session.IncidentId, summary);
                decision.ApprovalToken!.Consume(hash, _time.GetUtcNow(), _signingKey);
                session.Stage = InvestigationStage.Approved;
                break;
            case HumanDecisionKind.Reject:
                session.Stage = InvestigationStage.Rejected;
                break;
            case HumanDecisionKind.RequestInfo:
                session.ClearHypotheses();
                session.Stage = InvestigationStage.Escalated;
                session.EscalationReason = decision.Comment is null
                    ? "request_info: reviewer requested more information"
                    : "request_info: " + decision.Comment;
                break;
            default:
                throw new InvalidOperationException("The human decision is not recognized.");
        }
    }

    internal static string InvestigationIdText(string message)
    {
        return ParseInput(message).InvestigationId.ToString();
    }

    private Workflow BuildGraph()
    {
        var gate = new InvestigationGateExecutor(this);
        var port = RequestPort.Create<string, string>(ApprovalPortId);
        var approval = new ApprovalExecutor(this);
        return new WorkflowBuilder(gate)
            .AddEdge(gate, port)
            .AddEdge(port, approval)
            .WithOutputFrom(gate, approval)
            .Build(validateOrphans: true);
    }

    private static CitedEvidence[] MergeExplanations(
        IReadOnlyList<CitedEvidence> pack,
        IReadOnlyList<CitedEvidence> claim)
    {
        var merged = new Dictionary<EvidenceId, CitedEvidence>();
        foreach (var item in pack)
        {
            merged[item.EvidenceId] = item;
        }

        foreach (var item in claim)
        {
            merged[item.EvidenceId] = item;
        }

        return merged.Values.ToArray();
    }

    private static (IncidentId IncidentId, InvestigationId InvestigationId) ParseInput(string message)
    {
        var parts = message.Split('|');
        if (parts.Length != 2
            || !Guid.TryParse(parts[0], out var incidentValue)
            || !Guid.TryParse(parts[1], out var investigationValue))
        {
            throw new InvalidOperationException("Workflow input must be an incident id and an investigation id.");
        }

        return (new IncidentId(incidentValue), new InvestigationId(investigationValue));
    }

    private static ObservedRun ReadEvents(IEnumerable<WorkflowEvent>? events)
    {
        ArgumentNullException.ThrowIfNull(events);
        ExternalRequest? request = null;
        CheckpointInfo? checkpoint = null;
        var names = new List<string>();
        foreach (var evt in events)
        {
            names.Add(evt.GetType().Name);
            switch (evt)
            {
                case SuperStepCompletedEvent completed when completed.CompletionInfo?.Checkpoint is { } info:
                    if (completed.CompletionInfo.HasPendingRequests || checkpoint is null)
                    {
                        checkpoint = info;
                    }

                    break;
                case RequestInfoEvent info:
                    request = info.Request;
                    break;
                case WorkflowErrorEvent workflowError:
                    throw workflowError.Exception ?? new InvalidOperationException("The workflow failed.");
                case ExecutorFailedEvent failed:
                    throw failed.Data as Exception ?? new InvalidOperationException("An executor failed.");
            }
        }

        return new ObservedRun(request, checkpoint, string.Join(", ", names));
    }

    private readonly record struct ObservedRun(ExternalRequest? Request, CheckpointInfo? Checkpoint, string EventNames);

    /// <summary>Adds every record the pack itself rules out, unless the claim already names it.</summary>
    public static IReadOnlyList<string> WithRuledOut(IReadOnlyList<string> counters, IReadOnlyList<string> ruledOut)
    {
        var merged = counters.ToList();
        foreach (var sentence in ruledOut)
        {
            var id = sentence.Split(' ').FirstOrDefault(word => word.Contains('-', StringComparison.Ordinal))?.Trim('(', ')', '.');
            if (id is null || !merged.Any(counter => counter.Contains(id, StringComparison.OrdinalIgnoreCase)))
            {
                merged.Add(sentence);
            }
        }

        return merged;
    }

    /// <summary>The AI sometimes lists bare evidence ids as counter-evidence; show the record title instead.</summary>
    public static IReadOnlyList<string> Readable(IReadOnlyList<string> counters, IReadOnlyList<Mps.Domain.Evidence.EvidenceItem> items)
    {
        ArgumentNullException.ThrowIfNull(counters);
        ArgumentNullException.ThrowIfNull(items);
        return counters
            .Select(counter => Guid.TryParse(counter.Trim(), out var id)
                ? items.FirstOrDefault(item => item.Id.Value == id)?.Title
                : counter)
            .OfType<string>()
            .Where(counter => counter.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
