// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Bus;
using Mps.Application.Intake;
using Mps.Application.Metadata;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Domain.Events;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Telemetry;

namespace Mps.Application.Spc;

/// <summary>
/// Writes an alert only when the rule engine reports a state change, then asks the business rules whether that opens a ticket.
/// </summary>
public sealed class SpcAlertService
{
    private readonly IAlertLog _alerts;
    private readonly IIntakeService _intake;
    private readonly IQualityNotifier _notifier;
    private readonly TimeSpan _debounce;
    private readonly IBusinessRuleEngine _rules;
    private readonly ICascadeDirectory? _hints;
    private readonly ILifecycleEventSink? _events;
    private readonly SpcTicketCap? _cap;
    private readonly SpcRuleSettings _settings;

    public SpcAlertService(
        IAlertLog alerts,
        IIntakeService intake,
        IQualityNotifier notifier,
        TimeSpan debounce,
        IBusinessRuleEngine rules,
        ICascadeDirectory? hints = null,
        ILifecycleEventSink? events = null,
        SpcTicketCap? ticketCap = null,
        SpcRuleSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(alerts);
        ArgumentNullException.ThrowIfNull(intake);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(rules);
        if (debounce < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(debounce), "Debounce window cannot be negative.");
        }

        _alerts = alerts;
        _intake = intake;
        _notifier = notifier;
        _debounce = debounce;
        _rules = rules;
        _hints = hints;
        _events = events;
        _cap = ticketCap;
        _settings = settings ?? SpcRuleSettings.Shared;
    }

    public async Task<SpcAlertOutcome> ApplyAsync(SpcObservation observation, SpcDisplay? display, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.Action == SpcAction.None)
        {
            return Empty();
        }

        var reading = observation.Reading;
        var toolId = new ToolId(reading.ToolId);
        var history = await _alerts.ListBySignalAsync(toolId, reading.Parameter, reading.ChamberId, cancellationToken).ConfigureAwait(false);
        var active = history.Where(item => item.IsActive).OrderByDescending(item => item.RaisedAt).FirstOrDefault();
        var violations = Violations(observation, toolId);
        var sample = Sample(observation, toolId, display);

        if (observation.Action == SpcAction.Clear)
        {
            return await ClearAsync(observation, sample, history, violations, cancellationToken).ConfigureAwait(false);
        }

        if (active is null && observation.Action == SpcAction.Raise
            && SpcDebounce.ShouldSuppress(history, toolId, reading.ChamberId, reading.Parameter, reading.At, _debounce, observation.Severity))
        {
            return new SpcAlertOutcome(violations, null, null, null, debounced: true);
        }

        if (active is null)
        {
            var alert = new Alert(
                AlertId.New(),
                toolId,
                reading.ChamberId,
                reading.Parameter,
                observation.Severity,
                observation.RuleText,
                reading.At,
                observation.CorrelationId);
            await _alerts.AddAsync(alert, cancellationToken).ConfigureAwait(false);
            await _notifier.AlertRaisedAsync(alert, cancellationToken).ConfigureAwait(false);
            return await DecideAsync(sample, violations, alert, observation.RuleText, observation.Severity, reading.At, observation.CorrelationId, history.Count, cancellationToken)
                .ConfigureAwait(false);
        }

        var rulesBefore = active.Rule;
        var escalated = active.Update(observation.RuleText, observation.Severity, reading.At, newSamples: 1);
        await _alerts.UpdateAsync(active, cancellationToken).ConfigureAwait(false);
        if (escalated || !string.Equals(rulesBefore, active.Rule, StringComparison.Ordinal))
        {
            await _notifier.AlertRaisedAsync(active, cancellationToken).ConfigureAwait(false);
        }

        if (!escalated && observation.Action != SpcAction.Escalate)
        {
            return new SpcAlertOutcome(violations, active, null, null, debounced: true);
        }

        return await DecideAsync(sample, violations, active, active.Rule, active.Severity, reading.At, active.CorrelationId, history.Count - 1, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<SpcAlertOutcome> ClearAsync(
        SpcObservation observation,
        SpcSample sample,
        IReadOnlyList<Alert> history,
        IReadOnlyList<SpcViolation> violations,
        CancellationToken cancellationToken)
    {
        var active = history.Where(alert => alert.IsActive).ToArray();
        if (active.Length == 0)
        {
            return Empty();
        }

        var at = observation.Reading.At;
        var readings = observation.ClearReason == "expiry" ? observation.QuietReadings : observation.InControlReadings;
        var why = observation.ClearReason == "expiry"
            ? $"watch expired after {readings} readings with no new violation"
            : $"{readings} readings in a row inside +/-{_settings.Clearing.Sigma:0.###} sigma";
        Alert? last = null;
        foreach (var alert in active)
        {
            await _alerts.ClearAsync(alert.Id, at, cancellationToken).ConfigureAwait(false);
            alert.Clear(at);
            last = alert;
            if (_events is null)
            {
                continue;
            }

            await _events.PublishAsync(
                new LifecycleEvent(
                    EventTypes.AlertCleared,
                    alert.CorrelationId,
                    null,
                    _hints?.FindStep(alert.CorrelationId, "alert"),
                    "spc",
                    null,
                    sample.ToolId.ToString(),
                    sample.ChamberId,
                    at,
                    new
                    {
                        signal = "alertCleared",
                        alertId = alert.Id.Value,
                        toolId = sample.ToolId.ToString(),
                        clearedAt = at,
                        parameter = sample.Parameter,
                        readings,
                        why,
                    }),
                cancellationToken).ConfigureAwait(false);
        }

        return new SpcAlertOutcome(violations, last, null, null, debounced: false);
    }

    private async Task<SpcAlertOutcome> DecideAsync(
        SpcSample sample,
        IReadOnlyList<SpcViolation> violations,
        Alert alert,
        string rule,
        Severity severity,
        DateTimeOffset at,
        string correlationId,
        int repeatCount,
        CancellationToken cancellationToken)
    {
        var decision = _rules.Evaluate(new RuleContext
        {
            AlertRuleId = rule,
            Severity = severity,
            ParameterId = sample.Parameter,
            ToolId = sample.ToolId.ToString(),
            RepeatCount = repeatCount,
            Channel = IncidentChannel.IotSpcAlert.ToString(),
        });
        if (!decision.OpensTicket)
        {
            return new SpcAlertOutcome(violations, alert, null, null, debounced: false, decision);
        }

        var cap = _settings.TicketCap.PerToolPerHour;
        if (_cap is not null && !_cap.TryAdmit(sample.ToolId.ToString(), at, cap))
        {
            await PublishCappedAsync(sample, correlationId, at, cap, cancellationToken).ConfigureAwait(false);
            return new SpcAlertOutcome(violations, alert, null, null, debounced: false, decision, ticketSuppressed: true);
        }

        await PublishRuleAsync(sample, correlationId, decision, at, cancellationToken).ConfigureAwait(false);
        var summary = SpcSummary.Describe(sample, violations);
        var intake = await _intake.AcceptAsync(
            new IntakeSubmission(
                IncidentChannel.IotSpcAlert,
                sample.ToolId.ToString(),
                sample.ChamberId,
                sample.Parameter,
                summary,
                requestedSeverity: null,
                signalSeverity: severity,
                correlationId,
                at,
                alertRuleId: rule,
                repeatCount: repeatCount),
            cancellationToken).ConfigureAwait(false);
        return new SpcAlertOutcome(violations, alert, intake.Incident, intake.Ticket, debounced: false, decision);
    }

    private static SpcAlertOutcome Empty() =>
        new([], null, null, null, debounced: false);

    private static SpcSample Sample(SpcObservation observation, ToolId toolId, SpcDisplay? display)
    {
        var reading = observation.Reading;
        var baseline = new ControlBaseline(reading.Center, Math.Max(reading.Sigma, 1e-6d), reading.SpecLow, reading.SpecHigh);
        return new SpcSample(toolId, reading.ChamberId, reading.Parameter, baseline, [new TelemetryPoint(reading.At, reading.RawValue)])
        {
            Display = display,
        };
    }

    private static IReadOnlyList<SpcViolation> Violations(SpcObservation observation, ToolId toolId)
    {
        if (observation.LiveRules.Count == 0 || observation.RuleText.Length == 0)
        {
            return [];
        }

        var reading = observation.Reading;
        return
        [
            new SpcViolation(observation.RuleText, toolId, reading.ChamberId, reading.Parameter, reading.At, reading.RawValue, Math.Max(reading.Sigma, 1e-6d), observation.Severity)
            {
                Center = reading.Center,
                LowerSpec = reading.SpecLow,
                UpperSpec = reading.SpecHigh,
            },
        ];
    }

    private async Task PublishCappedAsync(
        SpcSample sample,
        string correlationId,
        DateTimeOffset at,
        int cap,
        CancellationToken cancellationToken)
    {
        if (_events is null)
        {
            return;
        }

        await _events.PublishAsync(
            new LifecycleEvent(
                EventTypes.TicketCapped,
                correlationId,
                null,
                _hints?.FindStep(correlationId, "alert"),
                "spc",
                null,
                sample.ToolId.ToString(),
                sample.ChamberId,
                at,
                new
                {
                    signal = "ticketCapped",
                    toolId = sample.ToolId.ToString(),
                    parameter = sample.Parameter,
                    at,
                    cap,
                    opened = cap,
                }),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishRuleAsync(
        SpcSample sample,
        string correlationId,
        RuleDecision decision,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (_events is null)
        {
            return;
        }

        var parent = _hints?.FindStep(correlationId, "alert");
        var eventId = await _events.PublishAsync(
            new LifecycleEvent(
                EventTypes.RuleMatched,
                correlationId,
                parent,
                parent,
                "rules",
                null,
                sample.ToolId.ToString(),
                sample.ChamberId,
                at,
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
}
