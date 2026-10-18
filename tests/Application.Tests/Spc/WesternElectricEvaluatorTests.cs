// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Spc;
using Mps.Domain;
using Mps.Domain.Incidents;
using Mps.Domain.Telemetry;

namespace Mps.Application.Tests.Spc;

public sealed class WesternElectricEvaluatorTests
{
    [Fact]
    public void InControlNoiseProducesNoViolations()
    {
        var violations = WesternElectricEvaluator.Evaluate(
            SpcSeries.Tool,
            "A",
            "escTemperature",
            SpcSeries.Unit,
            SpcSeries.Values(0.2, -0.4, 0.5, -0.2, 0.1, -0.3, 0.4, -0.1, 0.3, 0.0));

        violations.Should().BeEmpty();
    }

    [Fact]
    public void APointExactlyAtThreeSigmaIsNotRule1()
    {
        var violations = Evaluate(3d);

        violations.Should().NotContain(violation => violation.Rule == SpcRuleNames.WesternElectric1);
    }

    [Fact]
    public void OnePointBeyondThreeSigmaFiresRule1()
    {
        var violations = Evaluate(0d, 3.5d);

        var rule = violations.Should().ContainSingle(violation => violation.Rule == SpcRuleNames.WesternElectric1).Subject;
        rule.Severity.Should().Be(Severity.High);
        rule.Value.Should().Be(3.5d);
    }

    [Fact]
    public void OnePointAtOrBeyondFourSigmaIsCritical()
    {
        var violations = Evaluate(4d);

        violations.Should().ContainSingle(violation => violation.Rule == SpcRuleNames.WesternElectric1)
            .Which.Severity.Should().Be(Severity.Critical);
    }

    [Fact]
    public void TwoOfThreeBeyondTwoSigmaOnTheSameSideFiresRule2()
    {
        var violations = Evaluate(2.5d, 2.5d, 0.1d);

        violations.Should().ContainSingle(violation => violation.Rule == SpcRuleNames.WesternElectric2)
            .Which.Severity.Should().Be(Severity.High);
    }

    [Fact]
    public void TwoOfThreeBeyondTwoSigmaOnOppositeSidesDoesNotFireRule2()
    {
        var violations = Evaluate(2.5d, -2.5d, 0.1d);

        violations.Should().NotContain(violation => violation.Rule == SpcRuleNames.WesternElectric2);
    }

    [Fact]
    public void APointExactlyAtTwoSigmaDoesNotCountTowardRule2()
    {
        var violations = Evaluate(2.1d, 2d, 0d);

        violations.Should().NotContain(violation => violation.Rule == SpcRuleNames.WesternElectric2);
    }

    [Fact]
    public void TwoBeyondTwoSigmaSeparatedByTheWindowDoesNotFireRule2()
    {
        var violations = Evaluate(2.5d, 0d, 0d, 2.5d);

        violations.Should().NotContain(violation => violation.Rule == SpcRuleNames.WesternElectric2);
    }

    [Fact]
    public void FourOfFiveBeyondOneSigmaFiresRule3()
    {
        var violations = Evaluate(1.5d, 1.5d, 1.5d, 1.5d, 0d);

        violations.Should().ContainSingle(violation => violation.Rule == SpcRuleNames.WesternElectric3)
            .Which.Severity.Should().Be(Severity.Medium);
    }

    [Fact]
    public void ThreeOfFiveBeyondOneSigmaDoesNotFireRule3()
    {
        var violations = Evaluate(1.5d, 1.5d, 1.5d, 0d, 0d);

        violations.Should().NotContain(violation => violation.Rule == SpcRuleNames.WesternElectric3);
    }

    [Fact]
    public void SevenPointsOnOneSideDoNotFireRule4()
    {
        var violations = Evaluate(0.5d, 0.5d, 0.5d, 0.5d, 0.5d, 0.5d, 0.5d);

        violations.Should().NotContain(violation => violation.Rule == SpcRuleNames.WesternElectric4);
    }

    [Fact]
    public void EightPointsOnOneSideFireRule4()
    {
        var violations = Evaluate(1d, 1d, 1d, 1d, 1d, 1d, 1d, 1d);

        violations.Should().ContainSingle(violation => violation.Rule == SpcRuleNames.WesternElectric4)
            .Which.Severity.Should().Be(Severity.Medium);
    }

    [Fact]
    public void APointOnTheCenterLineBreaksAnEightPointRun()
    {
        var violations = Evaluate(0.5d, 0.5d, 0.5d, 0.5d, 0d, 0.5d, 0.5d, 0.5d);

        violations.Should().NotContain(violation => violation.Rule == SpcRuleNames.WesternElectric4);
    }

