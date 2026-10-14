// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Errors;
using Mps.Application.Lifecycle;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;

namespace Mps.Application.Decisions;

public sealed record HumanDecisionOutcome(InvestigationSnapshot Investigation, StoredCapa? Capa);

/// <summary>
/// Records a human decision on the lifecycle. Approve does not create a CAPA until the approval token service runs.
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
    private readonly IInvestigationLog _investigations;
    private readonly IIncidentLog _incidents;
    private readonly IQualityNotifier _notifier;
    private readonly TimeProvider _time;

    public HumanDecisionService(
        IInvestigationLog investigations,
        IIncidentLog incidents,
        IQualityNotifier notifier,
        TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(investigations);
        ArgumentNullException.ThrowIfNull(incidents);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(time);
        _investigations = investigations;
        _incidents = incidents;
        _notifier = notifier;
        _time = time;
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
        var now = _time.GetUtcNow();

        switch (kind)
        {
            case HumanDecisionKind.Approve:
                if (string.IsNullOrWhiteSpace(approverId))
                {
                    throw new IntakeValidationException("Approver id is required.");
                }

                throw new ApprovalForbiddenException("A CAPA is created only with a valid explicit human approval token.");
            case HumanDecisionKind.Reject:
                return await RejectAsync(snapshot, stored, comment, now, cancellationToken).ConfigureAwait(false);
            case HumanDecisionKind.RequestInfo:
                return await RequestInfoAsync(snapshot, stored, comment, now, cancellationToken).ConfigureAwait(false);
            default:
                throw new IntakeValidationException("Decision kind is not supported.");
        }
    }

    private async Task<HumanDecisionOutcome> RejectAsync(
        InvestigationSnapshot snapshot,
        StoredIncident stored,
        string? comment,
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
        _ = comment;
        return new HumanDecisionOutcome(updated, null);
    }

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
