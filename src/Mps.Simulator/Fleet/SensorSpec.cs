// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Simulator.Fleet;

/// <summary>
/// Inclusive spec limits for one sensor. Warning bounds sit inside the spec and always contain the nominal.
/// </summary>
public sealed record SensorLimits
{
    public SensorLimits(double minimum, double maximum, double nominal, double warningMinimum, double warningMaximum)
    {
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || !double.IsFinite(nominal)
            || !double.IsFinite(warningMinimum) || !double.IsFinite(warningMaximum))
        {
            throw new ArgumentOutOfRangeException(nameof(nominal), "Sensor limits must be finite.");
        }

        if (minimum > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), "Minimum cannot exceed maximum.");
        }

        if (nominal < minimum || nominal > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(nominal), "Nominal must lie inside the spec.");
        }

        if (warningMinimum < minimum || warningMaximum > maximum || warningMinimum > nominal || warningMaximum < nominal || warningMinimum > warningMaximum)
        {
            throw new ArgumentOutOfRangeException(nameof(warningMinimum), "The warning band must sit inside the spec and contain the nominal.");
        }

        Minimum = minimum;
        Maximum = maximum;
        Nominal = nominal;
        WarningMinimum = warningMinimum;
        WarningMaximum = warningMaximum;
    }

    public double Minimum { get; }

    public double Maximum { get; }

    public double Nominal { get; }

    public double WarningMinimum { get; }

    public double WarningMaximum { get; }

    public double Sigma => (Maximum - Minimum) / 6d;
}

/// <summary>
/// One point on the tool schematic. The simulator emits <see cref="Id"/> and <see cref="Unit"/> and no other sensor identity.
/// </summary>
public sealed record SensorSpec
{
    public SensorSpec(
        string id,
        string type,
        string unit,
        string subsystem,
        string schematicAnchor,
        SensorLimits limits,
        string? chamberId,
        string? parameter,
        string? tracksSensorId,
        string? kind = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Sensor id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Sensor type is required.", nameof(type));
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("Sensor unit is required.", nameof(unit));
        }

        if (string.IsNullOrWhiteSpace(subsystem))
        {
            throw new ArgumentException("Subsystem is required.", nameof(subsystem));
        }

        if (string.IsNullOrWhiteSpace(schematicAnchor))
        {
            throw new ArgumentException("Schematic anchor is required.", nameof(schematicAnchor));
        }

        ArgumentNullException.ThrowIfNull(limits);
        Id = id.Trim();
        Type = type.Trim();
        Unit = unit.Trim();
        Subsystem = subsystem.Trim();
        SchematicAnchor = schematicAnchor.Trim();
        Limits = limits;
        ChamberId = string.IsNullOrWhiteSpace(chamberId) ? null : chamberId.Trim();
        Parameter = string.IsNullOrWhiteSpace(parameter) ? null : parameter.Trim();
        TracksSensorId = string.IsNullOrWhiteSpace(tracksSensorId) ? null : tracksSensorId.Trim();
        Kind = string.IsNullOrWhiteSpace(kind) ? SensorKinds.Periodic : kind.Trim();
        if (!SensorKinds.IsKnown(Kind))
        {
            throw new ArgumentException("Sensor kind must be periodic or cumulative.", nameof(kind));
        }
    }

    /// <summary>
    /// <see cref="SensorKinds.Periodic"/>: each reading stands alone and is SPC-charted against its limits.
    /// <see cref="SensorKinds.Cumulative"/>: a running count. Shown as a count or trend, never with SPC limits.
    /// </summary>
    public string Kind { get; }

    public bool IsCumulative => Kind == SensorKinds.Cumulative;

    public string Id { get; }

    public string Type { get; }

    public string Unit { get; }

    public string Subsystem { get; }

    public string SchematicAnchor { get; }

    public SensorLimits Limits { get; }

    /// <summary>Null when the point belongs to the whole tool (run rate, throughput, output, yield).</summary>
    public string? ChamberId { get; }

    /// <summary>Recipe parameter this point measures. Null for reflected power and for tool-level points.</summary>
    public string? Parameter { get; }

    /// <summary>Forward-power sensor a reflected-power point follows.</summary>
    public string? TracksSensorId { get; }

    public bool IsToolScope => ChamberId is null;

    public string FrameChamberId(string toolId)
    {
        return ChamberId ?? toolId;
    }
}

public static class SensorKinds
{
    public const string Periodic = "periodic";

    public const string Cumulative = "cumulative";

    public static bool IsKnown(string kind) => kind is Periodic or Cumulative;
}
