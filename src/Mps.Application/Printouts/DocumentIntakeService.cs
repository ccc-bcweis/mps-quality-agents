// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

using Mps.Application.Bus;
using Mps.Application.Errors;
using Mps.Application.Faults;
using Mps.Application.Intake;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Domain.Events;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;

namespace Mps.Application.Printouts;

public sealed record PrintoutInspection(PrintoutReading Reading, string ParseMethod);

public sealed record PrintoutConfirmation(
    Guid EvidenceId,
    Guid? IncidentId,
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
    private readonly IManualMeasurementLog _measurements;
    private readonly IManualSampleSink _samples;
    private readonly TimeProvider _time;
    private readonly ILifecycleEventSink? _events;
    private readonly ICascadeDirectory? _cascade;

    public DocumentIntakeService(
        IPrintoutTextExtractor extractor,
        IPrintoutFieldParser parser,
        IPrintoutModelReader model,
        IManualMeasurementLog measurements,
        IManualSampleSink samples,
        TimeProvider time,
        ILifecycleEventSink? events = null,
        ICascadeDirectory? cascade = null)
    {
        ArgumentNullException.ThrowIfNull(extractor);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(measurements);
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(time);
        _extractor = extractor;
        _parser = parser;
        _model = model;
        _measurements = measurements;
        _samples = samples;
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
        var opened = await OpenCommandAsync(reading, catalogParameter, cancellationToken).ConfigureAwait(false);
        using var command = CommandAmbient.Adopt(opened);
        var correlationId = CommandAmbient.CorrelationId ?? "printout-" + Guid.NewGuid().ToString("N");

        var evidence = new EvidenceItem(
            EvidenceId.New(),
            EvidenceSource.ManualMeasurement,
            Title(reading),
            Excerpt(reading, catalogParameter),
            _time.GetUtcNow(),
            reading.ToolId,
            "printout:" + reading.ToolId + ":" + catalogParameter);
        evidence = await StampDocumentAsync(evidence, reading, catalogParameter, correlationId, cancellationToken).ConfigureAwait(false);
        _measurements.Add(new ManualMeasurement(evidence, null)
        {
            Reading = reading,
            FileId = fileId,
            FileName = fileName,
        });

        // No printout workflow: the reading becomes one sample in the same series as the tool's sensor.
        // The plant publishes that frame on the bus, and the rule engine evaluates it once, at the observed time.
        var ingested = _samples.Ingest(new ManualSample(
            reading.ToolId,
            reading.ChamberId,
            catalogParameter,
            reading.Unit,
            reading.ObservedAt,
            reading.Value,
            evidence.Id.Value));

        return new PrintoutConfirmation(
            evidence.Id.Value,
            null,
            null,
            correlationId,
            null,
            ingested,
            reading.OutOfSpec,
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
                EventTypes.DocumentUpload,
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
                EventTypes.DocumentUploaded,
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

    private static string Summary(PrintoutReading reading, string catalogParameter)
    {
        var applied = catalogParameter.Equals(reading.Parameter, StringComparison.Ordinal)
            ? string.Empty
            : " Applied to " + Mps.Application.Metadata.ParameterNames.Display(catalogParameter) + ".";
        return "Manual measurement: " + reading.Instrument + " on " + reading.ToolId + ", "
            + Mps.Application.Metadata.ParameterNames.Display(reading.Parameter) + " " + Num(reading.Value) + " " + reading.Unit
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
