// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Evidence;
using Mps.Agents.Impact;
using Mps.Application.Spc;
using Mps.Domain.Incidents;
using Mps.Domain.Telemetry;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Agents.Investigation;

/// <summary>How long to wait for enough readings to classify the signal shape. From data/metadata/investigation.yaml.</summary>
public sealed record ShapeWait(int Attempts, TimeSpan Delay, int MinReadingsSinceOnset = 0)
{
    public static ShapeWait None { get; } = new(0, TimeSpan.Zero);

    /// <summary>True while the shape is unclear or fewer than the configured readings have arrived since the first rule break.</summary>
    public bool NeedsMoreReadings(TelemetryWindow? window, IReadOnlyList<SpcViolation> violations, DateTimeOffset? openedAt = null)
    {
        ArgumentNullException.ThrowIfNull(violations);
        if (window is null)
        {
            return false;
        }

        if (EvidencePackFactory.ShapeOf(window, violations) == SignalShape.Unknown)
        {
            return true;
        }

        if (MinReadingsSinceOnset <= 0 || (violations.Count == 0 && openedAt is null))
        {
            return false;
        }

        // Onset is when the alert opened the incident. The rule-break list only keeps each rule's latest break, and at
        // high plant speed those move to "now" every second, so counting from them would never reach the minimum.
        // Without an incident time, use the first serious break (a low-severity trend rule can fire on plain noise).
        var serious = violations.Where(violation => violation.Severity >= Severity.High).ToArray();
        var onset = openedAt ?? (serious.Length > 0 ? serious : violations).Min(violation => violation.At);
        return window.Points.Count(point => point.At >= onset) < MinReadingsSinceOnset;
    }

    public static ShapeWait Load(string? path = null)
    {
        path ??= DataFiles.Find(Path.Combine("metadata", "investigation.yaml"));
        if (path is null || !File.Exists(path))
        {
            return None;
        }

        var file = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build()
            .Deserialize<FileShape>(File.ReadAllText(path));
        var wait = file?.ShapeWait;
        return wait is null || wait.Attempts <= 0 || wait.DelaySeconds <= 0
            ? None
            : new ShapeWait(wait.Attempts, TimeSpan.FromSeconds(wait.DelaySeconds), Math.Max(0, wait.MinReadingsSinceOnset));
    }

    private sealed class FileShape
    {
        public WaitShape? ShapeWait { get; set; }
    }

    private sealed class WaitShape
    {
        public int Attempts { get; set; }

        public double DelaySeconds { get; set; }

        public int MinReadingsSinceOnset { get; set; }
    }
}
