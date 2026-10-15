// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Text.RegularExpressions;

namespace Mps.Simulator.Telemetry;

/// <summary>
/// Seed and scenario records were authored against <see cref="ReferenceOrigin"/>. When real time is on, every
/// date in them moves forward by whole days so "today" lands on the current UTC date. Gaps between records stay the same.
/// </summary>
public static partial class RecordDates
{
    public static readonly DateTimeOffset ReferenceOrigin = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private static int _shiftDays;

    /// <summary>Whole days added to every authored date. Zero unless <see cref="UseToday"/> ran.</summary>
    public static int ShiftDays => Volatile.Read(ref _shiftDays);

    public static void UseToday(TimeProvider? time = null)
    {
        var today = (time ?? TimeProvider.System).GetUtcNow().UtcDateTime.Date;
        Volatile.Write(ref _shiftDays, (int)(today - ReferenceOrigin.UtcDateTime.Date).TotalDays);
    }

    public static void UseReference() => Volatile.Write(ref _shiftDays, 0);

    public static DateTimeOffset Shift(DateTimeOffset value) => value.AddDays(ShiftDays);

    /// <summary>Moves every yyyy-MM-dd date (with or without a time) in the text forward by <see cref="ShiftDays"/>.</summary>
    public static string Rebase(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var days = ShiftDays;
        if (days == 0)
        {
            return text;
        }

        return DatePattern().Replace(text, match =>
        {
            if (!DateTime.TryParseExact(match.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return match.Value;
            }

            return date.AddDays(days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        });
    }

    [GeneratedRegex(@"(?<![\d-])(20\d\d-[01]\d-[0-3]\d)(?![\d])")]
    private static partial Regex DatePattern();
}
