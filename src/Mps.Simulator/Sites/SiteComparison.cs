// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

using Mps.Domain.Equipment;
using Mps.Simulator.Fleet;
using Mps.Simulator.Telemetry;

namespace Mps.Simulator.Sites;

/// <summary>
/// Compares one tool family across plants. Northwater is the reference. A plant is flagged when its
/// sample mean sits more than two reference spreads away, or its spread is more than one and a half times wider.
/// </summary>
public static class SiteComparison
{
    public const double SpreadRatio = 1.5d;

    public static FamilyComparison Compare(SiteWorld world, ToolFamily family)
    {
        ArgumentNullException.ThrowIfNull(world);
        var reference = world.Sites.FirstOrDefault(site => site.Id == SiteIds.Northwater) ?? world.Sites[0];
        var limits = FamilyParameterCatalog.For(family);
        var parameters = new List<ParameterComparison>();
        foreach (var name in limits.Keys.Order(StringComparer.Ordinal))
        {
            var limit = limits[name];
            var rows = new List<SiteParameterStats>();
            foreach (var site in world.Sites)
            {
                var tools = site.Tools.Where(tool => tool.Family == family).OrderBy(tool => tool.Id, StringComparer.Ordinal).ToArray();
                if (tools.Length == 0)
                {
                    continue;
                }

                double mean = 0;
                double sigma = 0;
                double alarms = 0;
                foreach (var tool in tools)
                {
                    var sample = Sample(world.Seed, site, tool, name, limit);
                    mean += sample.Mean;
                    sigma += sample.Sigma;
                    alarms += sample.AlarmRate;
                }

                var count = tools.Length;
                var starts = tools.Sum(tool => tool.WaferStartsPerHour);
                var yield = tools.Sum(tool => tool.WaferStartsPerHour * tool.YieldPct) / starts;
                var uptime = tools.Sum(tool => tool.WaferStartsPerHour * tool.UptimePct) / starts;
                rows.Add(new SiteParameterStats(
                    site.Id,
                    site.Name,
                    Round4(mean / count),
                    Round4(sigma / count),
                    Round4(alarms / count),
                    SitePlantGenerator.Round1(uptime),
                    SitePlantGenerator.Round1(yield),
                    false,
                    null));
            }

            var baseline = rows.First(row => row.SiteId == reference.Id);
            var finished = new List<SiteParameterStats>();
            foreach (var row in rows)
            {
                var site = world.Find(row.SiteId)!;
                var meanApart = Math.Abs(row.Mean - baseline.Mean) > (2d * Math.Max(baseline.Sigma, 1e-6d));
                var spreadApart = row.Sigma / Math.Max(baseline.Sigma, 1e-6d) > SpreadRatio;
                var flagged = row.SiteId != reference.Id && (meanApart || spreadApart);
                var reason = flagged ? Reason(site, reference, family, name, limit.Unit, row, baseline, meanApart, spreadApart) : null;
                finished.Add(new SiteParameterStats(row.SiteId, row.SiteName, row.Mean, row.Sigma, row.AlarmRate, row.UptimePct, row.YieldPct, flagged, reason));
            }

            parameters.Add(new ParameterComparison(name, limit.Unit, finished));
        }

        return new FamilyComparison(family, reference.Id, parameters);
    }

    private static (double Mean, double Sigma, double AlarmRate) Sample(int seed, SimulatedSite site, PlacedTool tool, string parameter, ParameterLimit limit)
    {
        var behavior = site.Behavior.FirstOrDefault(item => item.Family == tool.Family && item.Parameter == parameter);
        var offset = behavior?.MeanOffset ?? 0d;
        var scale = behavior?.SigmaScale ?? 1d;
        var baselineSigma = Math.Max((limit.Maximum - limit.Minimum) / 6d, 1e-6d);
        var nominal = Nominal(tool, parameter, limit);
        var center = nominal + offset;
        var sampleSigma = baselineSigma * scale;
        var low = nominal - (3d * baselineSigma);
        var high = nominal + (3d * baselineSigma);
        var rng = SeededRng.Create(seed, site.Id + "|" + tool.Id + "|" + parameter);
        var values = new double[SitePlantGenerator.SampleCount];
        var alarms = 0;
        for (var index = 0; index < values.Length; index++)
        {
            var value = center + (rng.NextNormal() * sampleSigma);
            values[index] = value;
            if (value < low || value > high)
            {
                alarms++;
            }
        }

        var mean = values.Average();
        var variance = values.Sum(value => (value - mean) * (value - mean)) / values.Length;
        return (mean, Math.Sqrt(variance), alarms / (double)values.Length);
    }

