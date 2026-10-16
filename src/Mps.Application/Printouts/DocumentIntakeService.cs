// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

using Mps.Application.Bus;
using Mps.Application.Errors;
using Mps.Application.Faults;
using Mps.Application.Intake;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;

namespace Mps.Application.Printouts;

public sealed record PrintoutInspection(PrintoutReading Reading, string ParseMethod);

public sealed record PrintoutConfirmation(
    Guid EvidenceId,
    Guid IncidentId,
    Guid? FaultId,
    string? CorrelationId,
    Guid? AlertId,
    bool AppliedToSimulation,
    bool OutOfSpec,
    string PrintedParameter,
    string CatalogParameter);

public interface IDocumentIntake
{
    Task<PrintoutInspection> InspectAsync(
        ReadOnlyMemory<byte> bytes,
        string? mediaType,
        string? fileName,
        CancellationToken cancellationToken);

    Task<PrintoutConfirmation> ConfirmAsync(PrintoutReading reading, CancellationToken cancellationToken);

    Task<PrintoutConfirmation> ConfirmAsync(PrintoutReading reading, Guid? fileId, string? fileName, CancellationToken cancellationToken) =>
        ConfirmAsync(reading, cancellationToken);
}

/// <summary>
/// Confirms a bench printout. The reading is stored as evidence and linked to an operator incident.
/// A reading outside the printed spec is injected and applied to the simulation, and an alert is raised.
/// </summary>
public sealed class DocumentIntakeService : IDocumentIntake
{
    private readonly IPrintoutTextExtractor _extractor;
    private readonly IPrintoutFieldParser _parser;
    private readonly IPrintoutModelReader _model;
    private readonly IIntakeService _intake;
    private readonly IFaultInjectionService _faults;
    private readonly IAlertLog _alerts;
    private readonly IQualityNotifier _notifier;
    private readonly IManualMeasurementLog _measurements;
    private readonly ISimulationFaultPort _simulation;
    private readonly TimeProvider _time;
    private readonly ILifecycleEventSink? _events;
    private readonly ICascadeDirectory? _cascade;

    public DocumentIntakeService(
        IPrintoutTextExtractor extractor,
        IPrintoutFieldParser parser,
        IPrintoutModelReader model,
        IIntakeService intake,
        IFaultInjectionService faults,
        IAlertLog alerts,
        IQualityNotifier notifier,
        IManualMeasurementLog measurements,
        ISimulationFaultPort simulation,
        TimeProvider time,
        ILifecycleEventSink? events = null,
        ICascadeDirectory? cascade = null)
    {
        ArgumentNullException.ThrowIfNull(extractor);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(intake);
        ArgumentNullException.ThrowIfNull(faults);
        ArgumentNullException.ThrowIfNull(alerts);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(measurements);
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(time);
        _extractor = extractor;
        _parser = parser;
        _model = model;
        _intake = intake;
        _faults = faults;
        _alerts = alerts;
        _notifier = notifier;
        _measurements = measurements;
        _simulation = simulation;
        _time = time;
        _events = events;
        _cascade = cascade;
    }

    public async Task<PrintoutInspection> InspectAsync(
        ReadOnlyMemory<byte> bytes,
        string? mediaType,
        string? fileName,
        CancellationToken cancellationToken)
    {
        var text = _extractor.Extract(bytes, mediaType, fileName);
        if (_parser.TryParse(text, out var parsed))
        {
            return new PrintoutInspection(parsed, "text");
        }

        var model = await _model.ReadAsync(bytes, mediaType, fileName, cancellationToken).ConfigureAwait(false);
        if (model is null)
        {
            throw new IntakeValidationException("The printout could not be read.");
        }

        return new PrintoutInspection(model, "model");
    }

    public Task<PrintoutConfirmation> ConfirmAsync(PrintoutReading reading, CancellationToken cancellationToken) =>
        ConfirmAsync(reading, null, null, cancellationToken);

