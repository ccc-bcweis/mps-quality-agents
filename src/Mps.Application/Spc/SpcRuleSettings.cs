// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mps.Application.Spc;

/// <summary>Control-chart thresholds from data/metadata/spc-rules.yaml, loaded once at startup. No threshold lives in code.</summary>
public sealed class SpcRuleSettings
{
    private static readonly Lazy<SpcRuleSettings> SharedSettings = new(() => Load(DataFiles.Find(Path.Combine("metadata", "spc-rules.yaml"))));

    public static SpcRuleSettings Shared => SharedSettings.Value;

    public double ControlLimitSigma { get; set; }

    public double WatchSigma { get; set; }

    public double CriticalSigma { get; set; }

    public WesternElectricSettings WesternElectric { get; set; } = new();

    public RunSetting NelsonRising { get; set; } = new();

    public LimitCheckSettings LimitCheck { get; set; } = new();

    public ClearingSettings Clearing { get; set; } = new();

    public double AlertDebounceMinutes { get; set; }

    public double RecoveryWatchMinutes { get; set; }

    public TimeSpan AlertDebounce => TimeSpan.FromMinutes(AlertDebounceMinutes);

    public TimeSpan RecoveryWatch => TimeSpan.FromMinutes(RecoveryWatchMinutes);

    public (double Low, double High) ControlLimits(double center, double sigma) =>
        (center - (ControlLimitSigma * sigma), center + (ControlLimitSigma * sigma));

    public static SpcRuleSettings Load(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            throw new InvalidOperationException("data/metadata/spc-rules.yaml was not found.");
        }

        var settings = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build()
            .Deserialize<SpcRuleSettings>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("spc-rules.yaml is empty.");
        if (settings.ControlLimitSigma <= 0 || settings.WatchSigma <= 0 || settings.WesternElectric.Rule1.Sigma <= 0
            || settings.WesternElectric.Rule4.Count <= 0 || settings.NelsonRising.Count <= 0
            || settings.LimitCheck.RecentReadings <= 0 || settings.Clearing.Readings <= 0 || settings.Clearing.Sigma <= 0
            || settings.AlertDebounceMinutes <= 0 || settings.RecoveryWatchMinutes <= 0)
        {
            throw new InvalidOperationException("spc-rules.yaml is missing a threshold.");
        }

        return settings;
    }

    public sealed class WesternElectricSettings
    {
        public ZoneSetting Rule1 { get; set; } = new();

        public ZoneSetting Rule2 { get; set; } = new();

        public ZoneSetting Rule3 { get; set; } = new();

        public RunSetting Rule4 { get; set; } = new();
    }

    public sealed class ZoneSetting
    {
        public double Sigma { get; set; }

        public int Count { get; set; }

        public int Window { get; set; }
    }

    public sealed class RunSetting
    {
        public int Count { get; set; }
    }

    /// <summary>The limit check every live series gets: how many recent readings it looks at.</summary>
    public sealed class LimitCheckSettings
    {
        public int RecentReadings { get; set; }
    }

    /// <summary>When an alert clears on its own: this many readings in a row inside plus or minus this many sigma.</summary>
    public sealed class ClearingSettings
    {
        public int Readings { get; set; }

        public double Sigma { get; set; }
    }
}
