// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Simulator.Telemetry;

public enum FaultShape
{
    Step,
    Ramp,
    Oscillation,
    Intermittent,
}

public enum MagnitudeKind
{
    /// <summary>Multiples of the operating-window sigma, where six sigma spans minimum to maximum.</summary>
    Sigma,

    /// <summary>Percent of the window nominal. A zero nominal uses the window maximum instead.</summary>
    Percent,
}

/// <summary>
/// A signal imposed on telemetry. It carries no root-cause label. Ground truth stays in the fault catalog writer.
/// </summary>
public sealed record FaultSignal
{
    public FaultSignal(
        string toolId,
        string? chamberId,
        string parameter,
        FaultShape shape,
        MagnitudeKind magnitudeKind,
        double magnitude,
        DateTimeOffset onset,
        double periodSeconds = 30d,
        double rampSeconds = 600d,
        double dutyCycle = 0.5d)
    {
        if (string.IsNullOrWhiteSpace(toolId))
        {
            throw new ArgumentException("Tool id is required.", nameof(toolId));
        }

        if (string.IsNullOrWhiteSpace(parameter))
        {
            throw new ArgumentException("Parameter is required.", nameof(parameter));
        }

        if (!double.IsFinite(magnitude))
        {
            throw new ArgumentOutOfRangeException(nameof(magnitude), "Magnitude must be finite.");
        }

        if (periodSeconds <= 0 || !double.IsFinite(periodSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(periodSeconds), "Period must be positive.");
        }

        if (rampSeconds <= 0 || !double.IsFinite(rampSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(rampSeconds), "Ramp duration must be positive.");
        }

        if (dutyCycle is < 0d or > 1d || double.IsNaN(dutyCycle))
        {
            throw new ArgumentOutOfRangeException(nameof(dutyCycle), "Duty cycle must be between 0 and 1.");
        }

        ToolId = toolId.Trim();
        ChamberId = string.IsNullOrWhiteSpace(chamberId) ? null : chamberId.Trim();
        Parameter = parameter.Trim();
        Shape = shape;
        MagnitudeKind = magnitudeKind;
        Magnitude = magnitude;
        Onset = onset;
        PeriodSeconds = periodSeconds;
        RampSeconds = rampSeconds;
        DutyCycle = dutyCycle;
    }

    /// <summary>Same fault with its period and ramp shortened by <paramref name="pace"/>, so it develops that many times faster.</summary>
    public FaultSignal Paced(double pace) =>
        pace is > 0 and not 1d && double.IsFinite(pace)
            ? new FaultSignal(ToolId, ChamberId, Parameter, Shape, MagnitudeKind, Magnitude, Onset, PeriodSeconds / pace, RampSeconds / pace, DutyCycle)
            : this;

    public string ToolId { get; }

    public string? ChamberId { get; }

    public string Parameter { get; }

    public FaultShape Shape { get; }

    public MagnitudeKind MagnitudeKind { get; }

    public double Magnitude { get; }

    public DateTimeOffset Onset { get; }

    public double PeriodSeconds { get; }

    public double RampSeconds { get; }

    public double DutyCycle { get; }

    public bool AppliesTo(string toolId, string chamberId, string parameter, DateTimeOffset at)
    {
        if (at < Onset)
        {
            return false;
        }

        if (!string.Equals(ToolId, toolId, StringComparison.Ordinal) || !string.Equals(Parameter, parameter, StringComparison.Ordinal))
        {
            return false;
        }

        return ChamberId is null || string.Equals(ChamberId, chamberId, StringComparison.Ordinal);
    }
}
