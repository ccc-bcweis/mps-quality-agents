// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Spc;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Telemetry;

namespace Mps.Application.Tests.Spc;

public sealed class SpcRuleEngineTests
{
    [Fact]
    public void TheShippedFileNamesEveryNewKnob()
    {
        var rules = SpcRuleSettings.Shared;

        rules.WesternElectric.Rule1.Tier.Should().Be("alert");
        rules.WesternElectric.Rule2.Tier.Should().Be("alert");
        rules.WesternElectric.Rule3.Tier.Should().Be("watch");
        rules.WesternElectric.Rule4.Tier.Should().Be("watch");
        rules.WesternElectric.Rule4.MinMagnitudeSigma.Should().BeGreaterThan(0);
        rules.NelsonRising.MinMagnitudeSigma.Should().BeGreaterThan(0);
        rules.NelsonFalling.Count.Should().Be(rules.NelsonRising.Count);
        rules.NelsonFalling.Tier.Should().Be("watch");
        rules.SpecWindow.Tier.Should().Be("alert");
        rules.WatchExpiry.Readings.Should().BeGreaterThan(0);
        rules.WatchExpiry.Minutes.Should().BeGreaterThan(0);
        rules.TicketCap.PerToolPerHour.Should().BeGreaterThan(0);
        rules.Sensors.EnabledKinds.Should().Contain("periodic");
        rules.LimitCheck.RecentReadings.Should().Be(20);
        rules.RecoveryWatchMinutes.Should().Be(30);
    }

