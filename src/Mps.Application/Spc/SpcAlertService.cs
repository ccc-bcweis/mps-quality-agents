// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Bus;
using Mps.Application.Intake;
using Mps.Application.Metadata;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Telemetry;

namespace Mps.Application.Spc;

/// <summary>
/// Evaluates a sample, debounces per tool and parameter, and opens one intake ticket when a business rule says to.
/// </summary>
public sealed class SpcAlertService
{
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMinutes(10);

    private readonly IAlertLog _alerts;
    private readonly IIntakeService _intake;
    private readonly IQualityNotifier _notifier;
    private readonly TimeSpan _debounce;
    private readonly IBusinessRuleEngine _rules;
    private readonly ICascadeDirectory? _hints;
    private readonly ILifecycleEventSink? _events;

    public SpcAlertService(
        IAlertLog alerts,
        IIntakeService intake,
        IQualityNotifier notifier,
        TimeSpan debounce,
        IBusinessRuleEngine rules,
        ICascadeDirectory? hints = null,
        ILifecycleEventSink? events = null)
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
    }

    public async Task<SpcAlertOutcome> RaiseAsync(SpcSample sample, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sample);
        var violations = WesternElectricEvaluator.Evaluate(
            sample.ToolId,
            sample.ChamberId,
            sample.Parameter,
            sample.Baseline,
            sample.Points);
        if (violations.Count == 0)
        {
            // Only a series that is back in control may clear: a live violation keeps the alert active.
            await ClearRecoveredAsync(sample, cancellationToken).ConfigureAwait(false);
            return new SpcAlertOutcome(violations, null, null, null, debounced: false);
        }

        var history = await _alerts.ListBySignalAsync(sample.ToolId, sample.Parameter, sample.ChamberId, cancellationToken)
            .ConfigureAwait(false);

        // A point alerts once. Violations that end at or before the newest alert on this signal were already raised
        // (and may since have cleared); a later window that still contains them must not raise them again.
        if (history.Count > 0)
        {
            // The cutoff is the newest violation already folded into an alert (last seen), or its clear time.
            var newest = history.Max(alert => alert.ClearedAt is { } cleared && cleared > alert.LastSeenAt ? cleared : alert.LastSeenAt);
            violations = violations.Where(violation => violation.At > newest).ToArray();
            if (violations.Count == 0)
            {
                await ClearRecoveredAsync(sample, cancellationToken).ConfigureAwait(false);
                return new SpcAlertOutcome(violations, null, null, null, debounced: true);
            }
        }

        var at = violations.Max(violation => violation.At);
        var severity = WesternElectricEvaluator.Highest(violations);

        // One active alert per signal (tool, chamber, parameter). While it is active, new violations update it in place.
        // A new alert is raised only after the previous one cleared.
        var active = history.Where(item => item.IsActive).OrderByDescending(item => item.RaisedAt).FirstOrDefault();
        if (active is not null)
        {
            var rulesBefore = active.Rule;
            var escalated = active.Update(WesternElectricEvaluator.JoinRules(violations), severity, at, DistinctSamples(violations));
            await _alerts.UpdateAsync(active, cancellationToken).ConfigureAwait(false);

            // Broadcast only a real change (a higher severity or a newly broken rule). More readings on the same
            // alert are stored but not re-announced, so open pages do not reload on every evaluation.
            if (escalated || !string.Equals(rulesBefore, active.Rule, StringComparison.Ordinal))
            {
                await _notifier.AlertRaisedAsync(active, cancellationToken).ConfigureAwait(false);
            }
            if (!escalated)
            {
                return new SpcAlertOutcome(violations, active, null, null, debounced: true);
            }

            // Watch to alarm: same alert, upgraded. The rule engine decides on a ticket as it does for a new alarm.
            return await DecideAsync(sample, violations, active, active.Rule, active.Severity, at, active.CorrelationId, history.Count - 1, cancellationToken)
                .ConfigureAwait(false);
        }

        if (SpcDebounce.ShouldSuppress(history, sample.ToolId, sample.ChamberId, sample.Parameter, at, _debounce, severity))
        {
            return new SpcAlertOutcome(violations, null, null, null, debounced: true);
        }

        var rule = WesternElectricEvaluator.JoinRules(violations);
        var correlationId = _hints?.CorrelationFor(sample.ToolId.ToString(), sample.Parameter)
            ?? "spc-" + Guid.NewGuid().ToString("N");
        var alert = new Alert(
            AlertId.New(),
            sample.ToolId,
            sample.ChamberId,
            sample.Parameter,
            severity,
            rule,
            at,
            correlationId);
        alert.Restore(at, DistinctSamples(violations));
        await _alerts.AddAsync(alert, cancellationToken).ConfigureAwait(false);
        await _notifier.AlertRaisedAsync(alert, cancellationToken).ConfigureAwait(false);
        return await DecideAsync(sample, violations, alert, rule, severity, at, correlationId, history.Count, cancellationToken).ConfigureAwait(false);
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

    /// <summary>Readings that broke at least one rule. Several rules on one reading count once.</summary>
    private static int DistinctSamples(IReadOnlyList<SpcViolation> violations) =>
        violations.Select(violation => violation.At).Distinct().Count();

    /// <summary>Clears active alerts on this signal once the last readings have stayed in control long enough.</summary>
    public async Task ClearRecoveredAsync(SpcSample sample, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sample);
        if (sample.Points.Count < AlertClearing.InControlReadings)
        {
            return;
        }

        var history = await _alerts.ListBySignalAsync(sample.ToolId, sample.Parameter, sample.ChamberId, cancellationToken)
            .ConfigureAwait(false);
        var active = history.Where(alert => alert.IsActive).ToArray();
        if (active.Length == 0)
        {
            return;
        }

        // A run inside +/-2 sigma can still be out of control (eight on one side during a slow drift).
        var trailing = sample.Points.Skip(Math.Max(0, sample.Points.Count - AlertClearing.InControlReadings)).ToArray();
        if (WesternElectricEvaluator.Evaluate(sample.ToolId, sample.ChamberId, sample.Parameter, sample.Baseline, sample.Points)
            .Any(violation => violation.At >= trailing[0].At))
        {
            return;
        }

        foreach (var alert in active)
        {
            var run = InControlRun(sample, alert.RaisedAt);
            if (run.Count < AlertClearing.InControlReadings)
            {
                continue;
            }

            var at = run.Last;
            await _alerts.ClearAsync(alert.Id, at, cancellationToken).ConfigureAwait(false);
            alert.Clear(at);
            if (_events is not null)
            {
                await _events.PublishAsync(
                    new LifecycleEvent(
                        LifecycleTypes.AlertCleared,
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
                            alertId = alert.Id.Value,
                            parameter = sample.Parameter,
                            readings = run.Count,
                            why = $"{run.Count} readings in a row inside +/-{AlertClearing.ZoneSigma:0} sigma",
                        }),
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Trailing readings after <paramref name="after"/> that sit inside the clearing zone.</summary>
    internal static (int Count, DateTimeOffset Last) InControlRun(SpcSample sample, DateTimeOffset after)
    {
        var count = 0;
        var last = DateTimeOffset.MinValue;
        for (var index = sample.Points.Count - 1; index >= 0; index--)
        {
            var point = sample.Points[index];
            if (point.At <= after || Math.Abs(sample.Baseline.Z(point.Value)) >= AlertClearing.ZoneSigma)
            {
                break;
            }

            if (count == 0)
            {
                last = point.At;
            }

            count++;
        }

        return (count, last);
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
                LifecycleTypes.RuleMatched,
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
