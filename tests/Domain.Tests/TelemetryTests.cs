// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Telemetry;

namespace Mps.Domain.Tests;

public sealed class TelemetryTests
{
    private static readonly DateTimeOffset From = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0)]
    [InlineData(-1.5)]
    [InlineData(31.5)]
    public void APointKeepsAFiniteValue(double value)
    {
        var point = new TelemetryPoint(From, value);

        point.At.Should().Be(From);
        point.Value.Should().Be(value);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void APointRejectsANonFiniteValue(double value)
    {
        var act = () => new TelemetryPoint(From, value);
        act.Should().Throw<DomainRuleException>().WithMessage("Telemetry value must be a finite number.");
    }

    [Fact]
    public void AWindowKeepsItsBoundsAndACopyOfThePoints()
    {
        var points = new List<TelemetryPoint> { new(From, 1), new(From.AddMinutes(1), 2) };
        var window = new TelemetryWindow(new ToolId("E-02"), "  escTemperature  ", From, From, points)
        {
            Nominals = [30, 31],
        };

        window.ToolId.Value.Should().Be("E-02");
        window.Parameter.Should().Be("escTemperature");
        window.From.Should().Be(From);
        window.To.Should().Be(From);
        window.Points.Should().Equal(points);
        window.Nominals.Should().Equal(30, 31);

        points.Clear();
        window.Points.Should().HaveCount(2);
        window.Points[1].Value.Should().Be(2);

        var open = new TelemetryWindow(new ToolId("E-02"), "p", From, From.AddTicks(1), []);
        open.To.Should().Be(From.AddTicks(1));
        open.Points.Should().BeEmpty();
        open.Nominals.Should().BeNull();
    }

    [Fact]
    public void AWindowRequiresAToolAParameterPointsAndAnEndThatIsNotBeforeTheStart()
    {
        var points = new List<TelemetryPoint> { new(From, 1) };
        Rejects(() => new TelemetryWindow(default, "p", From, From, points), "Tool id is required.");
        Rejects(() => new TelemetryWindow(new ToolId("E-02"), " ", From, From, points), "Parameter is required.");
        Rejects(() => new TelemetryWindow(new ToolId("E-02"), null!, From, From, points), "Parameter is required.");
        Rejects(() => new TelemetryWindow(new ToolId("E-02"), "p", From, From.AddTicks(-1), points), "Telemetry window end is before its start.");

        var missing = () => new TelemetryWindow(new ToolId("E-02"), "p", From, From, null!);
        missing.Should().Throw<ArgumentNullException>().WithParameterName("points");
    }

    [Fact]
    public void AViolationKeepsFiniteNumbersAndDropsABlankChamber()
    {
        var violation = new SpcViolation(
            "  WE1  ",
            new ToolId("E-02"),
            "  C1  ",
            "  escTemperature  ",
            From,
            0,
            -1.5,
            Severity.Critical)
        {
            Center = 30,
            LowerSpec = 20,
            UpperSpec = 40,
        };

        violation.Rule.Should().Be("WE1");
        violation.ToolId.Value.Should().Be("E-02");
        violation.ChamberId.Should().Be("C1");
        violation.Parameter.Should().Be("escTemperature");
        violation.At.Should().Be(From);
        violation.Value.Should().Be(0);
        violation.Sigma.Should().Be(-1.5);
        violation.Severity.Should().Be(Severity.Critical);
        violation.Center.Should().Be(30);
        violation.LowerSpec.Should().Be(20);
        violation.UpperSpec.Should().Be(40);

        new SpcViolation("WE1", new ToolId("E-02"), null, "p", From, 1, 1, Severity.Low).ChamberId.Should().BeNull();
        new SpcViolation("WE1", new ToolId("E-02"), "   ", "p", From, 1, 1, Severity.High).ChamberId.Should().BeNull();
    }

    [Fact]
    public void AViolationRejectsABlankRuleAnEmptyToolOrANonFiniteNumber()
    {
        var tool = new ToolId("E-02");
        Rejects(() => new SpcViolation(" ", tool, "C1", "p", From, 1, 1, Severity.Low), "SPC rule is required.");
        Rejects(() => new SpcViolation(null!, tool, "C1", "p", From, 1, 1, Severity.Low), "SPC rule is required.");
        Rejects(() => new SpcViolation("WE1", default, "C1", "p", From, 1, 1, Severity.Low), "Tool id is required.");
        Rejects(() => new SpcViolation("WE1", tool, "C1", " ", From, 1, 1, Severity.Low), "Parameter is required.");
        Rejects(() => new SpcViolation("WE1", tool, "C1", null!, From, 1, 1, Severity.Low), "Parameter is required.");
        Rejects(() => new SpcViolation("WE1", tool, "C1", "p", From, double.NaN, 1, Severity.Low), "SPC value and sigma must be finite numbers.");
        Rejects(() => new SpcViolation("WE1", tool, "C1", "p", From, double.PositiveInfinity, 1, Severity.Low), "SPC value and sigma must be finite numbers.");
        Rejects(() => new SpcViolation("WE1", tool, "C1", "p", From, double.NegativeInfinity, 1, Severity.Low), "SPC value and sigma must be finite numbers.");
        Rejects(() => new SpcViolation("WE1", tool, "C1", "p", From, 1, double.NaN, Severity.Low), "SPC value and sigma must be finite numbers.");
        Rejects(() => new SpcViolation("WE1", tool, "C1", "p", From, 1, double.PositiveInfinity, Severity.Low), "SPC value and sigma must be finite numbers.");
        Rejects(() => new SpcViolation("WE1", tool, "C1", "p", From, 1, double.NegativeInfinity, Severity.Medium), "SPC value and sigma must be finite numbers.");
    }

    private static void Rejects<T>(Func<T> act, string message)
    {
        act.Should().Throw<DomainRuleException>().WithMessage(message);
    }
}
