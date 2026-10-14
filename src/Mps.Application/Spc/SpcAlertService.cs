// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Application.Writes;
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
    private readonly IIncidentLog _incidents;
    private readonly ITicketingPort _ticketing;
    private readonly IQualityNotifier _notifier;
    private readonly TimeSpan _debounce;

    public SpcAlertService(
        IAlertLog alerts,
        IIncidentLog incidents,
        ITicketingPort ticketing,
        IQualityNotifier notifier,
        TimeSpan debounce)
    {
        ArgumentNullException.ThrowIfNull(alerts);
        ArgumentNullException.ThrowIfNull(incidents);
        ArgumentNullException.ThrowIfNull(ticketing);
        ArgumentNullException.ThrowIfNull(notifier);
        if (debounce < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(debounce), "Debounce window cannot be negative.");
        }

        _alerts = alerts;
        _incidents = incidents;
        _ticketing = ticketing;
        _notifier = notifier;
        _debounce = debounce;
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
        var correlationId = "spc-" + Guid.NewGuid().ToString("N");
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

        if (severity < Severity.High)
        {
            return new SpcAlertOutcome(violations, alert, null, null, debounced: false);
        }

        var open = await _incidents.FindOpenAsync(sample.ToolId, sample.Parameter, cancellationToken).ConfigureAwait(false);
        if (open is not null)
        {
            return new SpcAlertOutcome(violations, alert, open.Incident, null, debounced: false);
        }

        var summary = "SPC " + rule + " on " + sample.ToolId + " " + sample.Parameter + ".";
        var incident = Incident.Open(
            IncidentId.New(),
            sample.ToolId,
            sample.ChamberId,
            sample.Parameter,
            IncidentChannel.IotSpcAlert,
            severity,
            summary,
            correlationId,
            at);
        var stored = new StoredIncident(incident, escalationRequired: severity == Severity.Critical, hasEscalated: false);
        await _incidents.AddAsync(stored, cancellationToken).ConfigureAwait(false);
        var ticket = await _ticketing.OpenAsync(incident, summary, cancellationToken).ConfigureAwait(false);
        await _notifier.TicketOpenedAsync(ticket, cancellationToken).ConfigureAwait(false);
        await _notifier.IncidentChangedAsync(incident, cancellationToken).ConfigureAwait(false);
        return new SpcAlertOutcome(violations, alert, incident, ticket, debounced: false);
    }
}
