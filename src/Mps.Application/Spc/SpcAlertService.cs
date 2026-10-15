// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Bus;
using Mps.Application.Intake;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;

namespace Mps.Application.Spc;

/// <summary>
/// Evaluates a sample, debounces per tool and parameter, and opens one intake ticket when severity is high or critical.
/// </summary>
public sealed class SpcAlertService
{
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMinutes(10);

    private readonly IAlertLog _alerts;
    private readonly IIntakeService _intake;
    private readonly IQualityNotifier _notifier;
    private readonly TimeSpan _debounce;
    private readonly ICascadeDirectory? _hints;

    public SpcAlertService(
        IAlertLog alerts,
        IIntakeService intake,
        IQualityNotifier notifier,
        TimeSpan debounce,
        ICascadeDirectory? hints = null)
    {
        ArgumentNullException.ThrowIfNull(alerts);
        ArgumentNullException.ThrowIfNull(intake);
        ArgumentNullException.ThrowIfNull(notifier);
        if (debounce < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(debounce), "Debounce window cannot be negative.");
        }

        _alerts = alerts;
        _intake = intake;
        _notifier = notifier;
        _debounce = debounce;
        _hints = hints;
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
            return new SpcAlertOutcome(violations, null, null, null, debounced: false);
        }

        var at = violations.Max(violation => violation.At);
        var history = await _alerts.ListBySignalAsync(sample.ToolId, sample.Parameter, sample.ChamberId, cancellationToken)
            .ConfigureAwait(false);
        if (SpcDebounce.ShouldSuppress(history, sample.ToolId, sample.ChamberId, sample.Parameter, at, _debounce))
        {
            return new SpcAlertOutcome(violations, null, null, null, debounced: true);
        }

        var severity = WesternElectricEvaluator.Highest(violations);
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
        await _alerts.AddAsync(alert, cancellationToken).ConfigureAwait(false);
        await _notifier.AlertRaisedAsync(alert, cancellationToken).ConfigureAwait(false);

        if (!SeverityRules.OpensTicketFromSignal(severity))
        {
            return new SpcAlertOutcome(violations, alert, null, null, debounced: false);
        }

        var summary = "SPC " + rule + " on " + sample.ToolId + " " + sample.Parameter + ".";
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
                at),
            cancellationToken).ConfigureAwait(false);
        return new SpcAlertOutcome(violations, alert, intake.Incident, intake.Ticket, debounced: false);
    }
}
