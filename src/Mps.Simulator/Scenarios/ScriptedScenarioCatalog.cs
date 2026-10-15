// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Hypotheses;
using Mps.Domain.Telemetry;
using Mps.Simulator.Fleet;
using Mps.Simulator.Telemetry;

namespace Mps.Simulator.Scenarios;

/// <summary>
/// One named pitch scenario. Telemetry is a function of the demo seed and this definition.
/// </summary>
public sealed class ScriptedScenario
{
    public ScriptedScenario(
        string id,
        string title,
        RootCauseCategory category,
        string summary,
        IReadOnlyList<string> toolIds,
        string parameter,
        FaultShape shape,
        MagnitudeKind magnitudeKind,
        double magnitude,
        bool confounders,
        double periodSeconds = 45d,
        double rampSeconds = 600d)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Scenario id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Scenario title is required.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Scenario summary is required.", nameof(summary));
        }

        if (string.IsNullOrWhiteSpace(parameter))
        {
            throw new ArgumentException("Parameter is required.", nameof(parameter));
        }

        ArgumentNullException.ThrowIfNull(toolIds);
        if (toolIds.Count == 0 || toolIds.Any(toolId => string.IsNullOrWhiteSpace(toolId)))
        {
            throw new ArgumentException("At least one tool id is required.", nameof(toolIds));
        }

        if (!double.IsFinite(magnitude))
        {
            throw new ArgumentOutOfRangeException(nameof(magnitude), "Magnitude must be finite.");
        }

        Id = id.Trim();
        Title = title.Trim();
        Category = category;
        Summary = summary.Trim();
        ToolIds = toolIds.Select(toolId => toolId.Trim()).ToArray();
        Parameter = parameter.Trim();
        Shape = shape;
        MagnitudeKind = magnitudeKind;
        Magnitude = magnitude;
        Confounders = confounders;
        PeriodSeconds = periodSeconds;
        RampSeconds = rampSeconds;
    }

    public string Id { get; }

    public string Title { get; }

    public RootCauseCategory Category { get; }

    public string Summary { get; }

    public IReadOnlyList<string> ToolIds { get; }

    public string Parameter { get; }

    public FaultShape Shape { get; }

    public MagnitudeKind MagnitudeKind { get; }

    public double Magnitude { get; }

    public bool Confounders { get; }

    public double PeriodSeconds { get; }

    public double RampSeconds { get; }

    public string ShapeName => Shape switch
    {
        FaultShape.Step => "step",
        FaultShape.Ramp => "ramp",
        FaultShape.Oscillation => "oscillation",
        FaultShape.Intermittent => "intermittent",
        _ => throw new InvalidOperationException("Unknown fault shape."),
    };

    public static string ChamberIdFor(string toolId)
    {
        return toolId + "-A";
    }

    public IReadOnlyList<FaultSignal> Signals(DateTimeOffset onset)
    {
        return ToolIds.Select(toolId => new FaultSignal(
            toolId,
            ChamberIdFor(toolId),
            Parameter,
            Shape,
            MagnitudeKind,
            Magnitude,
            onset,
            PeriodSeconds,
            RampSeconds)).ToArray();
    }

    /// <summary>
    /// The live stream has to cross a process-control limit. A percent change such as a 12 percent flow drop
    /// can sit inside a wide window, so the stream uses the same 4.5 sigma step the fault command already records.
    /// Catalog samples keep the percent so the scenario card stays the physical story.
    /// </summary>
    public IReadOnlyList<FaultSignal> StreamSignals(DateTimeOffset onset)
    {
        if (MagnitudeKind != MagnitudeKind.Percent)
        {
            return Signals(onset);
        }

        var magnitude = Math.CopySign(4.5d, Magnitude);
        return ToolIds.Select(toolId => new FaultSignal(
            toolId,
            ChamberIdFor(toolId),
            Parameter,
            Shape,
            MagnitudeKind.Sigma,
            magnitude,
            onset,
            PeriodSeconds,
            RampSeconds)).ToArray();
    }
}

/// <summary>
/// One sampled point of a scripted scenario.
/// </summary>
public sealed record ScenarioPoint(string ToolId, string ChamberId, string Parameter, DateTimeOffset At, double Value);

/// <summary>
/// Named pitch scenarios. SC-1 through SC-4 cover each root-cause class; SC-5, SC-7, and SC-8 add cross-plant, bearing, and recipe stories.
/// SC-6 replays the oscilloscope printout on E-01. The sample starts at the demo clock origin; live ticks use a slower ramp so the chart moves after the button press.
/// </summary>
public static class ScriptedScenarioCatalog
{
    public const int SampleSeconds = 30;

