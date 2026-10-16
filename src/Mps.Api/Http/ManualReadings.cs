// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

using Mps.Api.Live;
using Mps.Application.Printouts;

namespace Mps.Api.Http;

/// <summary>Shapes confirmed bench readings for the schematic tile, the SPC chart marker, the alert source, and linked records.</summary>
public static class ManualReadings
{
    public static ManualReadingResponse ToResponse(ManualMeasurement item, PlantSimulation plant)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(plant);
        var reading = item.Reading ?? throw new ArgumentException("The measurement has no reading.", nameof(item));
        var catalog = PrintoutReading.CatalogParameter(reading.ToolId, reading.Parameter);
        return new ManualReadingResponse(
            item.Evidence.Id.Value,
            item.IncidentId,
            item.AlertId,
            reading.ToolId,
            LiveResponses.SiteOf(plant, reading.ToolId),
            reading.ChamberId,
            catalog,
            reading.Instrument,
            reading.Value,
            reading.Unit,
            reading.SpecMinimum,
            reading.SpecMaximum,
            reading.OutOfSpec,
            reading.ObservedAt,
            reading.CorrectedFrom,
            OlderThanWindow(plant, reading.ToolId, reading.ChamberId, catalog, reading.ObservedAt),
            reading.OperatorInitials,
            item.FileName,
            item.FileId is null ? null : $"/api/manual-readings/{item.Evidence.Id.Value}/file",
            Source(reading, item.FileName));
    }

    /// <summary>True when the observed time is before the oldest point the chart window holds for that series.</summary>
    public static bool OlderThanWindow(PlantSimulation plant, string toolId, string? chamberId, string parameter, DateTimeOffset observedAt)
    {
        ArgumentNullException.ThrowIfNull(plant);
        var points = plant.Log.Window(toolId, chamberId ?? string.Empty, parameter);
        return points.Count > 0 && observedAt < points[0].At;
    }

    public static string Source(PrintoutReading reading, string? fileName)
    {
        ArgumentNullException.ThrowIfNull(reading);
        var sheet = string.IsNullOrWhiteSpace(fileName) ? "printout" : "printout " + fileName;
        var who = reading.OperatorInitials is null ? string.Empty : ", operator " + reading.OperatorInitials;
        return "Manual reading from the " + Words(reading.Instrument) + " " + sheet + who + ": "
            + reading.Value.ToString("0.###", CultureInfo.InvariantCulture) + " " + reading.Unit
            + " (spec " + reading.SpecMinimum.ToString("0.###", CultureInfo.InvariantCulture) + "-"
            + reading.SpecMaximum.ToString("0.###", CultureInfo.InvariantCulture) + ")"
            + (reading.OutOfSpec ? ", out of spec." : ", in spec.");
    }

    private static string Words(string instrument)
    {
        var chars = new List<char>();
        foreach (var ch in instrument)
        {
            if (char.IsUpper(ch) && chars.Count > 0)
            {
                chars.Add(' ');
            }

            chars.Add(char.ToLowerInvariant(ch));
        }

        return new string(chars.ToArray());
    }
}
