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

    public RunSetting NelsonFalling { get; set; } = new();

    public SpecWindowSettings SpecWindow { get; set; } = new();

    public LimitCheckSettings LimitCheck { get; set; } = new();

    public ClearingSettings Clearing { get; set; } = new();

    public double AlertDebounceMinutes { get; set; }

    public double RecoveryWatchMinutes { get; set; }

    public WatchExpirySettings WatchExpiry { get; set; } = new();

    public TicketCapSettings TicketCap { get; set; } = new();

    public SensorSettings Sensors { get; set; } = new();

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
        settings.Validate();
        return settings;
    }

    /// <summary>A deep copy so a test can change one knob without touching the shared file.</summary>
    public SpcRuleSettings Copy() =>
        new()
        {
            ControlLimitSigma = ControlLimitSigma,
            WatchSigma = WatchSigma,
            CriticalSigma = CriticalSigma,
            WesternElectric = new WesternElectricSettings
            {
                Rule1 = WesternElectric.Rule1.Copy(),
                Rule2 = WesternElectric.Rule2.Copy(),
                Rule3 = WesternElectric.Rule3.Copy(),
                Rule4 = WesternElectric.Rule4.Copy(),
            },
            NelsonRising = NelsonRising.Copy(),
            NelsonFalling = NelsonFalling.Copy(),
            SpecWindow = new SpecWindowSettings { Tier = SpecWindow.Tier },
            LimitCheck = new LimitCheckSettings { RecentReadings = LimitCheck.RecentReadings },
            Clearing = new ClearingSettings { Readings = Clearing.Readings, Sigma = Clearing.Sigma },
            AlertDebounceMinutes = AlertDebounceMinutes,
            RecoveryWatchMinutes = RecoveryWatchMinutes,
            WatchExpiry = new WatchExpirySettings { Readings = WatchExpiry.Readings, Minutes = WatchExpiry.Minutes },
            TicketCap = new TicketCapSettings { PerToolPerHour = TicketCap.PerToolPerHour },
            Sensors = new SensorSettings
            {
                EnabledKinds = [.. Sensors.EnabledKinds],
                DisabledParameters = [.. Sensors.DisabledParameters],
            },
        };

    private void Validate()
    {
        Require(ControlLimitSigma > 0, "controlLimitSigma");
        Require(WatchSigma > 0, "watchSigma");
        Require(CriticalSigma > 0, "criticalSigma");
        RequireZone(WesternElectric.Rule1, "westernElectric.rule1");
        RequireZone(WesternElectric.Rule2, "westernElectric.rule2");
        RequireZone(WesternElectric.Rule3, "westernElectric.rule3");
        RequireRun(WesternElectric.Rule4, "westernElectric.rule4", magnitude: true);
        RequireRun(NelsonRising, "nelsonRising", magnitude: true);
        RequireRun(NelsonFalling, "nelsonFalling", magnitude: true);
        RequireTier(SpecWindow.Tier, "specWindow.tier");
        Require(LimitCheck.RecentReadings > 0, "limitCheck.recentReadings");
        Require(Clearing.Readings > 0, "clearing.readings");
        Require(Clearing.Sigma > 0, "clearing.sigma");
        Require(AlertDebounceMinutes > 0, "alertDebounceMinutes");
        Require(RecoveryWatchMinutes > 0, "recoveryWatchMinutes");
        Require(WatchExpiry.Readings > 0, "watchExpiry.readings");
        Require(WatchExpiry.Minutes > 0, "watchExpiry.minutes");
        Require(TicketCap.PerToolPerHour > 0, "ticketCap.perToolPerHour");
        if (Sensors.EnabledKinds is null || Sensors.EnabledKinds.Count == 0 || Sensors.EnabledKinds.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("spc-rules.yaml: sensors.enabledKinds must list at least one sensor kind.");
        }

        if (Sensors.DisabledParameters is null)
        {
            throw new InvalidOperationException("spc-rules.yaml: sensors.disabledParameters is missing.");
        }
    }

    private static void RequireZone(ZoneSetting zone, string field)
    {
        Require(zone.Sigma > 0, field + ".sigma");
        Require(zone.Count > 0, field + ".count");
        Require(zone.Window > 0, field + ".window");
        Require(zone.Count <= zone.Window, field + ".count");
        RequireTier(zone.Tier, field + ".tier");
    }

    private static void RequireRun(RunSetting run, string field, bool magnitude)
    {
        Require(run.Count > 0, field + ".count");
        RequireTier(run.Tier, field + ".tier");
        if (magnitude)
        {
            Require(run.MinMagnitudeSigma > 0, field + ".minMagnitudeSigma");
        }
    }

    private static void RequireTier(string? tier, string field)
    {
        if (!string.Equals(tier, "watch", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(tier, "alert", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("spc-rules.yaml: " + field + " must be watch or alert.");
        }
    }

    private static void Require(bool ok, string field)
    {
        if (!ok)
        {
            throw new InvalidOperationException("spc-rules.yaml: " + field + " must be greater than zero.");
        }
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

        public string Tier { get; set; } = string.Empty;

        public ZoneSetting Copy() => new() { Sigma = Sigma, Count = Count, Window = Window, Tier = Tier };
    }

    public sealed class RunSetting
    {
        public int Count { get; set; }

        public string Tier { get; set; } = string.Empty;

        /// <summary>Mean |z| over the run. Zero turns the gate off (tests). The shipped file requires a positive value.</summary>
        public double MinMagnitudeSigma { get; set; }

        public RunSetting Copy() => new() { Count = Count, Tier = Tier, MinMagnitudeSigma = MinMagnitudeSigma };
    }

    public sealed class SpecWindowSettings
    {
        public string Tier { get; set; } = string.Empty;
    }

    /// <summary>Catalog figure. The engine does not use it as a pre-filter.</summary>
    public sealed class LimitCheckSettings
    {
        public int RecentReadings { get; set; }
    }

    /// <summary>When an alert clears on its own: this many readings in a row inside plus or minus this many sigma, and no live rule.</summary>
    public sealed class ClearingSettings
    {
        public int Readings { get; set; }

        public double Sigma { get; set; }
    }

    public sealed class WatchExpirySettings
    {
        public int Readings { get; set; }

        public double Minutes { get; set; }
    }

    public sealed class TicketCapSettings
    {
        public int PerToolPerHour { get; set; }
    }

    public sealed class SensorSettings
    {
        public List<string> EnabledKinds { get; set; } = [];

        public List<string> DisabledParameters { get; set; } = [];
    }
}