    private static readonly ScriptedScenario[] Definitions =
    [
        new(
            "SC-1",
            "Edge ring revision C on E-02",
            RootCauseCategory.EngineeringChange,
            "ESC temperature oscillates on E-02 chamber A after edge ring revision C.",
            ["E-02"],
            "escTemperature",
            FaultShape.Oscillation,
            MagnitudeKind.Sigma,
            3.5d,
            confounders: true,
            periodSeconds: 1200d),
        new(
            "SC-2",
            "ESC thermocouple calibration drift",
            RootCauseCategory.EquipmentCalibrationOrAlignment,
            "The ESC thermocouple on E-01 chamber A drifts because its calibration is overdue.",
            ["E-01"],
            "escTemperature",
            FaultShape.Ramp,
            MagnitudeKind.Sigma,
            4d,
            confounders: true,
            rampSeconds: 7200d),
        new(
            "SC-3",
            "Bad precursor gas lot",
            RootCauseCategory.SupplyChainMaterial,
            "Precursor gas lot PL-2044 is out of spec and both etch tools step off on CF4 flow.",
            ["E-01", "E-02"],
            "cf4Flow",
            FaultShape.Step,
            MagnitudeKind.Percent,
            -12d,
            confounders: true),
        new(
            "SC-4",
            "Slow chamber seasoning drift",
            RootCauseCategory.ProcessDrift,
            "Etch rate on E-01 chamber A ramps slowly as the chamber seasons after a clean.",
            ["E-01"],
            "etchRate",
            FaultShape.Ramp,
            MagnitudeKind.Sigma,
            -3.5d,
            confounders: false,
            rampSeconds: 7200d),
        new(
            "SC-6",
            "Oscilloscope RF oscillation on E-01",
            RootCauseCategory.EquipmentCalibrationOrAlignment,
            "An oscilloscope printout shows source RF power oscillating out of spec on E-01 chamber A.",
            ["E-01"],
            "rfSourcePower",
            FaultShape.Oscillation,
            MagnitudeKind.Sigma,
            5d,
            confounders: false,
            periodSeconds: 30d),
        new(
            "SC-5",
            "Precursor lot on two plants",
            RootCauseCategory.SupplyChainMaterial,
            "Precursor lot MX4419 is thin, so film thickness steps down on D-03 at Northwater and I-D-01 at Ironwood.",
            ["D-03", "I-D-01"],
            "filmThickness",
            FaultShape.Step,
            MagnitudeKind.Percent,
            -8d,
            confounders: true),
        new(
            "SC-7",
            "Pump bearing wear",
            RootCauseCategory.EquipmentCalibrationOrAlignment,
            "Foreline pump vibration on D-02 rises as the bearing runs past its service interval.",
            ["D-02"],
            "vibrationRms",
            FaultShape.Ramp,
            MagnitudeKind.Sigma,
            5d,
            confounders: false,
            rampSeconds: 9000d),
        new(
            "SC-8",
            "Recipe setpoint edited",
            RootCauseCategory.ProcessDrift,
            "An operator raised the RF power setpoint on E-03 in the recipe and did not file an engineering change.",
            ["E-03"],
            "rfBiasPower",
            FaultShape.Step,
            MagnitudeKind.Sigma,
            4.5d,
            confounders: false),
    ];

    public static IReadOnlyList<ScriptedScenario> All => Definitions;

    public static bool TryGet(string? id, out ScriptedScenario scenario)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            scenario = null!;
            return false;
        }

        var found = Definitions.FirstOrDefault(candidate => string.Equals(candidate.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
        if (found is null)
        {
            scenario = null!;
            return false;
        }

        scenario = found;
        return true;
    }

    public static IReadOnlyList<ScenarioPoint> Sample(ScriptedScenario scenario, FleetCatalog fleet, int seed)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(fleet);
        var origin = DemoClock.BaselineOrigin;
        var until = origin.AddSeconds(SampleSeconds);
        var signals = scenario.Signals(origin);
        var points = new List<ScenarioPoint>();
        foreach (var toolId in scenario.ToolIds)
        {
            var tool = fleet.GetRequired(toolId);
            var chamberId = ScriptedScenario.ChamberIdFor(toolId);
            var series = FleetTelemetry.Generate(tool, chamberId, scenario.Parameter, seed, origin, origin, until, signals);
            foreach (var point in series)
            {
                points.Add(new ScenarioPoint(toolId, chamberId, scenario.Parameter, point.At, point.Value));
            }
        }

        return points;
    }
}
