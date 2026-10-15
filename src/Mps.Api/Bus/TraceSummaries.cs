// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Text.Json;

using Mps.Api.Http;

namespace Mps.Api.Bus;

internal static class TraceSummaries
{
    public static TraceNodeResponse Node(
        StoredEvent row,
        string? toolName,
        StoredEvent? cause,
        IReadOnlyList<TraceNodeResponse> children,
        bool truncated)
    {
        var (value, low, high) = Reading(row, cause);
        return new TraceNodeResponse(
            row.EventId,
            row.Type,
            Describe(row, toolName, value, low, high),
            row.OccurredAt,
            row.ToolId,
            string.IsNullOrWhiteSpace(toolName) ? null : toolName,
            value,
            low,
            high,
            children,
            truncated);
    }

    private static string Describe(StoredEvent row, string? toolName, double? value, double? low, double? high)
    {
        var tool = string.IsNullOrWhiteSpace(toolName) ? row.ToolId : toolName;
        var parameter = Parameter(row);
        var when = row.OccurredAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
        var where = string.IsNullOrWhiteSpace(tool) ? "the plant" : tool;
        var reading = value is null
            ? string.Empty
            : " The value was " + Num(value.Value) + Limit(low, high) + ".";
        var text = row.Type switch
        {
            "telemetry.reading" => where + " recorded " + (parameter ?? "a reading") + " at " + when + "." + reading,
            "alert.raised" => "A process-control rule fired on " + where
                + (parameter is null ? string.Empty : " for " + parameter) + " at " + when + "." + reading,
            "ticket.created" => "A ticket was opened for " + where + " at " + when + ".",
            "incident.changed" => "The incident on " + where + " was updated at " + when + ".",
            "investigation.step" => Step(row, where, when),
            "investigation.changed" => "The investigation for " + where + " was updated at " + when + ".",
            "approval.granted" => "A person approved the recommendation at " + when + ".",
            "capa.created" => "A corrective action was opened at " + when + ".",
            "capa.advanced" => "The corrective action moved forward at " + when + ".",
            "document.uploaded" => "A bench reading was stored for " + where + " at " + when + "." + reading,
            "command.fault.inject" => "A fault was injected on " + where
                + (parameter is null ? string.Empty : " for " + parameter) + " at " + when + ".",
            "command.scenario.start" => "A scripted scenario started at " + when + ".",
            "command.reset.hard" => "The demo was reset to a clean plant at " + when + ".",
            "command.reset.soft" => "The live faults were cleared and the tickets were kept at " + when + ".",
            "command.operator.report" => "An operator reported " + where + " at " + when + ".",
            "command.document.upload" => "An operator uploaded a bench reading for " + where + " at " + when + ".",
            "command.approval.grant" => "An approver submitted the recommendation at " + when + ".",
            "command.capa.advance" => "Someone advanced the corrective action at " + when + ".",
            "command.investigation.start" => "An investigation was started for " + where + " at " + when + ".",
            _ => "An event of type " + row.Type + " was recorded at " + when + ".",
        };
        return text;
    }

    private static string Step(StoredEvent row, string where, string when)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(row.PayloadJson) ? "{}" : row.PayloadJson);
        var kind = PayloadReader.String(document.RootElement, "kind");
        return kind switch
        {
            "evidence" => "Evidence was attached for " + where + " at " + when + ".",
            "recommendation" => "A recommendation was ready for " + where + " at " + when + ".",
            _ => "An investigation step was recorded for " + where + " at " + when + ".",
        };
    }

    private static (double? Value, double? Low, double? High) Reading(StoredEvent row, StoredEvent? cause)
    {
        if (row.FromTelemetry)
        {
            return (row.Value, row.LimitLow, row.LimitHigh);
        }

        if (cause is { FromTelemetry: true })
        {
            return (cause.Value, cause.LimitLow, cause.LimitHigh);
        }

        if (string.IsNullOrWhiteSpace(row.PayloadJson))
        {
            return (null, null, null);
        }

        using var document = JsonDocument.Parse(row.PayloadJson);
        return (
            PayloadReader.Number(document.RootElement, "value"),
            PayloadReader.Number(document.RootElement, "low"),
            PayloadReader.Number(document.RootElement, "high"));
    }

    private static string? Parameter(StoredEvent row)
    {
        if (!string.IsNullOrWhiteSpace(row.Parameter))
        {
            return row.Parameter;
        }

        if (string.IsNullOrWhiteSpace(row.PayloadJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(row.PayloadJson);
        return PayloadReader.String(document.RootElement, "parameter");
    }

    private static string Limit(double? low, double? high)
    {
        if (low is null && high is null)
        {
            return string.Empty;
        }

        if (low is not null && high is not null)
        {
            return " against a limit of " + Num(low.Value) + " to " + Num(high.Value);
        }

        return low is null
            ? " against an upper limit of " + Num(high!.Value)
            : " against a lower limit of " + Num(low.Value);
    }

    private static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
