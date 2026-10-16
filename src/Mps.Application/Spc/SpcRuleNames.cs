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

    /// <summary>Nelson trend: six strictly rising points. Six falling points are not this rule.</summary>
    public const string NelsonSixRising = "nelson-6-rising";

    /// <summary>
    /// Plain words for a rule id, or for ids joined with '+', for example
    /// "Western Electric rules 1-4 and Nelson rule 6 (rising trend)". Mirrors web/src/api/labels.ts.
    /// </summary>
    public static string Plain(string rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var ids = rule.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 1)
        {
            return ids[0] switch
            {
                WesternElectric1 => "Western Electric rule 1 (one reading more than 3 sigma from the average)",
                WesternElectric2 => "Western Electric rule 2 (2 of 3 readings more than 2 sigma out on one side)",
                WesternElectric3 => "Western Electric rule 3 (4 of 5 readings more than 1 sigma out on one side)",
                WesternElectric4 => "Western Electric rule 4 (8 readings in a row on one side of the average)",
                NelsonSixRising => "Nelson rule 6 (6 readings in a row, each higher than the last)",
                Window => "reading outside the spec window",
                _ => ids[0],
            };
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

        if (ids.Contains(Window))
        {
            phrases.Add("reading outside the spec window");
        }

        phrases.AddRange(ids.Where(id => !id.StartsWith("western-electric-", StringComparison.Ordinal) && id != NelsonSixRising && id != Window));
        return JoinAnd(phrases.ToArray());
    }

    private static string JoinAnd(string[] parts) =>
        parts.Length <= 1 ? string.Concat(parts) : string.Join(", ", parts[..^1]) + " and " + parts[^1];
}
