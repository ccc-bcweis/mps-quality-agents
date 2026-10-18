// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Simulator.Fleet;

/// <summary>
/// Inclusive operating window for one parameter on one recipe step. Nominal is the healthy center the simulator tracks.
/// </summary>
public sealed record OperatingWindow
{
    public OperatingWindow(string parameter, string unit, double minimum, double maximum, double nominal)
    {
        if (string.IsNullOrWhiteSpace(parameter))
        {
            throw new ArgumentException("Parameter is required.", nameof(parameter));
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("Unit is required.", nameof(unit));
        }

        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || !double.IsFinite(nominal))
        {
            throw new ArgumentOutOfRangeException(nameof(nominal), "Window bounds must be finite.");
        }

        if (minimum > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), "Minimum cannot exceed maximum.");
        }

        if (nominal < minimum || nominal > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(nominal), "Nominal must lie inside the window.");
        }

        Parameter = parameter.Trim();
        Unit = unit.Trim();
        Minimum = minimum;
        Maximum = maximum;
        Nominal = nominal;
    }

    public string Parameter { get; }

    public string Unit { get; }

    public double Minimum { get; }

    public double Maximum { get; }

    public double Nominal { get; }

    /// <summary>
    /// True when the recipe step does not set this parameter (it is idle in the step). The window is still a real
    /// window from the recipe data (usually the setting the parameter holds from its own step), never the sensor's
    /// physical range, so the chart and the rules always have limits for it.
    /// </summary>
    public bool Idle { get; init; }

    /// <summary>
    /// Window sigma used by fault magnitudes. Six sigma spans the whole window, so three sigma is the spec limit.
    /// </summary>
    public double Sigma => (Maximum - Minimum) / 6d;
}
