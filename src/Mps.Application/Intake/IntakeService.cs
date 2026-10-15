// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Bus;
using Mps.Application.Errors;
using Mps.Application.Metadata;
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
    private readonly IBusinessRuleEngine _rules;
    private readonly ILifecycleEventSink? _events;
    private readonly ICascadeDirectory? _hints;

    public IntakeService(
        IFleetCatalog fleet,
        IIncidentLog incidents,
        ITicketingPort ticketing,
        IQualityNotifier notifier,
        TimeProvider time,
        IBusinessRuleEngine rules,
        ILifecycleEventSink? events = null,
        ICascadeDirectory? hints = null)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(incidents);
        ArgumentNullException.ThrowIfNull(ticketing);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(rules);
        _fleet = fleet;
        _incidents = incidents;
        _ticketing = ticketing;
        _notifier = notifier;
        _time = time;
        _rules = rules;
        _events = events;
        _hints = hints;
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
            return new IntakeResult(open.Incident, null, linkedToExisting: true, open.EscalationRequired, open.Decision);
        }

        var decision = _rules.Evaluate(new RuleContext
        {
            Severity = severity,
            ToolId = submission.ToolId,
            ParameterId = parameter,
            Channel = submission.Channel.ToString(),
            AlertRuleId = submission.AlertRuleId,
            RepeatCount = submission.RepeatCount,
            Confidence = submission.Confidence,
            EvidenceSufficiency = submission.EvidenceSufficiency,
        });
        var occurredAt = submission.OccurredAt ?? _time.GetUtcNow();
        var correlationId = CommandAmbient.CorrelationId
            ?? submission.CorrelationId
            ?? "intake-" + Guid.NewGuid().ToString("N");
        var chamberId = string.IsNullOrWhiteSpace(submission.ChamberId) ? null : submission.ChamberId.Trim();
        var opened = await OpenCommandAsync(
            correlationId,
            toolId.ToString(),
            chamberId,
            parameter,
            summary,
            occurredAt,
            cancellationToken).ConfigureAwait(false);
        using var command = CommandAmbient.Adopt(opened);
        if (decision.OpensTicket)
        {
            await PublishRuleAsync(decision, correlationId, toolId.ToString(), chamberId, occurredAt, cancellationToken)
                .ConfigureAwait(false);
        }

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
        var stored = new StoredIncident(incident, decision.EscalatesToHuman, hasEscalated: false, decision);
        await _incidents.AddAsync(stored, cancellationToken).ConfigureAwait(false);
        var ticket = await _ticketing.OpenAsync(incident, summary, cancellationToken).ConfigureAwait(false);
        await _notifier.TicketOpenedAsync(ticket, cancellationToken).ConfigureAwait(false);
        await _notifier.IncidentChangedAsync(incident, cancellationToken).ConfigureAwait(false);
        return new IntakeResult(incident, ticket, linkedToExisting: false, stored.EscalationRequired, decision);
    }

    private async ValueTask<CommandAmbient.Opened?> OpenCommandAsync(
        string correlationId,
        string toolId,
        string? chamberId,
        string parameter,
        string summary,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        if (_events is null || CommandAmbient.EventId is not null)
        {
            return null;
        }

        var eventId = await _events.PublishAsync(
            new LifecycleEvent(
                LifecycleTypes.OperatorReport,
                correlationId,
                null,
                null,
                "operator",
                null,
                toolId,
                chamberId,
                occurredAt,
                new { toolId, chamberId, parameter, summary }),
            cancellationToken).ConfigureAwait(false);
        return new CommandAmbient.Opened(correlationId, eventId);
    }

    private async Task PublishRuleAsync(
        RuleDecision decision,
        string correlationId,
        string toolId,
        string? chamberId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        if (_events is null || _hints?.FindStep(correlationId, "rule") is not null)
        {
            return;
        }

        var parent = _hints?.FindStep(correlationId, "alert") ?? CommandAmbient.EventId;
        var eventId = await _events.PublishAsync(
            new LifecycleEvent(
                LifecycleTypes.RuleMatched,
                correlationId,
                parent,
                parent,
                "rules",
                null,
                toolId,
                chamberId,
                occurredAt,
                new
                {
                    ruleId = decision.RuleId,
                    name = decision.RuleName,
                    why = decision.Why,
                    queue = decision.Queue,
                }),
            cancellationToken).ConfigureAwait(false);
        _hints?.RememberStep(correlationId, "rule", eventId);
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