    public async Task<PrintoutConfirmation> ConfirmAsync(PrintoutReading reading, Guid? fileId, string? fileName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reading);
        var catalogParameter = PrintoutReading.CatalogParameter(reading.ToolId, reading.Parameter);
        var summary = Summary(reading, catalogParameter);
        var severity = reading.OutOfSpec ? Severity.High : Severity.Low;
        var opened = await OpenCommandAsync(reading, catalogParameter, cancellationToken).ConfigureAwait(false);
        using var command = CommandAmbient.Adopt(opened);

        // The fault, the reading, and the alert land before intake so the business rule hangs off the alert.
        Guid? faultId = null;
        string? faultCorrelation = null;
        var applied = false;
        Alert? alert = null;
        if (reading.OutOfSpec)
        {
            var shape = reading.Shape ?? "step";
            var magnitude = reading.MagnitudeSigma();
            var accepted = await _faults.InjectAsync(
                new FaultInjectionRequest(
                    reading.ToolId,
                    reading.ChamberId,
                    catalogParameter,
                    shape,
                    magnitude,
                    RootCauseCategory.EquipmentCalibrationOrAlignment,
                    false),
                cancellationToken).ConfigureAwait(false);
            faultId = accepted.FaultId;
            faultCorrelation = accepted.CorrelationId;
            // A hosted bus applies the fault while the command is published. Applying it here as well stacks the signal.
            applied = _events is not null
                || _simulation.TryApply(reading.ToolId, reading.ChamberId, catalogParameter, shape, magnitude);
            // Times use the plant clock at confirm, so the reading sorts before its alert and ticket. The printed time stays on the sheet.
            await PublishReadingAsync(reading, catalogParameter, accepted.CorrelationId, cancellationToken).ConfigureAwait(false);
            alert = new Alert(
                AlertId.New(),
                new ToolId(reading.ToolId),
                reading.ChamberId,
                catalogParameter,
                Severity.High,
                ManualMeasurement.AlertRule,
                _time.GetUtcNow(),
                accepted.CorrelationId);
            await _alerts.AddAsync(alert, cancellationToken).ConfigureAwait(false);
            await _notifier.AlertRaisedAsync(alert, cancellationToken).ConfigureAwait(false);
        }

        var intake = await _intake.AcceptAsync(
            new IntakeSubmission(
                IncidentChannel.OperatorReport,
                reading.ToolId,
                reading.ChamberId,
                catalogParameter,
                summary,
                severity,
                signalSeverity: null,
                faultCorrelation ?? CommandAmbient.CorrelationId,
                _time.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);

        var evidence = new EvidenceItem(
            EvidenceId.New(),
            EvidenceSource.ManualMeasurement,
            Title(reading),
            Excerpt(reading, catalogParameter),
            _time.GetUtcNow(),
            reading.ToolId,
            "printout:" + reading.ToolId + ":" + catalogParameter);
        evidence = await StampDocumentAsync(evidence, reading, catalogParameter, intake.Incident.CorrelationId, cancellationToken)
            .ConfigureAwait(false);
        _measurements.Add(new ManualMeasurement(evidence, intake.Incident.Id.Value)
        {
            Reading = reading,
            AlertId = alert?.Id.Value,
            FileId = fileId,
            FileName = fileName,
        });

        if (!reading.OutOfSpec || alert is null || faultId is null)
        {
            return new PrintoutConfirmation(
                evidence.Id.Value,
                intake.Incident.Id.Value,
                null,
                null,
                null,
                false,
                false,
                reading.Parameter,
                catalogParameter);
        }

        return new PrintoutConfirmation(
            evidence.Id.Value,
            intake.Incident.Id.Value,
            faultId,
            faultCorrelation,
            alert.Id.Value,
            applied,
            true,
            reading.Parameter,
            catalogParameter);
    }

