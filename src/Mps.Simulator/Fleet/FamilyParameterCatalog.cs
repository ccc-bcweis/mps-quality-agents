// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Equipment;

namespace Mps.Simulator.Fleet;

/// <summary>
/// Family envelopes from the spec table. A step window must sit inside the envelope and use its unit.
/// Bounds that the spec only names (film thickness, edge-ring height, flows) are fictional demo limits.
/// </summary>
public readonly record struct ParameterLimit(string Unit, double Minimum, double Maximum);

public static class FamilyParameterCatalog
{
    private static readonly IReadOnlyDictionary<string, ParameterLimit> Deposition = Map(
        Limit("chamberPressure", "Torr", 2d, 6d),
        Limit("heaterTemperature", "C", 350d, 400d),
        Limit("rfPower", "W", 300d, 600d),
        Limit("sih4Flow", "sccm", 5d, 80d),
        Limit("n2oFlow", "sccm", 200d, 1500d),
        Limit("nh3Flow", "sccm", 10d, 200d),
        Limit("waferCenterOffset", "mm", 0d, 0.15d),
        Limit("vibrationRms", "mm/s", 0d, 2.5d),
        Limit("particlesAdders", "count", 0d, 10d),
        Limit("filmThickness", "nm", 50d, 800d),
        Limit("filmUniformity", "percent", 0d, 5d));

    private static readonly IReadOnlyDictionary<string, ParameterLimit> Etch = Map(
        Limit("chamberPressure", "mTorr", 20d, 80d),
        Limit("escTemperature", "C", 40d, 60d),
        Limit("wallTemperature", "C", 55d, 65d),
        Limit("rfSourcePower", "W", 1000d, 2000d),
        Limit("rfBiasPower", "W", 200d, 500d),
        Limit("cf4Flow", "sccm", 10d, 200d),
        Limit("chf3Flow", "sccm", 5d, 120d),
        Limit("o2Flow", "sccm", 0d, 100d),
        Limit("arFlow", "sccm", 20d, 500d),
        Limit("waferCenterOffset", "mm", 0d, 0.15d),
        Limit("edgeRingHeight", "mm", 1.5d, 2.5d),
        Limit("vibrationRms", "mm/s", 0d, 2.5d),
        Limit("particlesAdders", "count", 0d, 15d),
        Limit("etchRate", "nm/min", 50d, 800d),
        Limit("endpointTime", "s", 15d, 180d));

    private static readonly IReadOnlyDictionary<string, ParameterLimit> Strip = Map(
        Limit("chamberPressure", "Torr", 0.8d, 1.5d),
        Limit("temperature", "C", 250d, 280d),
        Limit("remotePlasmaPower", "W", 2000d, 4000d),
        Limit("o2Flow", "sccm", 200d, 5000d),
        Limit("n2h2Flow", "sccm", 50d, 2000d),
        Limit("waferCenterOffset", "mm", 0d, 0.15d),
        Limit("vibrationRms", "mm/s", 0d, 2.5d),
        Limit("particlesAdders", "count", 0d, 10d),
        Limit("ashRate", "nm/min", 100d, 2500d));

    private static readonly IReadOnlyDictionary<string, ParameterLimit> Clean = Map(
        Limit("ambientPressure", "atm", 0.97d, 1.03d),
        Limit("bathTemperature", "C", 60d, 70d),
        Limit("megasonicPower", "W", 500d, 900d),
        Limit("sc1Flow", "L/min", 0.4d, 2.5d),
        Limit("sc2Flow", "L/min", 0.4d, 2.5d),
        Limit("diwFlow", "L/min", 0.5d, 3d),
        Limit("spinChuckRunout", "mm", 0d, 0.20d),
        Limit("vibrationRms", "mm/s", 0d, 3d),
        Limit("particlesAdders", "count", 0d, 5d),
        Limit("residueCount", "count", 0d, 20d));

    public static IReadOnlyDictionary<string, ParameterLimit> For(ToolFamily family)
    {
        return family switch
        {
            ToolFamily.Deposition => Deposition,
            ToolFamily.Etch => Etch,
            ToolFamily.Strip => Strip,
            ToolFamily.Clean => Clean,
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "Unknown tool family."),
        };
    }

    public static string ModelFor(ToolFamily family)
    {
        return family switch
        {
            ToolFamily.Deposition => "MPS-D7",
            ToolFamily.Etch => "MPS-E7",
            ToolFamily.Strip => "MPS-S7",
            ToolFamily.Clean => "MPS-C7",
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "Unknown tool family."),
        };
    }

    public static string IdPrefix(ToolFamily family)
    {
        return family switch
        {
            ToolFamily.Deposition => "D",
            ToolFamily.Etch => "E",
            ToolFamily.Strip => "S",
            ToolFamily.Clean => "C",
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "Unknown tool family."),
        };
    }

    public static bool TryParseFamily(string? text, out ToolFamily family)
    {
        switch (text?.Trim())
        {
            case "deposition":
                family = ToolFamily.Deposition;
                return true;
            case "etch":
                family = ToolFamily.Etch;
                return true;
            case "strip":
                family = ToolFamily.Strip;
                return true;
            case "clean":
                family = ToolFamily.Clean;
                return true;
            default:
                family = default;
                return false;
        }
    }

    private static KeyValuePair<string, ParameterLimit> Limit(string name, string unit, double minimum, double maximum)
    {
        return new KeyValuePair<string, ParameterLimit>(name, new ParameterLimit(unit, minimum, maximum));
    }

    private static Dictionary<string, ParameterLimit> Map(params KeyValuePair<string, ParameterLimit>[] rows)
    {
        return rows.ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);
    }
}
