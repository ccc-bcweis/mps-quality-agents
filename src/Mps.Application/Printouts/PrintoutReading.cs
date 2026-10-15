// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

namespace Mps.Application.Printouts;

/// <summary>
/// One bench reading taken off a printout. The engineer can correct every field before it is stored.
/// </summary>
public sealed class PrintoutReading
{
    public const string Oscilloscope = "oscilloscope";

    public const string RfPowerMeter = "rfPowerMeter";

    public const string ParticleCounter = "particleCounter";

    public const string ThermocoupleLogger = "thermocoupleLogger";

    public static readonly string[] Instruments = [Oscilloscope, RfPowerMeter, ParticleCounter, ThermocoupleLogger];

    public static readonly string[] Shapes = ["step", "ramp", "oscillation", "intermittent"];

    public PrintoutReading(
        string toolId,
        string? chamberId,
        DateTimeOffset observedAt,
        string instrument,
        string parameter,
        double value,
        string unit,
        double specMinimum,
        double specMaximum,
        string? shape,
        string? operatorInitials)
    {
        if (string.IsNullOrWhiteSpace(toolId))
        {
            throw new ArgumentException("Tool id is required.", nameof(toolId));
        }

        var canonicalInstrument = CanonicalInstrument(instrument);
        if (canonicalInstrument is null)
        {
            throw new ArgumentException("Instrument must be a known bench instrument.", nameof(instrument));
        }

        if (string.IsNullOrWhiteSpace(parameter))
        {
            throw new ArgumentException("Parameter is required.", nameof(parameter));
        }

        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Value must be finite.");
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("Unit is required.", nameof(unit));
        }

        if (!double.IsFinite(specMinimum) || !double.IsFinite(specMaximum) || specMinimum > specMaximum)
        {
            throw new ArgumentException("Spec limits must be finite and the minimum cannot exceed the maximum.");
        }

        var normalizedShape = string.IsNullOrWhiteSpace(shape) ? null : shape.Trim().ToLowerInvariant();
        if (normalizedShape is not null && !Shapes.Contains(normalizedShape, StringComparer.Ordinal))
        {
            throw new ArgumentException("Shape must be step, ramp, oscillation, or intermittent.", nameof(shape));
        }

        ToolId = toolId.Trim();
        ChamberId = string.IsNullOrWhiteSpace(chamberId) ? null : chamberId.Trim();
        ObservedAt = observedAt;
        Instrument = canonicalInstrument;
        Parameter = parameter.Trim();
        Value = value;
        Unit = unit.Trim();
        SpecMinimum = specMinimum;
        SpecMaximum = specMaximum;
        Shape = normalizedShape;
        OperatorInitials = string.IsNullOrWhiteSpace(operatorInitials) ? null : operatorInitials.Trim();
    }

    public string ToolId { get; }

    public string? ChamberId { get; }

    public DateTimeOffset ObservedAt { get; }

    public string Instrument { get; }

    public string Parameter { get; }

    public double Value { get; }

    public string Unit { get; }

    public double SpecMinimum { get; }

    public double SpecMaximum { get; }

    public string? Shape { get; }

    public string? OperatorInitials { get; }

    public bool OutOfSpec => Value < SpecMinimum || Value > SpecMaximum;

    /// <summary>
    /// Sigma of the printed window. Six sigma spans the printed minimum to maximum.
    /// A reading that only just leaves the window is still injected at 3.5 sigma so the trace is visible.
    /// </summary>
    public double MagnitudeSigma()
    {
        var span = SpecMaximum - SpecMinimum;
        var sigma = span <= 0d ? 1d : span / 6d;
        var nominal = (SpecMinimum + SpecMaximum) / 2d;
        var magnitude = (Value - nominal) / sigma;
        if (!double.IsFinite(magnitude) || magnitude == 0d)
        {
            magnitude = Value >= nominal ? 3.5d : -3.5d;
        }

        if (Math.Abs(magnitude) < 3.5d)
        {
            magnitude = Math.CopySign(3.5d, magnitude);
        }

        return magnitude;
    }

    public static bool IsInstrument(string? value) => CanonicalInstrument(value) is not null;

    public static string? CanonicalInstrument(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var key = new string(value.Trim().Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        return key switch
        {
            "oscilloscope" => Oscilloscope,
            "rfpowermeter" => RfPowerMeter,
            "particlecounter" => ParticleCounter,
            "thermocouplelogger" => ThermocoupleLogger,
            _ => null,
        };
    }

    /// <summary>
    /// Etch tools name source RF power <c>rfSourcePower</c>. A printout that says <c>rfPower</c> means that channel.
    /// Deposition tools already use <c>rfPower</c>.
    /// </summary>
    public static string CatalogParameter(string toolId, string parameter)
    {
        var name = parameter.Trim();
        if (!name.Equals("rfPower", StringComparison.OrdinalIgnoreCase))
        {
            return name;
        }

        if (toolId.StartsWith("E-", StringComparison.OrdinalIgnoreCase)
            || toolId.Contains("-E-", StringComparison.OrdinalIgnoreCase))
        {
            return "rfSourcePower";
        }

        return "rfPower";
    }

    public static bool TryParseNumber(string? text, out double value)
    {
        value = 0d;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var token = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    }
}
