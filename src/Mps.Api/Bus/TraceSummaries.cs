// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Text.Json;

using Mps.Api.Http;
using Mps.Simulator.Scenarios;

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
        var parameter = PlainParameter(Parameter(row));
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
            "rule.matched" => RuleSentence(row, when),
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
            "command.scenario.start" => ScenarioSentence(row, when),
            "command.reset.hard" => "The plant was reset to a clean state at " + when + ".",
            "command.reset.soft" => "Open tickets were closed by reset and the history was kept at " + when + ".",
            "command.operator.report" => "An operator reported " + where + " at " + when + ".",
            "command.document.upload" => "An operator uploaded a bench reading for " + where + " at " + when + ".",
            "command.approval.grant" => "An approver submitted the recommendation at " + when + ".",
            "command.capa.advance" => "Someone advanced the corrective action at " + when + ".",
            "command.investigation.start" => "An investigation was started for " + where + " at " + when + ".",
            _ => "An event of type " + row.Type + " was recorded at " + when + ".",
        };
        return text;
    }

    private static string RuleSentence(StoredEvent row, string when)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(row.PayloadJson) ? "{}" : row.PayloadJson);
        var name = PayloadReader.String(document.RootElement, "name") ?? "that matched";
        var queue = PayloadReader.String(document.RootElement, "queue");
        var why = PayloadReader.String(document.RootElement, "why");
        var opened = string.IsNullOrWhiteSpace(queue)
            ? " It opened a ticket."
            : " It opened a ticket for " + queue.Trim().TrimEnd('.') + ".";
        var because = string.IsNullOrWhiteSpace(why) ? string.Empty : " " + why.Trim();
        return "The business rule " + name + " fired at " + when + "." + opened + because;
    }

    private static string ScenarioSentence(StoredEvent row, string when)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(row.PayloadJson) ? "{}" : row.PayloadJson);
        var id = PayloadReader.String(document.RootElement, "scenarioId");
        var title = id is not null && ScriptedScenarioCatalog.TryGet(id, out var scenario) ? scenario.Title : null;
        return title is null
            ? "A scripted scenario started at " + when + "."
            : "Scenario " + title + " started at " + when + ".";
    }

    private static string? PlainParameter(string? parameter)
    {
        return parameter switch
        {
            "escTemperature" => "chuck temperature",
            "etchRate" => "etch rate",
            "filmThickness" => "film thickness",
            "rfSourcePower" => "source power",
            "vibrationRms" => "pump vibration",
            "rfBiasPower" => "bias power",
            "cf4Flow" => "etch gas flow",
            _ => parameter,
        };
    }

    private static string Step(StoredEvent row, string where, string when)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(row.PayloadJson) ? "{}" : row.PayloadJson);
        var kind = PayloadReader.String(document.RootElement, "kind");
        var title = PayloadReader.String(document.RootElement, "title");
        if (kind == "evidence" && !string.IsNullOrWhiteSpace(title))
        {
            return "The investigation read \"" + title + "\" for " + where + " at " + when + ".";
        }

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