    [Fact]
    public void AMissingTierFailsLoadWithTheFieldName()
    {
        var path = Path.Combine(Path.GetTempPath(), "spc-rules-" + Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(path, """
            controlLimitSigma: 3
            watchSigma: 2
            criticalSigma: 4
            westernElectric:
              rule1: { sigma: 3, count: 1, window: 1 }
              rule2: { sigma: 2, count: 2, window: 3, tier: alert }
              rule3: { sigma: 1, count: 4, window: 5, tier: watch }
              rule4: { count: 8, tier: watch, minMagnitudeSigma: 0.5 }
            nelsonRising: { count: 6, tier: watch, minMagnitudeSigma: 0.5 }
            nelsonFalling: { count: 6, tier: watch, minMagnitudeSigma: 0.5 }
            specWindow: { tier: alert }
            limitCheck: { recentReadings: 20 }
            clearing: { readings: 8, sigma: 2 }
            alertDebounceMinutes: 10
            recoveryWatchMinutes: 30
            watchExpiry: { readings: 24, minutes: 30 }
            ticketCap: { perToolPerHour: 6 }
            sensors:
              enabledKinds: [periodic]
              disabledParameters: []
            """);

        try
        {
            var act = () => SpcRuleSettings.Load(path);
            act.Should().Throw<InvalidOperationException>().WithMessage("*westernElectric.rule1.tier*");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AFaultCorrelationIsKeptAndANominalOneBecomesAnSpcId()
    {
        var engine = new SpcRuleEngine(SpcRuleSettings.Shared);

        var faulted = engine.Observe(Point(0, 4d, "fault-cascade"));
        faulted!.Action.Should().Be(SpcAction.Raise);
        faulted.CorrelationId.Should().Be("fault-cascade");

        var nominal = new SpcRuleEngine(SpcRuleSettings.Shared).Observe(Point(0, 4d, "nominal-E-02"));
        nominal!.CorrelationId.Should().StartWith("spc-");
    }

    [Fact]
    public void IncrementalRulesMatchTheBatchEvaluatorOnTheSameSeries()
    {
        var values = new[]
        {
            0d, 0.2d, -0.1d, 1.2d, 1.3d, 1.1d, 1.4d, 2.2d, 2.3d, 0.1d, 3.5d, 3.6d, 3.1d, 2.9d,
            0.4d, 0.8d, 1.2d, 1.6d, 2.0d, 2.4d, 6d, 5d, 4d, 3d, 2d, 1d, 0d, -0.2d, 0.2d,
        };
        var engine = new SpcRuleEngine(SpcRuleSettings.Shared);
        var baseline = ControlBaseline.StandardScore;
        var points = new List<TelemetryPoint>(values.Length);
        for (var index = 0; index < values.Length; index++)
        {
            var at = SpcSeries.Start.AddMinutes(index);
            points.Add(new TelemetryPoint(at, values[index]));
            var observation = engine.Observe(Point(index, values[index], "nominal-E-02"));
            var live = WesternElectricEvaluator.Evaluate(SpcSeries.Tool, "A", "escTemperature", baseline, points, SpcRuleSettings.Shared)
                .Where(violation => violation.At == at)
                .Select(violation => violation.Rule)
                .Order(StringComparer.Ordinal)
                .ToArray();
            observation!.LiveRules.Order(StringComparer.Ordinal).Should().Equal(live, "point " + index);
        }
    }

    [Fact]
    public void IncrementalSpecWindowMatchesTheBatchEvaluator()
    {
        var baseline = new ControlBaseline(50d, 5d, 40d, 60d);
        var values = new[] { 50d, 55d, 61d, 50d, 39d };
        var engine = new SpcRuleEngine(SpcRuleSettings.Shared);
        var points = new List<TelemetryPoint>();
        for (var index = 0; index < values.Length; index++)
        {
            var at = SpcSeries.Start.AddMinutes(index);
            points.Add(new TelemetryPoint(at, values[index]));
            var z = baseline.Z(values[index]);
            var observation = engine.Observe(new SpcReading(
                "E-02",
                "A",
                "chamberPressure",
                "periodic",
                at,
                z,
                values[index],
                baseline.Center,
                baseline.Sigma,
                baseline.LowerSpec,
                baseline.UpperSpec,
                "nominal-E-02",
                "Torr"));
            var live = WesternElectricEvaluator.Evaluate(new ToolId("E-02"), "A", "chamberPressure", baseline, points, SpcRuleSettings.Shared)
                .Where(violation => violation.At == at)
                .Select(violation => violation.Rule)
                .Order(StringComparer.Ordinal);
            observation!.LiveRules.Order(StringComparer.Ordinal).Should().Equal(live);
        }
    }

    [Fact]
    public void EachZoneAndCountRuleCatchesADriftInsideThreeSigma()
    {
        Live(2.2d, 2.2d, 2.2d).Should().Equal(SpcRuleNames.WesternElectric2);
        Live(1.2d, 1.2d, 1.2d, 1.2d, 1.2d).Should().Equal(SpcRuleNames.WesternElectric3);
        Live(1d, 1d, 1d, 1d, 1d, 1d, 1d, 1d).Should().Equal(SpcRuleNames.WesternElectric4);
    }

    [Fact]
    public void EightSmallPointsOnOneSideDoNotFireTheCountRule()
    {
        Live(0.2d, 0.2d, 0.2d, 0.2d, 0.2d, 0.2d, 0.2d, 0.2d).Should().BeEmpty();
    }

    [Fact]
    public void AWatchExpiresWhenSimulatedMinutesPassWithoutANewViolation()
    {
        var settings = SpcRuleSettings.Shared.Copy();
        settings.WatchExpiry.Readings = 100;
        settings.WatchExpiry.Minutes = 10;
        var engine = new SpcRuleEngine(settings);
        for (var index = 0; index < 8; index++)
        {
            engine.Observe(Point(index, 1d, "nominal-E-02"));
        }

        var expired = engine.Observe(Point(20, 0d, "nominal-E-02"));
        expired!.Action.Should().Be(SpcAction.Clear);
        expired.ClearReason.Should().Be("expiry");
        expired.Tier.Should().Be(SpcTier.Nominal);
    }

    [Fact]
    public void RebuildKeepsTheAlertAndEmptiesTheRun()
    {
        var engine = new SpcRuleEngine(SpcRuleSettings.Shared);
        engine.Observe(Point(0, 4d, "fault-1"));
        engine.Rebuild(
        [
            new SpcSeriesSeed("E-02", "A", "escTemperature", Severity.High, SpcRuleNames.WesternElectric1, "fault-1", SpcSeries.Start),
        ]);

        var quiet = engine.Observe(Point(1, 0.1d, "nominal-E-02"));
        quiet!.Action.Should().Be(SpcAction.None);
        quiet.Tier.Should().Be(SpcTier.Alert);
    }

    private static IReadOnlyList<string> Live(params double[] values)
    {
        var engine = new SpcRuleEngine(SpcRuleSettings.Shared);
        SpcObservation? last = null;
        for (var index = 0; index < values.Length; index++)
        {
            last = engine.Observe(Point(index, values[index], "nominal-E-02"));
        }

        return last!.LiveRules;
    }

    private static SpcReading Point(int minute, double z, string correlation) =>
        new("E-02", "A", "escTemperature", "periodic", SpcSeries.Start.AddMinutes(minute), z, z, 0d, 1d, null, null, correlation, null);
}
