// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

using Mps.Application.Decisions;
using Mps.Application.Metadata;
using Mps.Application.Spc;

namespace Mps.Api.Http;

/// <summary>One setting the Rules &amp; Process screen shows. Kind is number or toggle; Value is the configured value as text.</summary>
public sealed record RuleSettingResponse(string Id, string Group, string Label, string Kind, string Value, string? Unit, string? Help);

/// <summary>One condition of a business rule, as configured in business-rules.yaml.</summary>
public sealed record RuleConditionResponse(string Field, string Label, string Kind, string Value);

/// <summary>Reads the live rule settings from the loaded configuration (spc-rules.yaml, investigation.yaml and code limits).</summary>
public static class RuleSettings
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static IReadOnlyList<RuleSettingResponse> Read(SpcRuleSettings spc, Mps.Agents.Investigation.ShapeWait wait)
    {
        ArgumentNullException.ThrowIfNull(spc);
        ArgumentNullException.ThrowIfNull(wait);
        const string Chart = "Control chart";
        const string Rules = "Western Electric and Nelson rules";
        const string Clearing = "Alert clearing";
        const string Investigation = "Investigation";
        const string Decision = "Decision";
        var we = spc.WesternElectric;
        return
        [
            Number("spc.controlLimitSigma", Chart, "Control limits", spc.ControlLimitSigma, "sigma", "Centre line plus or minus this many sigma."),
            Number("spc.watchSigma", Chart, "Watch level", spc.WatchSigma, "sigma", "A tile turns amber past this distance from nominal."),
            Number("spc.criticalSigma", Chart, "Critical level", spc.CriticalSigma, "sigma", "A reading this far out makes the alert critical."),
            Toggle("spc.we1.on", Rules, "Rule 1 on", we.Rule1.Count > 0),
            Number("spc.we1.sigma", Rules, "Rule 1: one reading beyond", we.Rule1.Sigma, "sigma", null),
            Toggle("spc.we2.on", Rules, "Rule 2 on", we.Rule2.Count > 0),
            Number("spc.we2.count", Rules, "Rule 2: readings out of the window", we.Rule2.Count, "readings", $"{we.Rule2.Count} of {we.Rule2.Window} beyond {Text(we.Rule2.Sigma)} sigma, one side."),
            Number("spc.we2.sigma", Rules, "Rule 2: beyond", we.Rule2.Sigma, "sigma", null),
            Toggle("spc.we3.on", Rules, "Rule 3 on", we.Rule3.Count > 0),
            Number("spc.we3.count", Rules, "Rule 3: readings out of the window", we.Rule3.Count, "readings", $"{we.Rule3.Count} of {we.Rule3.Window} beyond {Text(we.Rule3.Sigma)} sigma, one side."),
            Number("spc.we3.sigma", Rules, "Rule 3: beyond", we.Rule3.Sigma, "sigma", null),
            Toggle("spc.we4.on", Rules, "Rule 4 on", we.Rule4.Count > 0),
            Number("spc.we4.count", Rules, "Rule 4: readings in a row on one side", we.Rule4.Count, "readings", null),
            Toggle("spc.nelson.on", Rules, "Nelson rising run on", spc.NelsonRising.Count > 0),
            Number("spc.nelson.count", Rules, "Nelson: readings in a row, each higher", spc.NelsonRising.Count, "readings", null),
            Number("clear.readings", Clearing, "Readings in control before an alert clears", AlertClearing.InControlReadings, "readings", null),
            Number("clear.sigma", Clearing, "In control means within", AlertClearing.ZoneSigma, "sigma", null),
            Toggle("investigation.wait.on", Investigation, "Wait for a clear signal shape", wait.Attempts > 0),
            Number("investigation.wait.minReadings", Investigation, "Readings since the first rule break", wait.MinReadingsSinceOnset, "readings", null),
            Number("investigation.wait.attempts", Investigation, "Tries", wait.Attempts, null, null),
            Number("investigation.wait.delay", Investigation, "Time between tries", wait.Delay.TotalSeconds, "seconds", null),
            Number("decision.overrideMin", Decision, "Shortest override justification", HumanDecisionService.MinOverrideJustification, "characters", null),
        ];
    }

    public static IReadOnlyList<RuleConditionResponse> Conditions(RuleWhen when)
    {
        ArgumentNullException.ThrowIfNull(when);
        var rows = new List<RuleConditionResponse>();
        void Add(string field, string label, object? value, string kind = "text")
        {
            if (value is null || (value is string text && string.IsNullOrWhiteSpace(text)))
            {
                return;
            }

            rows.Add(new RuleConditionResponse(field, label, kind, value switch
            {
                bool flag => flag ? "true" : "false",
                double number => Text(number),
                int count => count.ToString(Invariant),
                _ => value.ToString() ?? string.Empty,
            }));
        }

        Add("severity", "Severity is", when.Severity);
        Add("severityAtLeast", "Severity at least", when.SeverityAtLeast);
        Add("severityAtMost", "Severity at most", when.SeverityAtMost);
        Add("alertRuleId", "Alert rule", when.AlertRuleId);
        Add("parameterClass", "Signal type", when.ParameterClass);
        Add("parameterId", "Signal", when.ParameterId);
        Add("toolId", "Tool", when.ToolId);
        Add("siteId", "Plant", when.SiteId);
        Add("repeatAtLeast", "Repeats at least", when.RepeatAtLeast, "number");
        Add("confidenceBelow", "Confidence below", when.ConfidenceBelow, "number");
        Add("evidenceSufficiency", "Evidence is", when.EvidenceSufficiency);
        Add("channel", "Channel", when.Channel);
        Add("requiresAlert", "Needs an alert", when.RequiresAlert, "toggle");
        return rows;
    }

    private static RuleSettingResponse Number(string id, string group, string label, double value, string? unit, string? help) =>
        new(id, group, label, "number", Text(value), unit, help);

    private static RuleSettingResponse Toggle(string id, string group, string label, bool on) =>
        new(id, group, label, "toggle", on ? "true" : "false", null, null);

    private static string Text(double value) => value.ToString("0.###", Invariant);
}
