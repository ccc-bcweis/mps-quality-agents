// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Collections.Concurrent;

using Mps.Application.Bus;
using Mps.Application.Errors;
using Mps.Application.Lifecycle;
using Mps.Application.Metrics;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Application.Writes;
using Mps.Domain;
using Mps.Domain.Approval;
using Mps.Domain.Capa;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;

namespace Mps.Application.Decisions;

public sealed record HumanDecisionOutcome(InvestigationSnapshot Investigation, StoredCapa? Capa)
{
    /// <summary>True when the investigation was closed so a new run can gather more evidence.</summary>
    public bool Superseded { get; init; }
}

/// <summary>
/// Records a human decision. A CAPA is created only after a valid explicit approval token is consumed.
/// </summary>
public interface IHumanDecisionService
{
    Task<HumanDecisionOutcome> DecideAsync(
        InvestigationId investigationId,
        HumanDecisionKind kind,
        string? comment,
        string? approverId,
        CancellationToken cancellationToken);
}

public sealed class HumanDecisionService : IHumanDecisionService
{
    public const int MaxWriteAttempts = 3;

    /// <summary>Shortest override justification accepted, in characters (ADR-005).</summary>
    public const int MinOverrideJustification = 20;

    public const string OverridePrefix = "Override of escalation by ";

    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> ApprovalGates = new();

    private readonly IInvestigationLog _investigations;
    private readonly IIncidentLog _incidents;
    private readonly ICapaLog _capas;
    private readonly IApprovalLog _approvals;
    private readonly IQmsCapaWriter _writer;
    private readonly IQualityNotifier _notifier;
    private readonly TimeProvider _time;
    private readonly ApprovalPolicy _policy;
    private readonly IMetricsLog _metrics;
    private readonly ILifecycleEventSink? _events;
    private readonly ICascadeDirectory? _cascade;

    public HumanDecisionService(
        IInvestigationLog investigations,
        IIncidentLog incidents,
        ICapaLog capas,
        IApprovalLog approvals,
        IQmsCapaWriter writer,
        IQualityNotifier notifier,
        TimeProvider time,
        ApprovalPolicy policy,
        IMetricsLog metrics,
        ILifecycleEventSink? events = null,
        ICascadeDirectory? cascade = null)
    {
        ArgumentNullException.ThrowIfNull(investigations);
        ArgumentNullException.ThrowIfNull(incidents);
        ArgumentNullException.ThrowIfNull(capas);
        ArgumentNullException.ThrowIfNull(approvals);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(metrics);
        _investigations = investigations;
        _incidents = incidents;
        _capas = capas;
        _approvals = approvals;
        _writer = writer;
        _notifier = notifier;
        _time = time;
        _policy = policy;
        _metrics = metrics;
        _events = events;
        _cascade = cascade;
    }

