// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Equipment;

namespace Mps.Simulator.Fleet;

/// <summary>
/// Closed vocabularies for the sensor catalog. The schematic and the simulator both use these names.
/// </summary>
public static class SensorCatalog
{
    public const string Thermocouple = "thermocouple";

    public const string ChamberPressure = "chamberPressure";

    public const string RfForwardPower = "rfForwardPower";

    public const string RfReflectedPower = "rfReflectedPower";

    public const string GasFlow = "gasFlow";

    public const string Vibration = "vibration";

    public const string RunRate = "runRate";

    public const string Throughput = "throughput";

    public const string Output = "output";

    public const string Yield = "yield";

    public const string Alignment = "alignment";

    public const string MegasonicPower = "megasonicPower";

    public const string Chamber = "chamber";

    public const string RfGenerator = "rfGenerator";

    public const string GasBox = "gasBox";

    public const string Vacuum = "vacuum";

    public const string ChuckHeater = "chuckHeater";

    public const string WaferHandler = "waferHandler";

    private static readonly HashSet<string> Types = new(StringComparer.Ordinal)
    {
        Thermocouple,
        ChamberPressure,
        RfForwardPower,
        RfReflectedPower,
        GasFlow,
        Vibration,
        RunRate,
        Throughput,
        Output,
        Yield,
        Alignment,
        MegasonicPower,
    };

    private static readonly HashSet<string> Subsystems = new(StringComparer.Ordinal)
    {
        Chamber,
        RfGenerator,
        GasBox,
        Vacuum,
        ChuckHeater,
        WaferHandler,
    };

    private static readonly Dictionary<string, string[]> Units = new(StringComparer.Ordinal)
    {
        [Thermocouple] = ["C"],
        [RfForwardPower] = ["W"],
        [RfReflectedPower] = ["W"],
        [MegasonicPower] = ["W"],
        [Vibration] = ["mm/s"],
        [RunRate] = ["wafers/hour"],
        [Throughput] = ["wafers/hour"],
        [Yield] = ["percent"],
        [Alignment] = ["mm"],
        [GasFlow] = ["sccm", "L/min"],
        [ChamberPressure] = ["Torr", "mTorr", "atm"],
        [Output] = ["count", "nm", "percent", "nm/min", "s", "wafers"],
    };

    public static bool IsType(string type) => Types.Contains(type);

    public static bool IsSubsystem(string subsystem) => Subsystems.Contains(subsystem);

    public static bool AllowsUnit(string type, string unit)
    {
        return Units.TryGetValue(type, out var allowed) && allowed.Contains(unit, StringComparer.Ordinal);
    }

    public static IReadOnlyList<string> RequiredTypes(ToolFamily family)
    {
        var required = new List<string>
        {
            Thermocouple,
            ChamberPressure,
            GasFlow,
            Vibration,
            RunRate,
            Throughput,
            Output,
            Yield,
        };
        if (family == ToolFamily.Clean)
        {
            required.Add(MegasonicPower);
        }
        else
        {
            required.Add(RfForwardPower);
            required.Add(RfReflectedPower);
        }

        return required;
    }
}