    private static double Nominal(PlacedTool tool, string parameter, ParameterLimit limit)
    {
        var windows = tool.Spec.Recipes.SelectMany(recipe => recipe.Steps).SelectMany(step => step.Windows)
            .Where(window => string.Equals(window.Parameter, parameter, StringComparison.Ordinal))
            .ToArray();
        if (windows.Length == 0)
        {
            return (limit.Minimum + limit.Maximum) / 2d;
        }

        return windows.Average(window => window.Nominal);
    }

    private static string Reason(
        SimulatedSite site,
        SimulatedSite reference,
        ToolFamily family,
        string parameter,
        string unit,
        SiteParameterStats row,
        SiteParameterStats baseline,
        bool meanApart,
        bool spreadApart)
    {
        var words = ParameterWords(parameter);
        var familyWord = FamilyWord(family);
        var sentence = site.Name + "'s " + words;
        if (meanApart)
        {
            var gap = Math.Abs(row.Mean - baseline.Mean);
            var shown = gap >= 10d
                ? Math.Round(gap, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture)
                : gap.ToString("0.0", CultureInfo.InvariantCulture);
            var hotter = unit is "C" or "°C";
            var direction = row.Mean >= baseline.Mean
                ? (hotter ? "hotter" : "higher")
                : (hotter ? "colder" : "lower");
            var amount = hotter ? shown + " degrees" : shown + " " + unit;
            sentence += " averages about " + amount + " " + direction + " than the same " + familyWord + " tools at " + reference.Name;
            sentence += spreadApart ? ", and the spread is wider." : ".";
        }
        else
        {
            sentence += " spread is wider than the same " + familyWord + " tools at " + reference.Name + ".";
        }

        if (row.AlarmRate > baseline.AlarmRate + 0.01d)
        {
            sentence += " That is why the alarm rate is higher on this line.";
        }

        if (row.UptimePct < baseline.UptimePct - 0.05d && row.YieldPct < baseline.YieldPct - 0.05d)
        {
            sentence += " Uptime and yield on this " + familyWord + " line are lower.";
        }

        return sentence;
    }

    private static string ParameterWords(string parameter)
    {
        return parameter switch
        {
            "escTemperature" => "electrostatic chuck temperature",
            "wallTemperature" => "chamber wall temperature",
            "heaterTemperature" => "heater temperature",
            "filmThickness" => "film thickness",
            "chamberPressure" => "chamber pressure",
            _ => SplitCamel(parameter),
        };
    }

    private static string SplitCamel(string parameter)
    {
        var chars = new List<char>();
        foreach (var character in parameter)
        {
            if (char.IsUpper(character) && chars.Count > 0)
            {
                chars.Add(' ');
            }

            chars.Add(char.ToLowerInvariant(character));
        }

        return new string(chars.ToArray());
    }

    private static string FamilyWord(ToolFamily family)
    {
        return family switch
        {
            ToolFamily.Deposition => "deposition",
            ToolFamily.Etch => "etch",
            ToolFamily.Strip => "strip",
            ToolFamily.Clean => "clean",
            _ => family.ToString(),
        };
    }

    private static double Round4(double value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}

public sealed class FamilyComparison
{
    public FamilyComparison(ToolFamily family, string referenceSiteId, IReadOnlyList<ParameterComparison> parameters)
    {
        Family = family;
        ReferenceSiteId = referenceSiteId;
        Parameters = parameters;
    }

    public ToolFamily Family { get; }

    public string ReferenceSiteId { get; }

    public IReadOnlyList<ParameterComparison> Parameters { get; }
}

public sealed class ParameterComparison
{
    public ParameterComparison(string parameter, string unit, IReadOnlyList<SiteParameterStats> sites)
    {
        Parameter = parameter;
        Unit = unit;
        Sites = sites;
    }

    public string Parameter { get; }

    public string Unit { get; }

    public IReadOnlyList<SiteParameterStats> Sites { get; }
}

public sealed class SiteParameterStats
{
    public SiteParameterStats(
        string siteId,
        string siteName,
        double mean,
        double sigma,
        double alarmRate,
        double uptimePct,
        double yieldPct,
        bool discrepancy,
        string? reason)
    {
        SiteId = siteId;
        SiteName = siteName;
        Mean = mean;
        Sigma = sigma;
        AlarmRate = alarmRate;
        UptimePct = uptimePct;
        YieldPct = yieldPct;
        Discrepancy = discrepancy;
        Reason = reason;
    }

    public string SiteId { get; }

    public string SiteName { get; }

    public double Mean { get; }

    public double Sigma { get; }

    public double AlarmRate { get; }

    public double UptimePct { get; }

    public double YieldPct { get; }

    public bool Discrepancy { get; }

    public string? Reason { get; }
}
