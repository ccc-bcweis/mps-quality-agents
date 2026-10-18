// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Application.Spc;

/// <summary>
/// Stable rule ids stored on alerts. Comparisons use a strict greater-than, matching the spec wording.
/// </summary>
public static class SpcRuleNames
{
    public const string Window = "window";

    public const string WesternElectric1 = "western-electric-1";

    public const string WesternElectric2 = "western-electric-2";

    public const string WesternElectric3 = "western-electric-3";

    public const string WesternElectric4 = "western-electric-4";

    /// <summary>Nelson trend: N strictly rising points.</summary>
    public const string NelsonSixRising = "nelson-6-rising";

    /// <summary>Nelson trend: N strictly falling points.</summary>
    public const string NelsonSixFalling = "nelson-6-falling";

    /// <summary>A typed-in or scanned reading outside the printed spec.</summary>
    public const string ManualMeasurement = "manualMeasurement";

    /// <summary>
    /// Plain words for a rule id, or for ids joined with '+', for example
    /// "Western Electric rules 1-4 and Nelson rule 6 (rising trend)". One rule reads as its full sentence from
    /// <see cref="SpcRuleText.Plain"/>, built from spc-rules.yaml.
    /// </summary>
    public static string Plain(string rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var ids = rule.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 1)
        {
            return SpcRuleText.Plain(ids[0], SpcRuleSettings.Shared);
        }

        var numbers = ids.Select(id => id.StartsWith("western-electric-", StringComparison.Ordinal) && int.TryParse(id[^1..], out var n) ? n : 0)
            .Where(n => n > 0).Order().ToArray();
        var phrases = new List<string>();
        if (numbers.Length > 0)
        {
            var contiguous = numbers.Length > 2 && numbers[^1] - numbers[0] == numbers.Length - 1;
            var list = contiguous ? numbers[0] + "-" + numbers[^1] : JoinAnd(numbers.Select(n => n.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray());
            phrases.Add((numbers.Length > 1 ? "Western Electric rules " : "Western Electric rule ") + list);
        }

        if (ids.Contains(NelsonSixRising))
        {
            phrases.Add("Nelson rule 6 (rising trend)");
        }

        if (ids.Contains(NelsonSixFalling))
        {
            phrases.Add("Nelson rule 6 (falling trend)");
        }

        if (ids.Contains(Window))
        {
            phrases.Add("reading outside the spec window");
        }

        phrases.AddRange(ids.Where(id => !id.StartsWith("western-electric-", StringComparison.Ordinal)
            && id != NelsonSixRising
            && id != NelsonSixFalling
            && id != Window));
        return JoinAnd(phrases.ToArray());
    }

    private static string JoinAnd(string[] parts) =>
        parts.Length <= 1 ? string.Concat(parts) : string.Join(", ", parts[..^1]) + " and " + parts[^1];
}
