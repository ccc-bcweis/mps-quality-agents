// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Bus;
using Mps.Api.Live;
using Mps.Application.Errors;
using Mps.Application.Persistence;
using Mps.Application.Printouts;
using Mps.Domain.Events;

namespace Mps.Api.Http;

public static partial class QualityEndpoints
{
    /// <summary>Printout inspect/confirm and manual readings.</summary>
    private static void MapPrintoutsAndManualReadings(WebApplication app)
    {
        app.MapPost("/api/printouts/inspect", async (HttpRequest request, IDocumentIntake intake, Mps.Api.Printouts.PrintoutFileStore files, CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
            {
                throw new IntakeValidationException("Upload a PDF or an image.");
            }

            var form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
            {
                throw new IntakeValidationException("A printout file is required.");
            }

            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            var bytes = buffer.ToArray();
            var inspection = await intake.InspectAsync(bytes, file.ContentType, file.FileName, cancellationToken).ConfigureAwait(false);
            var reading = inspection.Reading;
            var fileId = files.Add(bytes, file.ContentType, file.FileName);
            return Results.Ok(new PrintoutInspectionResponse(
                reading.ToolId,
                reading.ChamberId,
                reading.ObservedAt,
                reading.Instrument,
                reading.Parameter,
                reading.Value,
                reading.Unit,
                reading.SpecMinimum,
                reading.SpecMaximum,
                reading.Shape,
                reading.OperatorInitials,
                inspection.ParseMethod,
                reading.OutOfSpec,
                fileId));
        });
        app.MapPost("/api/printouts/confirm", async (
            PrintoutConfirmBody? body,
            IDocumentIntake intake,
            Mps.Api.Printouts.PrintoutFileStore files,
            CommandGateway commands,
            HttpContext http,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            if (body is null)
            {
                throw new IntakeValidationException("Printout body is required.");
            }

            commands.Ensure(EventTypes.DocumentUpload, Caller(http, configuration));

            if (body.ObservedAt is null || body.Value is null || body.SpecMinimum is null || body.SpecMaximum is null)
            {
                throw new IntakeValidationException("Observed time, value, and spec limits are required.");
            }

            PrintoutReading reading;
            try
            {
                reading = new PrintoutReading(
                    body.ToolId ?? string.Empty,
                    body.ChamberId,
                    body.ObservedAt.Value,
                    body.Instrument ?? string.Empty,
                    body.Parameter ?? string.Empty,
                    body.Value.Value,
                    body.Unit ?? string.Empty,
                    body.SpecMinimum.Value,
                    body.SpecMaximum.Value,
                    body.Shape,
                    body.OperatorInitials)
                {
                    PrintedObservedAt = body.PrintedObservedAt,
                };
            }
            catch (ArgumentException exception)
            {
                throw new IntakeValidationException(exception.Message);
            }

            var stored = body.FileId is { } id ? files.Find(id) : null;
            var confirmed = await intake.ConfirmAsync(reading, stored is null ? null : body.FileId, stored?.FileName, cancellationToken).ConfigureAwait(false);
            return Results.Ok(confirmed);
        });
        app.MapGet("/api/manual-readings", async (string? toolId, string? siteId, IManualMeasurementLog measurements, PlantSimulation plant, IAlertLog alerts, IIncidentLog incidents, CancellationToken cancellationToken) =>
        {
            var alertRows = await alerts.ListAsync(null, cancellationToken).ConfigureAwait(false);
            var incidentRows = await incidents.ListAsync(cancellationToken).ConfigureAwait(false);
            var rows = (string.IsNullOrWhiteSpace(toolId) ? measurements.All() : measurements.ForTool(toolId.Trim()))
                .Where(item => item.Reading is not null)
                .Select(item => ManualReadings.ToResponse(item, plant, alertRows, incidentRows))
                .Where(row => MatchesSite(siteId, row.SiteId))
                .OrderByDescending(row => row.ObservedAt)
                .ToArray();
            return Results.Ok(rows);
        });
        app.MapGet("/api/manual-readings/{evidenceId:guid}/file", (Guid evidenceId, HttpContext http, IManualMeasurementLog measurements, Mps.Api.Printouts.PrintoutFileStore files) =>
        {
            var item = measurements.All().FirstOrDefault(row => row.Evidence.Id.Value == evidenceId);
            var file = item?.FileId is { } id ? files.Find(id) : null;
            if (file is null)
            {
                return Results.NotFound();
            }

            // Shown in the console's viewer and in a new tab, so PDFs and images go inline with the type read from the bytes.
            var contentType = Mps.Api.Printouts.PrintoutContentType.Detect(file.Bytes);
            var disposition = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue(
                Mps.Api.Printouts.PrintoutContentType.ShowsInline(contentType) ? "inline" : "attachment");
            disposition.SetHttpFileName(file.FileName);
            http.Response.Headers.ContentDisposition = disposition.ToString();
            http.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(file.Bytes, contentType);
        });
    }
}
