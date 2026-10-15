// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.Extensions.AI;

using Mps.Application.Printouts;

namespace Mps.Api.Printouts;

/// <summary>
/// Asks the model gateway to read a scan. Mock and replay modes answer from the sample manifest.
/// </summary>
public sealed class PrintoutModelReader : IPrintoutModelReader
{
    public const string SystemPrompt =
        "You are a printout reader. Read the bench instrument printout image and return one JSON object only, no prose, no code fence, and do not call tools. "
        + "Use exactly these keys: toolId (e.g. \"E-01\"), chamberId (e.g. \"E-01-A\" or null), observedAt (ISO 8601 UTC), "
        + "instrument (one of oscilloscope, rfPowerMeter, particleCounter, thermocoupleLogger), parameter (camelCase id such as rfPower, rfBiasPower, particlesAdders, escTemperature), "
        + "value (number), unit (as printed, e.g. W, C, count), specMinimum (number), specMaximum (number), shape (oscillation, step, ramp or null), operatorInitials (string or null). "
        + "Printed labels may be upper case; return ids in the camelCase forms listed. If the sheet cannot be read, return {\"error\": \"<reason>\"}.";

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly IChatClient _chat;

    public PrintoutModelReader(IChatClient chat)
    {
        ArgumentNullException.ThrowIfNull(chat);
        _chat = chat;
    }

    public async Task<PrintoutReading?> ReadAsync(
        ReadOnlyMemory<byte> bytes,
        string? mediaType,
        string? fileName,
        CancellationToken cancellationToken)
    {
        var hash = Convert.ToHexString(SHA256.HashData(bytes.Span)).ToLowerInvariant();
        var type = string.IsNullOrWhiteSpace(mediaType) ? "application/octet-stream" : mediaType.Trim();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, [
                new TextContent("file: " + (fileName ?? "printout") + "\nsha256: " + hash + "\nmediaType: " + type),
                new DataContent(bytes, type),
            ]),
        };
        var response = await _chat.GetResponseAsync(messages, cancellationToken: cancellationToken).ConfigureAwait(false);
        return Parse(response.Text);
    }

    internal static PrintoutReading? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var body = text.Trim();
        if (body.StartsWith("```", StringComparison.Ordinal))
        {
            var start = body.IndexOf('\n');
            var end = body.LastIndexOf("```", StringComparison.Ordinal);
            if (start >= 0 && end > start)
            {
                body = body[(start + 1)..end].Trim();
            }
        }

        try
        {
            var dto = JsonSerializer.Deserialize<ModelPrintout>(body, Json);
            if (dto is null || !string.IsNullOrWhiteSpace(dto.Error) || dto.ToolId is null || dto.Parameter is null || dto.Unit is null || dto.Instrument is null || dto.Value is null || dto.SpecMinimum is null || dto.SpecMaximum is null || dto.ObservedAt is null)
            {
                return null;
            }

            return new PrintoutReading(
                dto.ToolId,
                dto.ChamberId,
                dto.ObservedAt.Value,
                dto.Instrument,
                dto.Parameter,
                dto.Value.Value,
                dto.Unit,
                dto.SpecMinimum.Value,
                dto.SpecMaximum.Value,
                dto.Shape,
                dto.OperatorInitials);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private sealed class ModelPrintout
    {
        public string? ToolId { get; set; }

        public string? ChamberId { get; set; }

        public DateTimeOffset? ObservedAt { get; set; }

        public string? Instrument { get; set; }

        public string? Parameter { get; set; }

        public double? Value { get; set; }

        public string? Unit { get; set; }

        public double? SpecMinimum { get; set; }

        public double? SpecMaximum { get; set; }

        public string? Shape { get; set; }

        public string? OperatorInitials { get; set; }

        public string? Error { get; set; }
    }
}
