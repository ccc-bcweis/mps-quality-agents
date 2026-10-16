// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Domain.Telemetry;

/// <summary>
/// Plain display names for telemetry parameter ids ("cf4Flow" is "CF4 flow"). The id stays the contract value.
/// Mirrors web/src/sites/parameters.ts; a test keeps the two lists equal.
/// </summary>
public static class ParameterNames
{
    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        ["ambientPressure"] = "Room pressure",
        ["arFlow"] = "Argon flow",
        ["ashRate"] = "Ash rate",
        ["bathTemperature"] = "Chemical bath temperature",
        ["cf4Flow"] = "CF4 flow",
        ["chamberPressure"] = "Chamber pressure",
        ["chf3Flow"] = "CHF3 flow",
        ["diwFlow"] = "DIW flow",
        ["edgeRingHeight"] = "Edge ring height",
        ["endpointTime"] = "Endpoint time",
        ["escTemperature"] = "ESC temperature",
        ["etchRate"] = "Etch rate",
        ["filmThickness"] = "Film thickness",
        ["filmUniformity"] = "Film uniformity",
        ["heaterTemperature"] = "Heater temperature",
        ["megasonicPower"] = "Megasonic power",
        ["n2h2Flow"] = "N2H2 flow",
        ["n2oFlow"] = "N2O flow",
        ["nh3Flow"] = "NH3 flow",
        ["o2Flow"] = "O2 flow",
        ["particlesAdders"] = "Particle adders",
        ["remotePlasmaPower"] = "Remote plasma power",
        ["residueCount"] = "Residue count",
        ["rfBiasPower"] = "Bias RF power",
        ["rfPower"] = "RF power",
        ["rfSourcePower"] = "Source RF power",
        ["sc1Flow"] = "SC1 flow",
        ["sc2Flow"] = "SC2 flow",
        ["sih4Flow"] = "SiH4 flow",
        ["spinChuckRunout"] = "Spin chuck runout",
        ["temperature"] = "Temperature",
        ["vibrationRms"] = "Vibration",
        ["waferCenterOffset"] = "Wafer center offset",
        ["wallTemperature"] = "Chamber wall temperature",
    };

    public static IReadOnlyDictionary<string, string> All => Labels;

    /// <summary>The display name, or the id split into words when it is not in the list.</summary>
    public static string Display(string parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        if (Labels.TryGetValue(parameter, out var label))
        {
            return label;
        }

        var words = System.Text.RegularExpressions.Regex.Replace(parameter, "([a-z0-9])([A-Z])", "$1 $2").ToLowerInvariant();
        return words.Length == 0 ? parameter : char.ToUpperInvariant(words[0]) + words[1..];
    }
}
