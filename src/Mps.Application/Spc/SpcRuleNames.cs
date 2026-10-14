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
}
