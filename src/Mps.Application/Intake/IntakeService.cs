// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Errors;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Application.Writes;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;

namespace Mps.Application.Intake;

public interface IIntakeService
{
    Task<IntakeResult> AcceptAsync(IntakeSubmission submission, CancellationToken cancellationToken);
}

public sealed class IntakeService : IIntakeService
{
    public const int MaxSummaryLength = 4000;

    public const int MaxParameterLength = 200;

    private readonly IFleetCatalog _fleet;
    private readonly IIncidentLog _incidents;
    private readonly ITicketingPort _ticketing;
    private readonly IQualityNotifier _notifier;
    private readonly TimeProvider _time;

    public IntakeService(
        IFleetCatalog fleet,
        IIncidentLog incidents,
        ITicketingPort ticketing,
        IQualityNotifier notifier,
        TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(incidents);
        ArgumentNullException.ThrowIfNull(ticketing);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(time);
        _fleet = fleet;
        _incidents = incidents;
        _ticketing = ticketing;
        _notifier = notifier;
        _time = time;
    }

    public async Task<IntakeResult> AcceptAsync(IntakeSubmission submission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);
        if (string.IsNullOrWhiteSpace(submission.ToolId))
        {
            throw new IntakeValidationException("Tool id is required.");
        }

        var parameter = submission.Parameter.Trim();
        if (parameter.Length == 0)
        {
            throw new IntakeValidationException("Parameter is required.");
        }

        if (parameter.Length > MaxParameterLength)
        {
            throw new IntakeValidationException("Parameter is too long.");
        }

        if (submission.Summary.Length > MaxSummaryLength)
        {
            throw new IntakeValidationException("Summary is too long.");
        }

        var summary = submission.Summary.Trim();
        if (summary.Length == 0)
        {
            throw new IntakeValidationException("Summary is required.");
        }

        if (ContainsDisallowedControl(submission.Summary))
        {
            throw new IntakeValidationException("Summary contains disallowed control characters.");
        }

        var toolId = new ToolId(submission.ToolId);
        var tool = await _fleet.FindAsync(toolId, cancellationToken).ConfigureAwait(false);
        if (tool is null)
        {
            throw new IntakeValidationException("Tool was not found.");
        }

        var severity = SeverityRules.Resolve(submission.Channel, submission.RequestedSeverity, submission.SignalSeverity);
        var open = await _incidents.FindOpenAsync(toolId, parameter, cancellationToken).ConfigureAwait(false);
        if (open is not null)
        {
            return new IntakeResult(open.Incident, null, linkedToExisting: true, open.EscalationRequired);
        }

        var occurredAt = submission.OccurredAt ?? _time.GetUtcNow();
        var correlationId = submission.CorrelationId ?? "intake-" + Guid.NewGuid().ToString("N");
        var chamberId = string.IsNullOrWhiteSpace(submission.ChamberId) ? null : submission.ChamberId.Trim();
        var incident = Incident.Open(
            IncidentId.New(),
            toolId,
            chamberId,
            parameter,
            submission.Channel,
            severity,
            summary,
            correlationId,
            occurredAt);
        var stored = new StoredIncident(incident, SeverityRules.ForcesEscalation(severity), hasEscalated: false);
        await _incidents.AddAsync(stored, cancellationToken).ConfigureAwait(false);
        var ticket = await _ticketing.OpenAsync(incident, summary, cancellationToken).ConfigureAwait(false);
        await _notifier.TicketOpenedAsync(ticket, cancellationToken).ConfigureAwait(false);
        await _notifier.IncidentChangedAsync(incident, cancellationToken).ConfigureAwait(false);
        return new IntakeResult(incident, ticket, linkedToExisting: false, stored.EscalationRequired);
    }

    private static bool ContainsDisallowedControl(string value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character) && character is not '\n' and not '\r' and not '\t')
            {
                return true;
            }
        }

        return false;
    }
}
