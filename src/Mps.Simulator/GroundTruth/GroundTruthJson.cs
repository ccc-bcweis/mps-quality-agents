// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mps.Simulator.GroundTruth;

public static class GroundTruthJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static void Write(GroundTruthDocument document, string path)
    {
        ArgumentNullException.ThrowIfNull(document);
        var json = JsonSerializer.Serialize(ToDto(document), Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        File.WriteAllText(path, json);
    }

    public static GroundTruthDocument Read(string path)
    {
        var dto = JsonSerializer.Deserialize<GroundTruthDto>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException($"Empty ground truth file {path}.");
        return new GroundTruthDocument(
            dto.FaultId ?? string.Empty,
            dto.Scenario ?? string.Empty,
            dto.Category ?? string.Empty,
            dto.ExpectedBehavior ?? string.Empty,
            dto.AssertCategory,
            dto.Cause ?? string.Empty,
            dto.Tools ?? [],
            dto.Parameter ?? string.Empty,
            dto.ChamberId,
            dto.EvidenceRecordIds ?? [],
            dto.ConfounderRecordIds ?? [],
            dto.MissingRecordIds ?? [],
            dto.Contradiction,
            dto.CounterEvidence ?? [],
            (dto.Signals ?? []).Select(signal => new Telemetry.FaultSignal(
                signal.ToolId ?? string.Empty,
                signal.ChamberId,
                signal.Parameter ?? string.Empty,
                Enum.Parse<Telemetry.FaultShape>(signal.Shape ?? string.Empty, ignoreCase: true),
                Enum.Parse<Telemetry.MagnitudeKind>(signal.MagnitudeKind ?? string.Empty, ignoreCase: true),
                signal.Magnitude,
                DateTimeOffset.Parse(signal.Onset ?? throw new InvalidDataException("A ground-truth signal is missing its onset."), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
                signal.PeriodSeconds,
                signal.RampSeconds,
                signal.DutyCycle)).ToArray());
    }

    private static GroundTruthDto ToDto(GroundTruthDocument document)
    {
        return new GroundTruthDto
        {
            FaultId = document.FaultId,
            Scenario = document.Scenario,
            Category = document.Category,
            ExpectedBehavior = document.ExpectedBehavior,
            AssertCategory = document.AssertCategory,
            Cause = document.Cause,
            Tools = document.Tools.ToArray(),
            Parameter = document.Parameter,
            ChamberId = document.ChamberId,
            EvidenceRecordIds = document.EvidenceRecordIds.ToArray(),
            ConfounderRecordIds = document.ConfounderRecordIds.ToArray(),
            MissingRecordIds = document.MissingRecordIds.ToArray(),
            Contradiction = document.Contradiction,
            CounterEvidence = document.CounterEvidence.ToArray(),
            Signals = document.Signals.Select(signal => new SignalDto
            {
                ToolId = signal.ToolId,
                ChamberId = signal.ChamberId,
                Parameter = signal.Parameter,
                Shape = Camel(signal.Shape.ToString()),
                MagnitudeKind = Camel(signal.MagnitudeKind.ToString()),
                Magnitude = signal.Magnitude,
                Onset = signal.Onset.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                PeriodSeconds = signal.PeriodSeconds,
                RampSeconds = signal.RampSeconds,
                DutyCycle = signal.DutyCycle,
            }).ToArray(),
        };
    }

    private static string Camel(string pascal)
    {
        return char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }

    private sealed class GroundTruthDto
    {
        public string? FaultId { get; set; }

        public string? Scenario { get; set; }

        public string? Category { get; set; }

        public string? ExpectedBehavior { get; set; }

        public bool AssertCategory { get; set; }

        public string? Cause { get; set; }

        public string[]? Tools { get; set; }

        public string? Parameter { get; set; }

        public string? ChamberId { get; set; }

        public string[]? EvidenceRecordIds { get; set; }

        public string[]? ConfounderRecordIds { get; set; }

        public string[]? MissingRecordIds { get; set; }

        public string? Contradiction { get; set; }

        public string[]? CounterEvidence { get; set; }

        public SignalDto[]? Signals { get; set; }
    }

    private sealed class SignalDto
    {
        public string? ToolId { get; set; }

        public string? ChamberId { get; set; }

        public string? Parameter { get; set; }

        public string? Shape { get; set; }

        public string? MagnitudeKind { get; set; }

        public double Magnitude { get; set; }

        public string? Onset { get; set; }

        public double PeriodSeconds { get; set; }

        public double RampSeconds { get; set; }

        public double DutyCycle { get; set; }
    }
}
