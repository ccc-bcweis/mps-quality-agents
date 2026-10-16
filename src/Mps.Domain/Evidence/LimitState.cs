// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Domain.Evidence;

/// <summary>
/// Where a reading sits against its recipe spec and its control limits. Out of spec means outside the recipe minimum or
/// maximum. Out of control means inside spec but statistically unusual: past the control limits or breaking a control rule.
/// </summary>
public static class LimitState
{
    public const string OutOfSpec = "outOfSpec";

    public const string OutOfControl = "outOfControl";

    public const string InControl = "inControl";

    /// <summary>The state of one value. A control rule that broke makes an in-spec value out of control. Null without any limit.</summary>
    public static string? Of(double? value, double? specLow, double? specHigh, double? controlLow, double? controlHigh, bool ruleBroke = false)
    {
        if (value is not double measured || !double.IsFinite(measured))
        {
            return null;
        }

        var hasSpec = specLow is not null || specHigh is not null;
        if (hasSpec && ((specLow is double low && measured < low) || (specHigh is double high && measured > high)))
        {
            return OutOfSpec;
        }

        var outsideControl = (controlLow is double cl && measured < cl) || (controlHigh is double ch && measured > ch);
        if (outsideControl || ruleBroke)
        {
            return OutOfControl;
        }

        return hasSpec || controlLow is not null || controlHigh is not null ? InControl : null;
    }
}
