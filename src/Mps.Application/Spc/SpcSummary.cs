// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Text;

using Mps.Domain.Telemetry;

namespace Mps.Application.Spc;

/// <summary>
/// Writes the plain-English ticket title for an SPC alert: which signal, where, the value, and which limit it crossed.
/// </summary>
public static class SpcSummary
{
    private static readonly HashSet<string> Acronyms = new(StringComparer.OrdinalIgnoreCase)
    {
        "rf", "esc", "cf4", "nf3", "sih4", "n2o", "nh3", "o2", "n2", "ar", "he", "c4f8", "chf3", "sf6", "cl2", "hbr", "wf6", "rms", "mfc", "dc", "hf", "diw",
    };

    public static string Describe(SpcSample sample, IReadOnlyList<SpcViolation> violations)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(violations);
        var signal = ParameterName(sample.Parameter);
        var where = Where(sample);
        var rules = RuleList(violations);
        double Z(SpcViolation violation) => (violation.Value - sample.Baseline.Center) / sample.Baseline.Sigma;
        var worst = violations.OrderByDescending(violation => Math.Abs(Z(violation))).FirstOrDefault();
        if (worst is null)
        {
            return Capitalize(signal) + " on " + where + " broke a control-chart rule.";
        }

        var display = sample.Display;
        var center = display?.Nominal ?? sample.Baseline.Center;
        var sigma = display?.Sigma ?? sample.Baseline.Sigma;
        // With display context the points are z-scores against the sample baseline: rebuild the reading in engineering units.
        var value = display is null
            ? worst.Value
            : display.Nominal + (((worst.Value - sample.Baseline.Center) / sample.Baseline.Sigma) * display.Sigma);

        var unit = display is null || string.IsNullOrWhiteSpace(display.Unit) ? string.Empty : " " + display.Unit;
        // Violations may carry an unsigned sigma distance, so the side comes from the reading itself.
        var high = worst.Value >= sample.Baseline.Center;
        var builder = new StringBuilder();
        builder.Append(Capitalize(signal)).Append(" on ").Append(where).Append(high ? " rose to " : " fell to ").Append(Num(value)).Append(unit);
        var beyondThree = Math.Abs(Z(worst)) > 3d;
        if (beyondThree)
        {
            var limit = center + ((high ? 3d : -3d) * sigma);
            builder.Append(high ? ", above the 3-sigma upper limit of " : ", below the 3-sigma lower limit of ").Append(Num(limit)).Append(unit);
        }
        else
        {
            builder.Append(high ? ", a sustained shift above the average of " : ", a sustained shift below the average of ").Append(Num(center)).Append(unit);
        }

        builder.Append(" (").Append(rules).Append(").");
        return builder.ToString();
    }

    public static string ParameterName(string parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter))
        {
            return "signal";
        }

        var words = new List<string>();
        var current = new StringBuilder();
        foreach (var character in parameter.Trim())
        {
            if (char.IsUpper(character) && current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }

            current.Append(character);
        }

        if (current.Length > 0)
        {
            words.Add(current.ToString());
        }

        var parts = words.Select(word => Acronyms.Contains(word) ? word.ToUpperInvariant() : word.ToLowerInvariant()).ToList();
        if (parts.Count > 1 && parts[^1] == "flow" && Acronyms.Contains(parts[0]))
        {
            parts.Insert(parts.Count - 1, "gas");
        }

        return string.Join(' ', parts);
    }

    private static string Where(SpcSample sample)
    {
        var tool = sample.ToolId.ToString();
        var family = sample.Display?.FamilyLabel;
        var head = string.IsNullOrWhiteSpace(family) ? tool : family + " " + tool;
        if (string.IsNullOrWhiteSpace(sample.ChamberId))
        {
            return head;
        }

        var chamber = sample.ChamberId!;
        var dash = chamber.LastIndexOf('-');
        var letter = dash >= 0 && dash < chamber.Length - 1 ? chamber[(dash + 1)..] : chamber;
        return head + ", Chamber " + letter;
    }

    private static string RuleList(IReadOnlyList<SpcViolation> violations)
    {
        var numbers = new SortedSet<int>();
        var extras = new List<string>();
        foreach (var violation in violations)
        {
            foreach (var rule in violation.Rule.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (rule.StartsWith("western-electric-", StringComparison.Ordinal) && int.TryParse(rule["western-electric-".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                {
                    numbers.Add(number);
                }
                else if (rule.StartsWith("nelson", StringComparison.Ordinal) && !extras.Contains("rising trend"))
                {
                    extras.Add("rising trend");
                }
                else if (rule == SpcRuleNames.Window && !extras.Contains("outside the operating window"))
                {
                    extras.Add("outside the operating window");
                }
            }
        }

        var parts = new List<string>();
        if (numbers.Count > 0)
        {
            var list = numbers.ToList();
            var contiguous = list.Count > 2 && list[^1] - list[0] == list.Count - 1;
            var text = contiguous
                ? list[0].ToString(CultureInfo.InvariantCulture) + "-" + list[^1].ToString(CultureInfo.InvariantCulture)
                : string.Join(", ", list.Select(number => number.ToString(CultureInfo.InvariantCulture)));
            parts.Add((list.Count == 1 ? "Western Electric rule " : "Western Electric rules ") + text + " tripped");
        }

        parts.AddRange(extras);
        return parts.Count == 0 ? "control-chart rule tripped" : string.Join("; ", parts);
    }

    private static string Num(double value)
    {
        var magnitude = Math.Abs(value);
        var format = magnitude >= 100 ? "0" : magnitude >= 10 ? "0.0" : "0.00";
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