    private async ValueTask<CommandAmbient.Opened?> OpenCommandAsync(
        PrintoutReading reading,
        string catalogParameter,
        CancellationToken cancellationToken)
    {
        if (_events is null || CommandAmbient.EventId is not null)
        {
            return null;
        }

        var correlationId = "printout-" + Guid.NewGuid().ToString("N");
        var eventId = await _events.PublishAsync(
            new LifecycleEvent(
                LifecycleTypes.DocumentUpload,
                correlationId,
                null,
                null,
                "printout",
                null,
                reading.ToolId,
                reading.ChamberId,
                _time.GetUtcNow(),
                new { toolId = reading.ToolId, parameter = catalogParameter, value = reading.Value, unit = reading.Unit }),
            cancellationToken).ConfigureAwait(false);
        return new CommandAmbient.Opened(correlationId, eventId);
    }

    private async Task<EvidenceItem> StampDocumentAsync(
        EvidenceItem evidence,
        PrintoutReading reading,
        string catalogParameter,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (_events is null)
        {
            return evidence;
        }

        var parent = CommandAmbient.EventId;
        var eventId = await _events.PublishAsync(
            new LifecycleEvent(
                LifecycleTypes.DocumentUploaded,
                correlationId,
                parent,
                parent,
                "printout",
                null,
                reading.ToolId,
                reading.ChamberId,
                _time.GetUtcNow(),
                new
                {
                    parameter = catalogParameter,
                    value = reading.Value,
                    unit = reading.Unit,
                    low = reading.SpecMinimum,
                    high = reading.SpecMaximum,
                    evidenceId = evidence.Id.Value,
                    observedAt = reading.ObservedAt,
                    printedObservedAt = reading.CorrectedFrom,
                    why = reading.CorrectedFrom is { } printed
                        ? "Observed time corrected by the user from " + Stamp(printed) + " to " + Stamp(reading.ObservedAt) + "."
                        : null,
                }),
            cancellationToken).ConfigureAwait(false);
        return evidence.WithSourceEventIds([eventId]);
    }

    private async Task PublishReadingAsync(
        PrintoutReading reading,
        string catalogParameter,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (_events is null)
        {
            return;
        }

        var parent = _cascade?.FaultEventId(reading.ToolId, catalogParameter) ?? CommandAmbient.EventId;
        await _events.PublishAsync(
            new LifecycleEvent(
                LifecycleTypes.TelemetryReading,
                correlationId,
                parent,
                parent,
                "printout",
                null,
                reading.ToolId,
                reading.ChamberId,
                _time.GetUtcNow(),
                new
                {
                    parameter = catalogParameter,
                    value = reading.Value,
                    unit = reading.Unit,
                    low = reading.SpecMinimum,
                    high = reading.SpecMaximum,
                }),
            cancellationToken).ConfigureAwait(false);
    }

    private static string Summary(PrintoutReading reading, string catalogParameter)
    {
        var applied = catalogParameter.Equals(reading.Parameter, StringComparison.Ordinal)
            ? string.Empty
            : " Applied to " + catalogParameter + ".";
        return "Manual measurement: " + reading.Instrument + " on " + reading.ToolId + ", "
            + reading.Parameter + " " + Num(reading.Value) + " " + reading.Unit
            + " (spec " + Num(reading.SpecMinimum) + "-" + Num(reading.SpecMaximum) + ")."
            + applied;
    }

    private static string Title(PrintoutReading reading)
    {
        return reading.Instrument + " printout";
    }

    private static string Excerpt(PrintoutReading reading, string catalogParameter)
    {
        var shape = reading.Shape is null ? string.Empty : " Shape " + reading.Shape + ".";
        var initials = reading.OperatorInitials is null ? string.Empty : " Operator " + reading.OperatorInitials + ".";
        var observed = " Observed " + Stamp(reading.ObservedAt) + "."
            + (reading.CorrectedFrom is { } printed ? " Corrected by the user from the printed " + Stamp(printed) + "." : string.Empty);
        return reading.Parameter + " " + Num(reading.Value) + " " + reading.Unit
            + ", spec " + Num(reading.SpecMinimum) + "-" + Num(reading.SpecMaximum)
            + ", catalog " + catalogParameter + "."
            + shape
            + initials
            + observed;
    }

    private static string Stamp(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
