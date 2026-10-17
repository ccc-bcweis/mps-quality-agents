// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Telemetry;

namespace Mps.Application.Spc;

/// <summary>
/// Specification-window check plus Western Electric rules 1-4 and the optional six-point rising trend.
/// Each fired rule is reported once, at its latest triggering point. In-control series produce no violations.
/// </summary>
public static class WesternElectricEvaluator
{
    public static IReadOnlyList<SpcViolation> Evaluate(
        ToolId toolId,
        string? chamberId,
        string parameter,
        ControlBaseline baseline,
        IReadOnlyList<TelemetryPoint> points)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(points);
        if (toolId.IsEmpty)
        {
            throw new DomainRuleException("Tool id is required.");
        }

        var parameterName = parameter?.Trim() ?? string.Empty;
        if (parameterName.Length == 0)
        {
            throw new DomainRuleException("Parameter is required.");
        }

        var ordered = points.OrderBy(point => point.At).ToArray();
        SpcViolation? window = null;
        SpcViolation? beyondThree = null;
        SpcViolation? twoOfThree = null;
        SpcViolation? fourOfFive = null;
        SpcViolation? eightOnOneSide = null;
        SpcViolation? rising = null;

        var rules = SpcRuleSettings.Shared;
        var we = rules.WesternElectric;
        for (var index = 0; index < ordered.Length; index++)
        {
            var point = ordered[index];
            var z = baseline.Z(point.Value);

            if (IsOutsideWindow(point.Value, baseline))
            {
                window = Create(
                    SpcRuleNames.Window,
                    toolId,
                    chamberId,
                    parameterName,
                    point,
                    baseline,
                    Severity.High);
            }

            if (Math.Abs(z) > we.Rule1.Sigma)
            {
                beyondThree = Create(
                    SpcRuleNames.WesternElectric1,
                    toolId,
                    chamberId,
                    parameterName,
                    point,
                    baseline,
                    Math.Abs(z) >= rules.CriticalSigma ? Severity.Critical : Severity.High);
            }

            if (index >= we.Rule2.Window - 1 && SameSideBeyond(ordered, baseline, index - (we.Rule2.Window - 1), index, we.Rule2.Sigma) >= we.Rule2.Count)
            {
                twoOfThree = Create(
                    SpcRuleNames.WesternElectric2,
                    toolId,
                    chamberId,
                    parameterName,
                    point,
                    baseline,
                    Severity.High);
            }

            if (index >= we.Rule3.Window - 1 && SameSideBeyond(ordered, baseline, index - (we.Rule3.Window - 1), index, we.Rule3.Sigma) >= we.Rule3.Count)
            {
                fourOfFive = Create(
                    SpcRuleNames.WesternElectric3,
                    toolId,
                    chamberId,
                    parameterName,
                    point,
                    baseline,
                    Severity.Medium);
            }

            if (index >= we.Rule4.Count - 1 && RunOnOneSide(ordered, baseline, index - (we.Rule4.Count - 1), index))
            {
                eightOnOneSide = Create(
                    SpcRuleNames.WesternElectric4,
                    toolId,
                    chamberId,
                    parameterName,
                    point,
                    baseline,
                    Severity.Medium);
            }

            if (index >= rules.NelsonRising.Count - 1 && StrictlyRising(ordered, index - (rules.NelsonRising.Count - 1), index))
            {
                rising = Create(
                    SpcRuleNames.NelsonSixRising,
                    toolId,
                    chamberId,
                    parameterName,
                    point,
                    baseline,
                    Severity.Low);
            }
        }

        var violations = new List<SpcViolation>(6);
        Add(violations, window);
        Add(violations, beyondThree);
        Add(violations, twoOfThree);
        Add(violations, fourOfFive);
        Add(violations, eightOnOneSide);
        Add(violations, rising);
        return violations;
    }

    public static Severity Highest(IReadOnlyList<SpcViolation> violations)
    {
        ArgumentNullException.ThrowIfNull(violations);
        if (violations.Count == 0)
        {
            throw new DomainRuleException("Severity requires at least one SPC violation.");
        }

        var highest = Severity.Low;
        foreach (var violation in violations)
        {
            if (violation.Severity > highest)
            {
                highest = violation.Severity;
            }
        }

        return highest;
    }

    public static string JoinRules(IReadOnlyList<SpcViolation> violations)
    {
        ArgumentNullException.ThrowIfNull(violations);
        if (violations.Count == 0)
        {
            throw new DomainRuleException("An alert rule requires at least one SPC violation.");
        }

        return string.Join('+', violations.Select(violation => violation.Rule));
    }

    private static bool IsOutsideWindow(double value, ControlBaseline baseline)
    {
        if (baseline.LowerSpec is not null && value < baseline.LowerSpec.Value)
        {
            return true;
        }

        return baseline.UpperSpec is not null && value > baseline.UpperSpec.Value;
    }

    private static int SameSideBeyond(
        TelemetryPoint[] points,
        ControlBaseline baseline,
        int start,
        int end,
        double threshold)
    {
        var high = 0;
        var low = 0;
        for (var index = start; index <= end; index++)
        {
            var z = baseline.Z(points[index].Value);
            if (z > threshold)
            {
                high++;
            }
            else if (z < -threshold)
            {
                low++;
            }
        }

        return Math.Max(high, low);
    }

    private static bool RunOnOneSide(TelemetryPoint[] points, ControlBaseline baseline, int start, int end)
    {
        var high = true;
        var low = true;
        for (var index = start; index <= end; index++)
        {
            var z = baseline.Z(points[index].Value);
            if (z <= 0d)
            {
                high = false;
            }

            if (z >= 0d)
            {
                low = false;
            }
        }

        return high || low;
    }

    private static bool StrictlyRising(TelemetryPoint[] points, int start, int end)
    {
        for (var index = start + 1; index <= end; index++)
        {
            if (!(points[index].Value > points[index - 1].Value))
            {
                return false;
            }
        }

        return true;
    }

    private static SpcViolation Create(
        string rule,
        ToolId toolId,
        string? chamberId,
        string parameter,
        TelemetryPoint point,
        ControlBaseline baseline,
        Severity severity)
    {
        return new SpcViolation(rule, toolId, chamberId, parameter, point.At, point.Value, baseline.Sigma, severity)
        {
            Center = baseline.Center,
            LowerSpec = baseline.LowerSpec,
            UpperSpec = baseline.UpperSpec,
        };
    }

    private static void Add(List<SpcViolation> violations, SpcViolation? violation)
    {
        if (violation is not null)
        {
            violations.Add(violation);
        }
    }
}