    public async Task<HumanDecisionOutcome> DecideAsync(
        InvestigationId investigationId,
        HumanDecisionKind kind,
        string? comment,
        string? approverId,
        CancellationToken cancellationToken)
    {
        if (comment is not null && comment.Length > 4000)
        {
            throw new IntakeValidationException("Comment is too long.");
        }

        var snapshot = await _investigations.GetAsync(investigationId, cancellationToken).ConfigureAwait(false)
            ?? throw new ResourceNotFoundException("Investigation was not found.");
        var stored = await _incidents.GetAsync(snapshot.IncidentId, cancellationToken).ConfigureAwait(false)
            ?? throw new ResourceNotFoundException("Incident was not found.");

        switch (kind)
        {
            case HumanDecisionKind.Approve:
                if (snapshot.Stage == InvestigationStage.Escalated)
                {
                    throw new LifecycleConflictException(
                        "An escalated investigation cannot be approved directly. Request more evidence, or override with a written justification.");
                }

                using (QualityDiagnostics.ActivitySource.StartActivity("decision.approve"))
                {
                    return await ApproveAsync(snapshot, comment, approverId, cancellationToken).ConfigureAwait(false);
                }

            case HumanDecisionKind.Reject:
                using (QualityDiagnostics.ActivitySource.StartActivity("decision.reject"))
                {
                    return await RejectAsync(snapshot, stored, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
                }
            case HumanDecisionKind.RequestInfo:
                if (snapshot.Stage == InvestigationStage.Escalated)
                {
                    return await SupersedeForMoreEvidenceAsync(snapshot, stored, comment, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
                }

                return await RequestInfoAsync(snapshot, stored, comment, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            case HumanDecisionKind.Override:
                if (snapshot.Stage != InvestigationStage.Escalated)
                {
                    throw new LifecycleConflictException("Override applies only to an escalated investigation. Use approve.");
                }

                var justification = comment?.Trim() ?? string.Empty;
                if (justification.Length < MinOverrideJustification)
                {
                    throw new IntakeValidationException(
                        $"Override needs a written justification of at least {MinOverrideJustification} characters.");
                }

                using (var activity = QualityDiagnostics.ActivitySource.StartActivity("decision.override"))
                {
                    activity?.SetTag("mps.override.approver", approverId);
                    activity?.SetTag("mps.override.justification", justification);
                    var audited = OverridePrefix + (approverId ?? "unknown") + ": " + justification;
                    return await ApproveAsync(snapshot, audited, approverId, cancellationToken).ConfigureAwait(false);
                }
            default:
                throw new IntakeValidationException("Decision kind is not supported.");
        }
    }

    private async Task<HumanDecisionOutcome> ApproveAsync(
        InvestigationSnapshot snapshot,
        string? comment,
        string? approverId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(approverId))
        {
            throw new IntakeValidationException("Approver id is required.");
        }

        var gate = ApprovalGates.GetOrAdd(snapshot.Id.Value, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = await _investigations.GetAsync(snapshot.Id, cancellationToken).ConfigureAwait(false)
                ?? throw new ResourceNotFoundException("Investigation was not found.");
            var incident = await _incidents.GetAsync(current.IncidentId, cancellationToken).ConfigureAwait(false)
                ?? throw new ResourceNotFoundException("Incident was not found.");
            var existing = await _approvals.FindByInvestigationAsync(current.Id, cancellationToken).ConfigureAwait(false);
            if (existing is null || existing.Outcome == ApprovalOutcomes.Failed)
            {
                // A stored failure keeps its consumed token. This approve mints a new single-use token.
                return await BeginApprovalAsync(current, incident, comment, approverId, cancellationToken).ConfigureAwait(false);
            }

            return await ResumeAsync(existing, current, incident, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<HumanDecisionOutcome> BeginApprovalAsync(
        InvestigationSnapshot snapshot,
        StoredIncident stored,
        string? comment,
        string approverId,
        CancellationToken cancellationToken)
    {
        var summary = ResolveSummary(comment, snapshot);
        _ = IncidentStagePath.Between(
            stored.Incident.Stage,
            IncidentStage.Approved,
            LifecycleMoves.BlockApproval(stored));
        var capa = OpenCapa(snapshot.IncidentId, summary, approverId);
        var approval = new StoredApproval(
            capa.Token,
            snapshot.Id,
            snapshot.IncidentId,
            summary,
            capa.Action.Id,
            "approve",
            ApprovalOutcomes.Writing,
            string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            null,
            0,
            null);
        await _approvals.AddAsync(approval, cancellationToken).ConfigureAwait(false);
        return await CompleteWriteAsync(capa.Action, approval, stored, snapshot, capa.OpenedAt, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<HumanDecisionOutcome> ResumeAsync(
        StoredApproval existing,
        InvestigationSnapshot snapshot,
        StoredIncident stored,
        CancellationToken cancellationToken)
    {
        if (existing.Outcome == ApprovalOutcomes.Succeeded)
        {
            var capa = await _capas.GetAsync(existing.CapaId, cancellationToken).ConfigureAwait(false)
                ?? throw new ResourceNotFoundException("CAPA was not found.");
            if (stored.Incident.Stage == IncidentStage.Approved)
            {
                return new HumanDecisionOutcome(snapshot, capa);
            }

            var now = _time.GetUtcNow();
            LifecycleMoves.WalkTo(stored, IncidentStage.Approved);
            await _incidents.SaveAsync(stored, now, cancellationToken).ConfigureAwait(false);
            var approved = snapshot.Stage == InvestigationStage.Approved
                ? snapshot
                : Copy(snapshot, InvestigationStage.Approved, snapshot.EscalationReason);
            if (!ReferenceEquals(approved, snapshot))
            {
                await _investigations.UpdateAsync(approved, cancellationToken).ConfigureAwait(false);
                await _notifier.InvestigationChangedAsync(approved, cancellationToken).ConfigureAwait(false);
                await _notifier.IncidentChangedAsync(stored.Incident, cancellationToken).ConfigureAwait(false);
            }

            return new HumanDecisionOutcome(approved, capa);
        }

        if (existing.Outcome == ApprovalOutcomes.Failed)
        {
            throw new InvalidOperationException("A stored QMS failure is retried with a new approval token.");
        }

        EnsureIntact(existing.Token);
        if (!existing.Token.IsConsumed)
        {
            throw new ApprovalForbiddenException("Approval token was not consumed for this action.");
        }

        CapaAction action;
        try
        {
            action = CapaAction.Rehydrate(
                existing.CapaId,
                existing.IncidentId,
                existing.Summary,
                existing.Token.Id,
                existing.Token.ActionHash,
                CapaStage.Open,
                existing.Token.IssuedAt);
        }
        catch (DomainRuleException exception)
        {
            throw new ApprovalForbiddenException(exception.Message, exception);
        }

        return await CompleteWriteAsync(action, existing, stored, snapshot, _time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
    }

    private void EnsureIntact(ApprovalToken token)
    {
        var claims = new ApprovalClaims(token.Id.Value, token.ApproverId, token.ActionHash, token.IssuedAt, token.ExpiresAt);
        if (!ApprovalSignatures.Matches(claims, token.Signature, _policy.SigningKey.Span))
        {
            throw new ApprovalForbiddenException("Approval token signature is invalid.");
        }
    }

    private async Task<HumanDecisionOutcome> CompleteWriteAsync(
        CapaAction capa,
        StoredApproval approval,
        StoredIncident stored,
        InvestigationSnapshot snapshot,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var write = await WriteWithRetryAsync(capa, approval, cancellationToken).ConfigureAwait(false);
        await _approvals.UpdateAsync(write.Approval, cancellationToken).ConfigureAwait(false);
        if (write.ExternalId is null)
        {
            var failed = Copy(snapshot, InvestigationStage.Failed, snapshot.EscalationReason);
            await _investigations.UpdateAsync(failed, cancellationToken).ConfigureAwait(false);
            await _notifier.InvestigationChangedAsync(failed, cancellationToken).ConfigureAwait(false);
            await RecordAsync(snapshot.Id.Value, completed: false, succeeded: false, toolSuccesses: 0, toolFailures: write.Approval.Attempts, cancellationToken)
                .ConfigureAwait(false);
            return new HumanDecisionOutcome(failed, null);
        }

        if (await _capas.GetAsync(capa.Id, cancellationToken).ConfigureAwait(false) is null)
        {
            await _capas.AddAsync(capa, write.ExternalId, cancellationToken).ConfigureAwait(false);
        }
        LifecycleMoves.WalkTo(stored, IncidentStage.Approved);
        await _incidents.SaveAsync(stored, now, cancellationToken).ConfigureAwait(false);
        var approved = Copy(snapshot, InvestigationStage.Approved, snapshot.EscalationReason);
        await _investigations.UpdateAsync(approved, cancellationToken).ConfigureAwait(false);
        await _notifier.InvestigationChangedAsync(approved, cancellationToken).ConfigureAwait(false);
        await PublishApprovalAsync(snapshot, stored, capa, approval, cancellationToken).ConfigureAwait(false);
        await _notifier.IncidentChangedAsync(stored.Incident, cancellationToken).ConfigureAwait(false);
        await _notifier.CapaChangedAsync(capa, "capaChanged", cancellationToken).ConfigureAwait(false);
        await RecordAsync(
            snapshot.Id.Value,
            completed: true,
            succeeded: true,
            toolSuccesses: 1,
            toolFailures: Math.Max(0, write.Approval.Attempts - 1),
            cancellationToken).ConfigureAwait(false);
        var saved = await _capas.GetAsync(capa.Id, cancellationToken).ConfigureAwait(false)
            ?? new StoredCapa(capa, write.ExternalId);
        return new HumanDecisionOutcome(approved, saved);
    }

    private async Task PublishApprovalAsync(
        InvestigationSnapshot snapshot,
        StoredIncident stored,
        CapaAction capa,
        StoredApproval approval,
        CancellationToken cancellationToken)
    {
        if (_events is null)
        {
            return;
        }

        var correlation = snapshot.CorrelationId;
        var recommendation = _cascade?.FindStep(correlation, "recommendation") ?? _cascade?.FindStep(correlation, "investigation");
        var toolId = stored.Incident.ToolId.ToString();
        var commandId = await _events.PublishAsync(
            new LifecycleEvent(
                LifecycleTypes.ApprovalGrant,
                correlation,
                recommendation,
                recommendation,
                "approval",
                null,
                toolId,
                stored.Incident.ChamberId,
                _time.GetUtcNow(),
                new { investigationId = snapshot.Id.Value, approverId = approval.Token.ApproverId, capaId = capa.Id.Value }),
            cancellationToken).ConfigureAwait(false);
        var grantedId = await _events.PublishAsync(
            new LifecycleEvent(
                LifecycleTypes.ApprovalGranted,
                correlation,
                commandId,
                commandId,
                "approval",
                null,
                toolId,
                stored.Incident.ChamberId,
                _time.GetUtcNow(),
                new { investigationId = snapshot.Id.Value, approverId = approval.Token.ApproverId, capaId = capa.Id.Value }),
            cancellationToken).ConfigureAwait(false);
        _cascade?.RememberStep(correlation, "approval", grantedId);
    }

    private async Task<(StoredApproval Approval, string? ExternalId)> WriteWithRetryAsync(
        CapaAction capa,
        StoredApproval approval,
        CancellationToken cancellationToken)
    {
        string? externalId = null;
        string? failure = null;
        var attempts = approval.Attempts;
        for (var extra = 0; extra < MaxWriteAttempts; extra++)
        {
            attempts++;
            try
            {
                externalId = await _writer.CreateAsync(capa, cancellationToken).ConfigureAwait(false);
                failure = null;
                break;
            }
            catch (ConnectorWriteException exception) when (exception.IsIdempotentConflict)
            {
                externalId = exception.ExistingExternalId;
                failure = null;
                break;
            }
            catch (ConnectorWriteException exception) when (exception.IsRetryable)
            {
                externalId = null;
                failure = exception.Message;
            }
            catch (ConnectorWriteException exception)
            {
                externalId = null;
                failure = exception.Message;
                break;
            }
        }

        var updated = approval with
        {
            Outcome = externalId is null ? ApprovalOutcomes.Failed : ApprovalOutcomes.Succeeded,
            ExternalCapaId = externalId,
            Attempts = attempts,
            FailureDetail = externalId is null ? failure : null,
        };
        return (updated, externalId);
    }

    private (CapaAction Action, ApprovalToken Token, DateTimeOffset OpenedAt) OpenCapa(
        IncidentId incidentId,
        string summary,
        string approverId)
    {
        var issuedAt = _time.GetUtcNow();
        ApprovalToken token;
        try
        {
            token = ApprovalToken.Issue(
                ApprovalTokenId.New(),
                approverId,
                CapaAction.ComputeActionHash(incidentId, summary),
                issuedAt,
                _policy.Lifetime,
                _policy.SigningKey.Span);
        }
        catch (DomainRuleException exception)
        {
            throw new ApprovalForbiddenException(exception.Message, exception);
        }

        var openedAt = _time.GetUtcNow();
        try
        {
            var capa = CapaAction.Open(
                CapaId.New(),
                incidentId,
                summary,
                token,
                openedAt,
                _policy.SigningKey.Span);
            return (capa, token, openedAt);
        }
        catch (DomainRuleException exception)
        {
            throw new ApprovalForbiddenException(exception.Message, exception);
        }
    }

    private static string ResolveSummary(string? comment, InvestigationSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(comment))
        {
            return comment.Trim();
        }

        foreach (var hypothesis in snapshot.Hypotheses)
        {
            foreach (var action in hypothesis.Actions)
            {
                if (!string.IsNullOrWhiteSpace(action))
                {
                    return action.Trim();
                }
            }
        }

        return "Hold the affected material and apply the recommended corrective action.";
    }

    private async Task<HumanDecisionOutcome> RejectAsync(
        InvestigationSnapshot snapshot,
        StoredIncident stored,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (snapshot.Stage == InvestigationStage.Rejected)
        {
            return new HumanDecisionOutcome(snapshot, null);
        }

        LifecycleMoves.WalkTo(stored, IncidentStage.Rejected);
        await _incidents.SaveAsync(stored, now, cancellationToken).ConfigureAwait(false);
        var updated = Copy(snapshot, InvestigationStage.Rejected, snapshot.EscalationReason);
        await _investigations.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        await _notifier.InvestigationChangedAsync(updated, cancellationToken).ConfigureAwait(false);
        await _notifier.IncidentChangedAsync(stored.Incident, cancellationToken).ConfigureAwait(false);
        await RecordAsync(snapshot.Id.Value, completed: true, succeeded: true, toolSuccesses: 0, toolFailures: 0, cancellationToken)
            .ConfigureAwait(false);
        return new HumanDecisionOutcome(updated, null);
    }

    private Task RecordAsync(
        Guid investigationId,
        bool completed,
        bool succeeded,
        int toolSuccesses,
        int toolFailures,
        CancellationToken cancellationToken) =>
        _metrics.RecordAsync(
            new InvestigationObservation(
                investigationId,
                _time.GetUtcNow(),
                completed,
                succeeded,
                null,
                toolSuccesses,
                toolFailures,
                null,
                0d),
            cancellationToken);

    private async Task<HumanDecisionOutcome> RequestInfoAsync(
        InvestigationSnapshot snapshot,
        StoredIncident stored,
        string? comment,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var reason = string.IsNullOrWhiteSpace(comment) ? "More information was requested." : comment.Trim();
        if (stored.Incident.Stage != IncidentStage.Escalated)
        {
            LifecycleMoves.WalkTo(stored, IncidentStage.Escalated);
            await _incidents.SaveAsync(stored, now, cancellationToken).ConfigureAwait(false);
        }

        var updated = Copy(snapshot, InvestigationStage.Escalated, reason);
        await _investigations.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        await _notifier.InvestigationChangedAsync(updated, cancellationToken).ConfigureAwait(false);
        await _notifier.IncidentChangedAsync(stored.Incident, cancellationToken).ConfigureAwait(false);
        return new HumanDecisionOutcome(updated, null);
    }

    /// <summary>
    /// "Request more evidence" on an escalated investigation closes it as superseded and returns the incident to
    /// investigating, so the endpoint can start a fresh run with the added data (ADR-005).
    /// </summary>
    private async Task<HumanDecisionOutcome> SupersedeForMoreEvidenceAsync(
        InvestigationSnapshot snapshot,
        StoredIncident stored,
        string? comment,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var note = string.IsNullOrWhiteSpace(comment) ? "More evidence requested." : comment.Trim();
        var updated = Copy(snapshot, InvestigationStage.Rejected, "Superseded: more evidence requested. " + note);
        await _investigations.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        if (stored.Incident.Stage == IncidentStage.Escalated)
        {
            LifecycleMoves.WalkTo(stored, IncidentStage.Investigating);
            await _incidents.SaveAsync(stored, now, cancellationToken).ConfigureAwait(false);
        }

        await _notifier.InvestigationChangedAsync(updated, cancellationToken).ConfigureAwait(false);
        await _notifier.IncidentChangedAsync(stored.Incident, cancellationToken).ConfigureAwait(false);
        return new HumanDecisionOutcome(updated, null) { Superseded = true };
    }

    private static InvestigationSnapshot Copy(InvestigationSnapshot snapshot, InvestigationStage stage, string? reason) =>
        new(
            snapshot.Id,
            snapshot.IncidentId,
            stage,
            snapshot.Evidence,
            snapshot.Hypotheses,
            reason,
            snapshot.CorrelationId);
}
