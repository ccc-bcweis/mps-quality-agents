// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

using Mps.Application.Printouts;

namespace Mps.Connectors.Printouts;

/// <summary>
/// Reads the labeled lines a bench printout carries: tool, chamber, time, instrument, parameter, value, unit, and spec.
/// </summary>
public sealed class LabeledPrintoutParser : IPrintoutFieldParser
{
    public bool TryParse(string text, out PrintoutReading reading)
    {
        reading = null!;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var key = NormalizeKey(line[..colon]);
            var field = line[(colon + 1)..].Trim();
            if (key.Length == 0 || field.Length == 0 || fields.ContainsKey(key))
            {
                continue;
            }

            fields[key] = field;
        }

        if (!fields.TryGetValue("tool", out var toolId))
        {
            return false;
        }

        if (!fields.TryGetValue("instrument", out var instrument))
        {
            return false;
        }

        if (!fields.TryGetValue("parameter", out var parameter))
        {
            return false;
        }

        if (!TryValue(fields, out var value))
        {
            return false;
        }

        if (!fields.TryGetValue("unit", out var unit))
        {
            return false;
        }

        if (!fields.TryGetValue("spec minimum", out var specMinText) || !PrintoutReading.TryParseNumber(specMinText, out var specMin))
        {
            return false;
        }

        if (!fields.TryGetValue("spec maximum", out var specMaxText) || !PrintoutReading.TryParseNumber(specMaxText, out var specMax))
        {
            return false;
        }

        if (!TryInstant(fields, out var observedAt))
        {
            return false;
        }

        fields.TryGetValue("chamber", out var chamber);
        fields.TryGetValue("shape", out var shape);
        if (!fields.TryGetValue("operator", out var initials))
        {
            fields.TryGetValue("operator initials", out initials);
        }

        if (!PrintoutReading.IsInstrument(instrument))
        {
            return false;
        }

        try
        {
            reading = new PrintoutReading(
                toolId,
                chamber,
                observedAt,
                instrument,
                parameter,
                value,
                unit,
                specMin,
                specMax,
                shape,
                initials);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryValue(Dictionary<string, string> fields, out double value)
    {
        if (fields.TryGetValue("value", out var text) && PrintoutReading.TryParseNumber(text, out value))
        {
            return true;
        }

        if (fields.TryGetValue("reading", out text) && PrintoutReading.TryParseNumber(text, out value))
        {
            return true;
        }

        value = 0d;
        return false;
    }

    private static bool TryInstant(Dictionary<string, string> fields, out DateTimeOffset observedAt)
    {
        observedAt = default;
        if (!fields.TryGetValue("observed at", out var text) && !fields.TryGetValue("timestamp", out text))
        {
            return false;
        }

        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out observedAt))
        {
            return true;
        }

        var cleaned = text.Replace("UTC", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        return DateTimeOffset.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out observedAt);
    }

    private static string NormalizeKey(string key)
    {
        var trimmed = key.Trim().ToLowerInvariant();
        return trimmed switch
        {
            "spec min" => "spec minimum",
            "spec max" => "spec maximum",
            "time" => "observed at",
            "observed" => "observed at",
            _ => trimmed,
        };
    }
}
