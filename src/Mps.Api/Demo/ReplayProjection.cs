// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Mps.Api.Bus;
using Mps.Api.Http;
using Mps.Application.Bus;
using Mps.Bus;
using Mps.Domain.Events;

namespace Mps.Api.Demo;

/// <summary>Turns a recorded run into the fleet, chart, and alert payloads the console already polls.</summary>
public static class ReplayProjection
{
    public static FleetLiveResponse Fleet(IReadOnlyList<ReplayMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var readings = ReadTelemetry(messages);
        var tools = readings
            .GroupBy(reading => reading.ToolId, StringComparer.Ordinal)
            .Select(group =>
            {
                var latest = Latest(group);
                var status = latest.Select(reading => reading.Status).Aggregate("nominal", Worse);
                return new FleetLiveToolResponse(group.Key, group.First().SiteId, status, latest);
            })
            .OrderBy(tool => tool.ToolId, StringComparer.Ordinal)
            .ToArray();
        return new FleetLiveResponse(Now(messages), tools);
    }

    public static ToolLiveResponse? Tool(IReadOnlyList<ReplayMessage> messages, string toolId)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (string.IsNullOrWhiteSpace(toolId))
        {
            return null;
        }

        var readings = ReadTelemetry(messages)
            .Where(reading => string.Equals(reading.ToolId, toolId.Trim(), StringComparison.Ordinal))
            .ToArray();
        if (readings.Length == 0)
        {
            return null;
        }

        var latest = Latest(readings);
        var status = latest.Select(reading => reading.Status).Aggregate("nominal", Worse);
        var series = readings
            .GroupBy(reading => reading.Parameter + "\u001f" + reading.ChamberId, StringComparer.Ordinal)
            .OrderBy(group => group.First().Parameter, StringComparer.Ordinal)
            .ThenBy(group => group.First().ChamberId, StringComparer.Ordinal)
            .Select(group => new LiveSeriesResponse(
                group.First().Parameter,
                group.First().ChamberId,
                group.OrderBy(reading => reading.At).Select(reading => new LivePointResponse(reading.At, reading.Value)).ToArray()))
            .ToArray();
        return new ToolLiveResponse(toolId.Trim(), readings[0].SiteId, Now(messages), status, latest, series);
    }

    public static AlertResponse[] Alerts(IReadOnlyList<ReplayMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var alerts = new List<AlertResponse>();
        foreach (var message in messages)
        {
            if (!string.Equals(message.Type, EventTypes.AlertRaised, StringComparison.Ordinal)
                || message.Payload.ValueKind != JsonValueKind.Object
                || !message.Payload.TryGetProperty("body", out var body)
                || body.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var alert = JsonSerializer.Deserialize<AlertResponse>(body, EventJson.Options);
            if (alert is not null)
            {
                alerts.Add(alert);
            }
        }

        return [.. alerts];
    }

    private static List<Reading> ReadTelemetry(IReadOnlyList<ReplayMessage> messages)
    {
        var readings = new List<Reading>();
        foreach (var message in messages)
        {
            if (!EventTypes.IsTelemetry(message.Type) || string.IsNullOrWhiteSpace(message.ToolId))
            {
                continue;
            }

            var parameter = PayloadReader.String(message.Payload, "parameter");
            var value = PayloadReader.Number(message.Payload, "value");
            if (parameter is null || value is null)
            {
                continue;
            }

            var low = PayloadReader.Number(message.Payload, "low");
            var high = PayloadReader.Number(message.Payload, "high");
            var toolId = message.ToolId.Trim();
            readings.Add(new Reading(
                toolId,
                message.SiteId ?? ContractMapper.SiteForTool(toolId),
                parameter,
                parameter,
                message.ChamberId ?? string.Empty,
                PayloadReader.String(message.Payload, "unit") ?? string.Empty,
                message.OccurredAt,
                value.Value,
                Status(value.Value, low, high)));
        }

        return readings;
    }

    private static LiveReadingResponse[] Latest(IEnumerable<Reading> readings) =>
        readings
            .GroupBy(reading => reading.Parameter + "\u001f" + reading.ChamberId, StringComparer.Ordinal)
            .Select(group => group.OrderBy(reading => reading.At).Last().ToResponse())
            .OrderBy(reading => reading.Parameter, StringComparer.Ordinal)
            .ThenBy(reading => reading.ChamberId, StringComparer.Ordinal)
            .ToArray();

    private static DateTimeOffset Now(IReadOnlyList<ReplayMessage> messages) =>
        messages.Count == 0 ? DateTimeOffset.UnixEpoch : messages.Max(message => message.OccurredAt);

    private static string Status(double value, double? low, double? high)
    {
        if (low is null || high is null || !double.IsFinite(value))
        {
            return "nominal";
        }

        return value < low.Value || value > high.Value ? "alarm" : "nominal";
    }

    private static string Worse(string left, string right) => Rank(left) >= Rank(right) ? left : right;

    private static int Rank(string status) => status switch
    {
        "alarm" => 2,
        "watch" => 1,
        _ => 0,
    };

    private sealed record Reading(
        string ToolId,
        string SiteId,
        string SensorId,
        string Parameter,
        string ChamberId,
        string Unit,
        DateTimeOffset At,
        double Value,
        string Status)
    {
        public LiveReadingResponse ToResponse() => new(SensorId, Parameter, ChamberId, Unit, At, Value, Status);
    }
}