    [Fact]
    public void SixStrictlyRisingPointsFireTheNelsonRule()
    {
        var violations = Evaluate(1d, 2d, 3d, 4d, 5d, 6d);

        violations.Should().ContainSingle(violation => violation.Rule == SpcRuleNames.NelsonSixRising)
            .Which.Severity.Should().Be(Severity.Low);
    }

    [Fact]
    public void FiveRisingPointsDoNotFireTheNelsonRule()
    {
        var violations = Evaluate(1d, 2d, 3d, 4d, 5d);

        violations.Should().NotContain(violation => violation.Rule == SpcRuleNames.NelsonSixRising);
    }

    [Fact]
    public void SixFallingPointsDoNotFireTheNelsonRule()
    {
        var violations = Evaluate(6d, 5d, 4d, 3d, 2d, 1d);

        violations.Should().NotContain(violation => violation.Rule == SpcRuleNames.NelsonSixRising);
    }

    [Fact]
    public void SixFallingPointsFireTheFallingNelsonRule()
    {
        var violations = Evaluate(6d, 5d, 4d, 3d, 2d, 1d);

        violations.Should().Contain(violation => violation.Rule == SpcRuleNames.NelsonSixFalling)
            .Which.Severity.Should().Be(Severity.Low);
    }

    [Fact]
    public void AFlatRepeatDoesNotCountAsRising()
    {
        var violations = Evaluate(1d, 2d, 3d, 4d, 5d, 5d);

        violations.Should().NotContain(violation => violation.Rule == SpcRuleNames.NelsonSixRising);
    }

    [Fact]
    public void APointOutsideTheSpecWindowFiresEvenWhenItIsInsideThreeSigma()
    {
        var baseline = new ControlBaseline(center: 10d, sigma: 2d, lowerSpec: 7d, upperSpec: 13d);
        var points = SpcSeries.Values(10d, 13.5d);

        var violations = WesternElectricEvaluator.Evaluate(SpcSeries.Tool, "A", "chamberPressure", baseline, points);

        violations.Should().ContainSingle(violation => violation.Rule == SpcRuleNames.Window)
            .Which.Severity.Should().Be(Severity.High);
        violations.Should().NotContain(violation => violation.Rule == SpcRuleNames.WesternElectric1);
    }

    [Fact]
    public void APointOnTheSpecLimitIsInsideTheWindow()
    {
        var baseline = new ControlBaseline(center: 10d, sigma: 1d, lowerSpec: 7d, upperSpec: 13d);

        var violations = WesternElectricEvaluator.Evaluate(
            SpcSeries.Tool,
            null,
            "chamberPressure",
            baseline,
            SpcSeries.Values(7d, 13d));

        violations.Should().NotContain(violation => violation.Rule == SpcRuleNames.Window);
    }

    [Fact]
    public void RulesAreReportedAtTheLatestTriggeringPoint()
    {
        var violations = Evaluate(3.5d, 0d, 4.5d);

        violations.Should().ContainSingle(violation => violation.Rule == SpcRuleNames.WesternElectric1)
            .Which.Value.Should().Be(4.5d);
    }

    [Fact]
    public void PointsAreEvaluatedInTimeOrder()
    {
        var later = SpcSeries.Values(0d)[0];
        var earlier = new TelemetryPoint(later.At.AddMinutes(-5), 4.5d);

        var violations = WesternElectricEvaluator.Evaluate(
            SpcSeries.Tool,
            "A",
            "escTemperature",
            SpcSeries.Unit,
            [later, earlier]);

        violations.Should().Contain(violation => violation.Rule == SpcRuleNames.WesternElectric1 && violation.Value == 4.5d);
    }

    [Fact]
    public void SigmaMustBePositive()
    {
        var act = () => new ControlBaseline(0d, 0d, null, null);

        act.Should().Throw<DomainRuleException>().WithMessage("*sigma*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParameterIsRequired(string parameter)
    {
        var act = () => WesternElectricEvaluator.Evaluate(
            SpcSeries.Tool,
            null,
            parameter,
            SpcSeries.Unit,
            SpcSeries.Values(0d));

        act.Should().Throw<DomainRuleException>().WithMessage("*Parameter*");
    }

    private static IReadOnlyList<SpcViolation> Evaluate(params double[] values) =>
        WesternElectricEvaluator.Evaluate(
            SpcSeries.Tool,
            "A",
            "escTemperature",
            SpcSeries.Unit,
            SpcSeries.Values(values));
}
