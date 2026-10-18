// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Incidents;

namespace Mps.Application.Spc;

/// <summary>
/// Incremental Western Electric and Nelson rules, one series per tool, chamber, and parameter.
/// Each reading updates fixed-size counters, so the cost does not grow with history.
/// The alert store is touched only when <see cref="SpcObservation.Action"/> is not <see cref="SpcAction.None"/>.
/// </summary>
public sealed class SpcRuleEngine
{
    private readonly SpcRuleSettings _settings;
    private readonly int _capacity;
    private readonly object _gate = new();
    private readonly Dictionary<string, Series> _series = new(StringComparer.Ordinal);

    public SpcRuleEngine(SpcRuleSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        var we = settings.WesternElectric;
        _capacity = Math.Max(1, Math.Max(we.Rule2.Window, Math.Max(we.Rule3.Window, Math.Max(we.Rule4.Count, Math.Max(settings.NelsonRising.Count, settings.NelsonFalling.Count)))));
    }

    public SpcRuleSettings Settings => _settings;

    public bool Evaluates(string? parameter, string? kind)
    {
        if (string.IsNullOrWhiteSpace(parameter))
        {
            return false;
        }

        foreach (var disabled in _settings.Sensors.DisabledParameters)
        {
            if (string.Equals(disabled, parameter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var resolved = string.IsNullOrWhiteSpace(kind) ? "periodic" : kind.Trim();
        foreach (var enabled in _settings.Sensors.EnabledKinds)
        {
            if (string.Equals(enabled, resolved, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public SpcObservation? Observe(SpcReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);
        if (!Evaluates(reading.Parameter, reading.Kind) || reading.ToolId.Length == 0 || reading.Parameter.Length == 0)
        {
            return null;
        }

        lock (_gate)
        {
            var key = Key(reading.ToolId, reading.ChamberId, reading.Parameter);
            if (!_series.TryGetValue(key, out var series))
            {
                series = new Series(_capacity);
                _series.Add(key, series);
            }

            return Advance(series, reading);
        }
    }

    /// <summary>Drops a raise the alert store refused (debounce) so the next violation can try again.</summary>
    public void RevertRaise(string toolId, string? chamberId, string parameter)
    {
        lock (_gate)
        {
            if (_series.TryGetValue(Key(toolId, chamberId, parameter), out var series))
            {
                series.Tier = SpcTier.Nominal;
                series.Severity = Severity.Low;
                series.Rules = string.Empty;
                series.CorrelationId = null;
            }
        }
    }

    /// <summary>Replaces every series from the active alerts. Rings start empty.</summary>
    public void Rebuild(IEnumerable<SpcSeriesSeed> seeds)
    {
        ArgumentNullException.ThrowIfNull(seeds);
        lock (_gate)
        {
            _series.Clear();
            foreach (var seed in seeds)
            {
                if (seed.ToolId.Length == 0 || seed.Parameter.Length == 0)
                {
                    continue;
                }

                var key = Key(seed.ToolId, seed.ChamberId, seed.Parameter);
                if (_series.TryGetValue(key, out var existing) && existing.LastViolationAt >= seed.LastSeenAt)
                {
                    continue;
                }

                _series[key] = new Series(_capacity)
                {
                    Tier = seed.Severity >= Severity.High ? SpcTier.Alert : SpcTier.Watch,
                    Severity = seed.Severity,
                    Rules = seed.Rule,
                    CorrelationId = seed.CorrelationId,
                    LastViolationAt = seed.LastSeenAt,
                };
            }
        }
    }

    private SpcObservation Advance(Series series, SpcReading reading)
    {
        var previousTier = series.Tier;
        var previousSeverity = series.Severity;
        series.Push(reading.Z);
        UpdateRuns(series, reading);

        var hits = LiveRules(series, reading);
        var incomingTier = SpcTier.Nominal;
        var incomingSeverity = Severity.Low;
        var ids = new string[hits.Count];
        for (var index = 0; index < hits.Count; index++)
        {
            ids[index] = hits[index].Rule;
            if (hits[index].Tier > incomingTier)
            {
                incomingTier = hits[index].Tier;
            }

            if (hits[index].Severity > incomingSeverity)
            {
                incomingSeverity = hits[index].Severity;
            }
        }

        var joined = string.Join('+', ids);
        if (hits.Count > 0)
        {
            series.Quiet = 0;
            series.LastViolationAt = reading.At;
        }
        else
        {
            series.Quiet++;
        }

        var inside = hits.Count == 0 && Math.Abs(reading.Z) < _settings.Clearing.Sigma;
        series.InControl = inside ? series.InControl + 1 : 0;

        var action = SpcAction.None;
        var clearReason = string.Empty;
        if (hits.Count > 0)
        {
            if (previousTier == SpcTier.Nominal)
            {
                action = SpcAction.Raise;
                series.Tier = incomingTier;
                series.Severity = incomingSeverity;
                series.Rules = joined;
                series.CorrelationId = CorrelationFor(reading.CorrelationId);
            }
            else if (incomingTier > previousTier || incomingSeverity > previousSeverity)
            {
                action = SpcAction.Escalate;
                if (incomingTier > series.Tier)
                {
                    series.Tier = incomingTier;
                }

                if (incomingSeverity > series.Severity)
                {
                    series.Severity = incomingSeverity;
                }

                series.Rules = joined;
            }
            else if (!SameRules(series.Rules, joined))
            {
                action = SpcAction.Update;
                series.Rules = joined;
            }
        }
        else if (previousTier != SpcTier.Nominal && series.InControl >= _settings.Clearing.Readings)
        {
            action = SpcAction.Clear;
            clearReason = "hysteresis";
            series.Tier = SpcTier.Nominal;
            series.Severity = Severity.Low;
            series.Rules = string.Empty;
        }
        else if (previousTier == SpcTier.Watch && WatchExpired(series, reading.At))
        {
            action = SpcAction.Clear;
            clearReason = "expiry";
            series.Tier = SpcTier.Nominal;
            series.Severity = Severity.Low;
            series.Rules = string.Empty;
        }

        return new SpcObservation(
            reading,
            action,
            series.Tier,
            action == SpcAction.Clear ? previousSeverity : series.Severity,
            action == SpcAction.Clear ? string.Empty : series.Rules,
            ids,
            series.CorrelationId ?? string.Empty,
            clearReason,
            series.InControl,
            series.Quiet);
    }

    private bool WatchExpired(Series series, DateTimeOffset at)
    {
        if (series.Quiet >= _settings.WatchExpiry.Readings)
        {
            return true;
        }

        if (series.LastViolationAt == default || at < series.LastViolationAt)
        {
            return false;
        }

        return at - series.LastViolationAt >= TimeSpan.FromMinutes(_settings.WatchExpiry.Minutes);
    }

    private List<Hit> LiveRules(Series series, SpcReading reading)
    {
        var hits = new List<Hit>(7);
        var we = _settings.WesternElectric;
        if (reading.SpecLow is not null && reading.RawValue < reading.SpecLow.Value)
        {
            hits.Add(HitFor(SpcRuleNames.Window, _settings.SpecWindow.Tier, Severity.High));
        }
        else if (reading.SpecHigh is not null && reading.RawValue > reading.SpecHigh.Value)
        {
            hits.Add(HitFor(SpcRuleNames.Window, _settings.SpecWindow.Tier, Severity.High));
        }

        if (Math.Abs(reading.Z) > we.Rule1.Sigma)
        {
            var severity = Math.Abs(reading.Z) >= _settings.CriticalSigma ? Severity.Critical : Severity.High;
            hits.Add(HitFor(SpcRuleNames.WesternElectric1, we.Rule1.Tier, severity));
        }

        if (series.Filled >= we.Rule2.Window && SameSide(series, we.Rule2.Window, we.Rule2.Sigma) >= we.Rule2.Count)
        {
            hits.Add(HitFor(SpcRuleNames.WesternElectric2, we.Rule2.Tier, Severity.High));
        }

        if (series.Filled >= we.Rule3.Window && SameSide(series, we.Rule3.Window, we.Rule3.Sigma) >= we.Rule3.Count)
        {
            hits.Add(HitFor(SpcRuleNames.WesternElectric3, we.Rule3.Tier, Severity.Medium));
        }

        if (series.RunHigh >= we.Rule4.Count || series.RunLow >= we.Rule4.Count)
        {
            if (MeanAtLeast(series, we.Rule4.Count, we.Rule4.MinMagnitudeSigma))
            {
                hits.Add(HitFor(SpcRuleNames.WesternElectric4, we.Rule4.Tier, Severity.Medium));
            }
        }

        if (series.Rise >= _settings.NelsonRising.Count && MeanAtLeast(series, _settings.NelsonRising.Count, _settings.NelsonRising.MinMagnitudeSigma))
        {
            hits.Add(HitFor(SpcRuleNames.NelsonSixRising, _settings.NelsonRising.Tier, Severity.Low));
        }

        if (series.Fall >= _settings.NelsonFalling.Count && MeanAtLeast(series, _settings.NelsonFalling.Count, _settings.NelsonFalling.MinMagnitudeSigma))
        {
            hits.Add(HitFor(SpcRuleNames.NelsonSixFalling, _settings.NelsonFalling.Tier, Severity.Low));
        }

        return hits;
    }

    private static Hit HitFor(string rule, string tierText, Severity natural)
    {
        var tier = string.Equals(tierText, "alert", StringComparison.OrdinalIgnoreCase) ? SpcTier.Alert : SpcTier.Watch;
        var severity = natural;
        if (tier == SpcTier.Watch && severity >= Severity.High)
        {
            severity = Severity.Medium;
        }
        else if (tier == SpcTier.Alert && severity < Severity.High)
        {
            severity = Severity.High;
        }

        return new Hit(rule, tier, severity);
    }

    private static void UpdateRuns(Series series, SpcReading reading)
    {
        if (reading.Z > 0d)
        {
            series.RunHigh++;
            series.RunLow = 0;
        }
        else if (reading.Z < 0d)
        {
            series.RunLow++;
            series.RunHigh = 0;
        }
        else
        {
            series.RunHigh = 0;
            series.RunLow = 0;
        }

        if (series.PreviousRaw is null)
        {
            series.Rise = 1;
            series.Fall = 1;
        }
        else if (reading.RawValue > series.PreviousRaw.Value)
        {
            series.Rise++;
            series.Fall = 1;
        }
        else if (reading.RawValue < series.PreviousRaw.Value)
        {
            series.Fall++;
            series.Rise = 1;
        }
        else
        {
            series.Rise = 1;
            series.Fall = 1;
        }

        series.PreviousRaw = reading.RawValue;
    }

    private static int SameSide(Series series, int window, double sigma)
    {
        var high = 0;
        var low = 0;
        var start = series.StartOf(window);
        for (var index = 0; index < window; index++)
        {
            var z = series.Z[(start + index) % series.Z.Length];
            if (z > sigma)
            {
                high++;
            }
            else if (z < -sigma)
            {
                low++;
            }
        }

        return Math.Max(high, low);
    }

    private static bool MeanAtLeast(Series series, int count, double minimum)
    {
        if (minimum <= 0d || series.Filled < count)
        {
            return minimum <= 0d;
        }

        var sum = 0d;
        var start = series.StartOf(count);
        for (var index = 0; index < count; index++)
        {
            sum += series.Z[(start + index) % series.Z.Length];
        }

        return Math.Abs(sum / count) >= minimum;
    }

    private static bool SameRules(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return true;
        }

        var a = Split(left);
        var b = Split(right);
        if (a.Length != b.Length)
        {
            return false;
        }

        Array.Sort(a, StringComparer.Ordinal);
        Array.Sort(b, StringComparer.Ordinal);
        for (var index = 0; index < a.Length; index++)
        {
            if (!string.Equals(a[index], b[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string[] Split(string rule) =>
        rule.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string CorrelationFor(string correlation) =>
        string.IsNullOrWhiteSpace(correlation) || correlation.StartsWith("nominal-", StringComparison.Ordinal)
            ? "spc-" + Guid.NewGuid().ToString("N")
            : correlation;

    private static string Key(string toolId, string? chamberId, string parameter) =>
        toolId + "\u001f" + (chamberId ?? string.Empty) + "\u001f" + parameter;

    private readonly struct Hit(string rule, SpcTier tier, Severity severity)
    {
        public string Rule { get; } = rule;

        public SpcTier Tier { get; } = tier;

        public Severity Severity { get; } = severity;
    }

    private sealed class Series
    {
        public Series(int capacity)
        {
            Z = new double[capacity];
        }

        public double[] Z { get; }

        public int Filled { get; private set; }

        public int Next { get; private set; }

        public int RunHigh { get; set; }

        public int RunLow { get; set; }

        public int Rise { get; set; }

        public int Fall { get; set; }

        public double? PreviousRaw { get; set; }

        public SpcTier Tier { get; set; }

        public Severity Severity { get; set; }

        public string Rules { get; set; } = string.Empty;

        public string? CorrelationId { get; set; }

        public DateTimeOffset LastViolationAt { get; set; }

        public int Quiet { get; set; }

        public int InControl { get; set; }

        public void Push(double z)
        {
            Z[Next] = z;
            Next = (Next + 1) % Z.Length;
            if (Filled < Z.Length)
            {
                Filled++;
            }
        }

        public int StartOf(int count) => (Next - count + Z.Length) % Z.Length;
    }
}
