// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

namespace Mps.Application.Spc;

/// <summary>
/// Plain-English rule sentences built from the rule parameters in spc-rules.yaml, so the sigma and the counts in
/// every sentence are the ones the rules engine runs with. The API sends these to the console; no copy lives in UI code.
/// </summary>
public static class SpcRuleText
{
    /// <summary>Rule ids the console can show, in display order.</summary>
    public static IReadOnlyList<string> RuleIds { get; } =
    [
        SpcRuleNames.WesternElectric1,
        SpcRuleNames.WesternElectric2,
        SpcRuleNames.WesternElectric3,
        SpcRuleNames.WesternElectric4,
        SpcRuleNames.NelsonSixRising,
        SpcRuleNames.Window,
        SpcRuleNames.ManualMeasurement,
    ];

    /// <summary>The full sentence the console shows for one rule, for example "Western Electric rules, rule 1: one point is more than 3σ from the average".</summary>
    public static string Label(string rule, SpcRuleSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var we = settings.WesternElectric;
        return rule switch
        {
            SpcRuleNames.WesternElectric1 => $"Western Electric rules, rule 1: {Count(we.Rule1.Count)} point is more than {N(we.Rule1.Sigma)}σ from the average",
            SpcRuleNames.WesternElectric2 => $"Western Electric rules, rule 2: {we.Rule2.Count} of the last {we.Rule2.Window} points are more than {N(we.Rule2.Sigma)}σ out on the same side",
            SpcRuleNames.WesternElectric3 => $"Western Electric rules, rule 3: {we.Rule3.Count} of the last {we.Rule3.Window} points are more than {N(we.Rule3.Sigma)}σ out on the same side",
            SpcRuleNames.WesternElectric4 => $"Western Electric rules, rule 4: {we.Rule4.Count} points in a row on one side of the average",
            SpcRuleNames.NelsonSixRising => $"Nelson rule 6: {settings.NelsonRising.Count} readings in a row, each higher than the last",
            SpcRuleNames.NelsonSixFalling => $"Nelson rule 6: {settings.NelsonFalling.Count} readings in a row, each lower than the last",
            SpcRuleNames.Window => "Reading outside the spec window",
            SpcRuleNames.ManualMeasurement => "Manual measurement outside the printed spec",
            _ => rule,
        };
    }

    /// <summary>The sentence used in server text (evidence, ticket notes), for example "Western Electric rule 1 (one reading more than 3 sigma from the average)".</summary>
    public static string Plain(string rule, SpcRuleSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var we = settings.WesternElectric;
        return rule switch
        {
            SpcRuleNames.WesternElectric1 => $"Western Electric rule 1 ({Count(we.Rule1.Count)} reading more than {N(we.Rule1.Sigma)} sigma from the average)",
            SpcRuleNames.WesternElectric2 => $"Western Electric rule 2 ({we.Rule2.Count} of {we.Rule2.Window} readings more than {N(we.Rule2.Sigma)} sigma out on one side)",
            SpcRuleNames.WesternElectric3 => $"Western Electric rule 3 ({we.Rule3.Count} of {we.Rule3.Window} readings more than {N(we.Rule3.Sigma)} sigma out on one side)",
            SpcRuleNames.WesternElectric4 => $"Western Electric rule 4 ({we.Rule4.Count} readings in a row on one side of the average)",
            SpcRuleNames.NelsonSixRising => $"Nelson rule 6 ({settings.NelsonRising.Count} readings in a row, each higher than the last)",
            SpcRuleNames.NelsonSixFalling => $"Nelson rule 6 ({settings.NelsonFalling.Count} readings in a row, each lower than the last)",
            SpcRuleNames.Window => "reading outside the spec window",
            SpcRuleNames.ManualMeasurement => "manual measurement outside the printed spec",
            _ => rule,
        };
    }

    /// <summary>The short form the Rules &amp; Process map uses, for example "2 of 3 beyond 2 sigma, one side".</summary>
    public static string Brief(string rule, SpcRuleSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var we = settings.WesternElectric;
        return rule switch
        {
            SpcRuleNames.WesternElectric1 => $"{Count(we.Rule1.Count)} reading beyond {N(we.Rule1.Sigma)} sigma",
            SpcRuleNames.WesternElectric2 => $"{we.Rule2.Count} of {we.Rule2.Window} beyond {N(we.Rule2.Sigma)} sigma, one side",
            SpcRuleNames.WesternElectric3 => $"{we.Rule3.Count} of {we.Rule3.Window} beyond {N(we.Rule3.Sigma)} sigma, one side",
            SpcRuleNames.WesternElectric4 => $"{we.Rule4.Count} readings in a row on one side",
            SpcRuleNames.NelsonSixRising => $"{settings.NelsonRising.Count} readings in a row, each higher",
            SpcRuleNames.NelsonSixFalling => $"{settings.NelsonFalling.Count} readings in a row, each lower",
            SpcRuleNames.Window => "reading outside the spec window",
            SpcRuleNames.ManualMeasurement => "manual measurement outside the printed spec",
            _ => rule,
        };
    }

    /// <summary>"one" for a single reading, digits otherwise.</summary>
    private static string Count(int count) => count == 1 ? "one" : count.ToString(CultureInfo.InvariantCulture);

    internal static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
