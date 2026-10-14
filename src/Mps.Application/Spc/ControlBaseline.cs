// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain;

namespace Mps.Application.Spc;

/// <summary>
/// Center line, sigma, and optional specification limits for one tool parameter.
/// A point on the limit is still inside the window.
/// </summary>
public sealed class ControlBaseline
{
    public ControlBaseline(double center, double sigma, double? lowerSpec, double? upperSpec)
    {
        if (double.IsNaN(center) || double.IsInfinity(center))
        {
            throw new DomainRuleException("SPC center must be a finite number.");
        }

        if (sigma <= 0d || double.IsNaN(sigma) || double.IsInfinity(sigma))
        {
            throw new DomainRuleException("SPC sigma must be a positive finite number.");
        }

        if (lowerSpec is not null && (double.IsNaN(lowerSpec.Value) || double.IsInfinity(lowerSpec.Value)))
        {
            throw new DomainRuleException("SPC lower spec must be a finite number.");
        }

        if (upperSpec is not null && (double.IsNaN(upperSpec.Value) || double.IsInfinity(upperSpec.Value)))
        {
            throw new DomainRuleException("SPC upper spec must be a finite number.");
        }

        if (lowerSpec is not null && upperSpec is not null && lowerSpec.Value > upperSpec.Value)
        {
            throw new DomainRuleException("SPC lower spec cannot be above the upper spec.");
        }

        Center = center;
        Sigma = sigma;
        LowerSpec = lowerSpec;
        UpperSpec = upperSpec;
    }

    public double Center { get; }

    public double Sigma { get; }

    public double? LowerSpec { get; }

    public double? UpperSpec { get; }

    public double Z(double value) => (value - Center) / Sigma;
}
