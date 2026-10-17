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
    public bool NeedsMoreReadings(TelemetryWindow? window, IReadOnlyList<SpcViolation> violations)
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

        if (MinReadingsSinceOnset <= 0 || violations.Count == 0)
        {
            return false;
        }

        // Onset is the first serious break: a low-severity trend rule can fire on ordinary noise long before the fault.
        var serious = violations.Where(violation => violation.Severity >= Severity.High).ToArray();
        var onset = (serious.Length > 0 ? serious : violations).Min(violation => violation.At);
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
