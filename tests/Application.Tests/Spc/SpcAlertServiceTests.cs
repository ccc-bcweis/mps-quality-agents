// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Intake;
using Mps.Application.Notifications;
using Mps.Application.Spc;
using Mps.Application.Tests.Fakes;
using Mps.Application.Tests.Metadata;
using Mps.Domain.Equipment;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Telemetry;

namespace Mps.Application.Tests.Spc;

public sealed class SpcAlertServiceTests
{
    [Fact]
    public async Task AHighRuleOpensOneAlertAndOneTicket()
    {
        var fixture = new Fixture();

        var outcome = await fixture.Service.RaiseAsync(SpcSeries.Sample(0d, 3.5d), CancellationToken.None);

        outcome.Debounced.Should().BeFalse();
        outcome.Alert.Should().NotBeNull();
        outcome.Alert!.Rule.Should().Contain(SpcRuleNames.WesternElectric1);
        outcome.Alert.Severity.Should().Be(Severity.High);
        outcome.Ticket.Should().NotBeNull();
        outcome.Ticket!.Number.Should().StartWith("TKT-");
        outcome.Incident.Should().NotBeNull();
        outcome.Incident!.Channel.Should().Be(IncidentChannel.IotSpcAlert);
        outcome.Incident.Stage.Should().Be(IncidentStage.TicketOpened);
        outcome.Ticket.IncidentId.Should().Be(outcome.Incident.Id);
        fixture.Ticketing.Tickets.Should().ContainSingle();
    }

    [Fact]
    public async Task ALowTrendAlertsWithoutOpeningATicket()
    {
        var fixture = new Fixture();
        var baseline = new ControlBaseline(0d, 10d, null, null);
        var sample = new SpcSample(
            SpcSeries.Tool,
            "A",
            "rfHours",
            baseline,
            SpcSeries.Values(0d, 0.1d, 0.2d, 0.3d, 0.4d, 0.5d));

        var outcome = await fixture.Service.RaiseAsync(sample, CancellationToken.None);

        outcome.Alert.Should().NotBeNull();
        outcome.Alert!.Severity.Should().Be(Severity.Low);
        outcome.Alert.Rule.Should().Be(SpcRuleNames.NelsonSixRising);
        outcome.Ticket.Should().BeNull();
        outcome.Incident.Should().BeNull();
        fixture.Ticketing.Tickets.Should().BeEmpty();
    }

    [Fact]
    public async Task InControlSamplesDoNotAlert()
    {
        var fixture = new Fixture();

        var outcome = await fixture.Service.RaiseAsync(
            SpcSeries.Sample(0.2, -0.1, 0.4, -0.3, 0.1),
            CancellationToken.None);

        outcome.Violations.Should().BeEmpty();
        outcome.Alert.Should().BeNull();
        outcome.Ticket.Should().BeNull();
    }

    [Fact]
    public async Task ARepeatInsideTheDebounceWindowDoesNotOpenAnotherAlertOrTicket()
    {
        var fixture = new Fixture();
        var sample = SpcSeries.Sample(0d, 3.5d);

        var first = await fixture.Service.RaiseAsync(sample, CancellationToken.None);
        var second = await fixture.Service.RaiseAsync(sample, CancellationToken.None);

        first.Ticket.Should().NotBeNull();
        second.Debounced.Should().BeTrue();
        second.Alert.Should().BeNull();
        second.Ticket.Should().BeNull();
        fixture.Alerts.Alerts.Should().ContainSingle();
        fixture.Ticketing.Tickets.Should().ContainSingle();
    }

    [Fact]
    public async Task AnotherParameterIsNotDebouncedWithTheFirst()
    {
        var fixture = new Fixture();
        var pressure = new SpcSample(SpcSeries.Tool, "A", "chamberPressure", SpcSeries.Unit, SpcSeries.Values(4d));
        var temperature = new SpcSample(SpcSeries.Tool, "A", "escTemperature", SpcSeries.Unit, SpcSeries.Values(4d));

        await fixture.Service.RaiseAsync(pressure, CancellationToken.None);
        var second = await fixture.Service.RaiseAsync(temperature, CancellationToken.None);

        second.Debounced.Should().BeFalse();
        second.Ticket.Should().NotBeNull();
        fixture.Alerts.Alerts.Should().HaveCount(2);
    }

    [Fact]
    public async Task AnExcursionAfterTheDebounceWindowOpensAnotherAlert()
    {
        var fixture = new Fixture();
        var firstSample = SpcSeries.Sample(4d);
        var later = new SpcSample(
            SpcSeries.Tool,
            "A",
            "escTemperature",
            SpcSeries.Unit,
            SpcSeries.Shifted(TimeSpan.FromMinutes(10), 4d));

        await fixture.Service.RaiseAsync(firstSample, CancellationToken.None);
        var second = await fixture.Service.RaiseAsync(later, CancellationToken.None);

        second.Debounced.Should().BeFalse();
        second.Alert.Should().NotBeNull();
        fixture.Alerts.Alerts.Should().HaveCount(2);
    }

    [Fact]
    public async Task ASpecWindowBreachAboveTheThresholdOpensATicket()
    {
        var fixture = new Fixture();
        var baseline = new ControlBaseline(50d, 5d, 40d, 60d);
        var sample = new SpcSample(SpcSeries.Tool, null, "chamberPressure", baseline, SpcSeries.Values(50d, 61d));

        var outcome = await fixture.Service.RaiseAsync(sample, CancellationToken.None);

        outcome.Alert.Should().NotBeNull();
        outcome.Alert!.Rule.Should().Contain(SpcRuleNames.Window);
        outcome.Ticket.Should().NotBeNull();
        outcome.Incident!.Channel.Should().Be(IncidentChannel.IotSpcAlert);
    }

    [Fact]
    public async Task AnAlertClearsAfterEightInControlReadingsInARow()
    {
        var fixture = new Fixture();
        var raised = await fixture.Service.RaiseAsync(SpcSeries.Sample(0d, 4d), CancellationToken.None);
        raised.Alert.Should().NotBeNull();
        raised.Alert!.IsActive.Should().BeTrue();

        var seven = new SpcSample(SpcSeries.Tool, "A", "escTemperature", SpcSeries.Unit, SpcSeries.Shifted(TimeSpan.FromMinutes(2), 0d, 0.1d, -0.2d, 0.3d, 0d, 0.1d, -0.1d));
        await fixture.Service.RaiseAsync(seven, CancellationToken.None);
        fixture.Alerts.Alerts.Single().IsActive.Should().BeTrue();

        var eight = new SpcSample(SpcSeries.Tool, "A", "escTemperature", SpcSeries.Unit, SpcSeries.Shifted(TimeSpan.FromMinutes(2), 0d, 0.1d, -0.2d, 0.3d, 0d, 0.1d, -0.1d, 0.2d));
        await fixture.Service.RaiseAsync(eight, CancellationToken.None);
        var alert = fixture.Alerts.Alerts.Single();
        alert.IsActive.Should().BeFalse();
        alert.Status.Should().Be("cleared");
        alert.ClearedAt.Should().Be(SpcSeries.Start.AddMinutes(9));
    }

    [Fact]
    public async Task AClearedPointDoesNotRaiseAgainFromALaterWindow()
    {
        // A point alerts once. After the alert clears, a window that still holds the old breaking point must not reopen it.
        var fixture = new Fixture();
        await fixture.Service.RaiseAsync(SpcSeries.Sample(0d, 4d), CancellationToken.None);
        var nominal = SpcSeries.Shifted(TimeSpan.FromMinutes(2), 0d, 0.1d, -0.2d, 0.3d, 0d, 0.1d, -0.1d, 0.2d);
        await fixture.Service.RaiseAsync(new SpcSample(SpcSeries.Tool, "A", "escTemperature", SpcSeries.Unit, nominal), CancellationToken.None);
        fixture.Alerts.Alerts.Single().IsActive.Should().BeFalse();

        var window = SpcSeries.Values(0d, 4d).Concat(nominal).ToArray();
        await fixture.Service.RaiseAsync(new SpcSample(SpcSeries.Tool, "A", "escTemperature", SpcSeries.Unit, window), CancellationToken.None);

        fixture.Alerts.Alerts.Should().ContainSingle().Which.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SlowDriftLikeSc4At10xStaysActiveEscalatesToAlarmAndOpensATicket()
    {
        // SC-4: etch rate ramps down about 3.5 sigma, 15-second samples, the publisher sends the last 20 points each tick.
        var fixture = new Fixture();
        var values = new List<double>();
        for (var index = 0; index < 120; index++)
        {
            var ramp = -3.5d * Math.Min(1d, index / 90d);
            values.Add(ramp + (index % 2 == 0 ? 0.15d : -0.15d));
        }

        var raisedAny = false;
        for (var count = 1; count <= values.Count; count++)
        {
            var points = values.Take(count).Select((value, index) => new TelemetryPoint(SpcSeries.Start.AddSeconds(15 * index), value)).ToArray();
            var sample = new SpcSample(SpcSeries.Tool, "A", "etchRate", SpcSeries.Unit, points.Skip(Math.Max(0, points.Length - 20)).ToArray());
            await fixture.Service.RaiseAsync(sample, CancellationToken.None);
            raisedAny |= fixture.Alerts.Alerts.Count > 0;
            if (raisedAny)
            {
                fixture.Alerts.Alerts.Should().Contain(alert => alert.IsActive, $"the drift is still out of control at point {count}");
                fixture.Alerts.Alerts.Should().OnlyContain(alert => alert.IsActive, $"nothing clears mid-drift (point {count})");
            }
        }

        fixture.Alerts.Alerts.Should().Contain(alert => alert.Severity >= Severity.High, "crossing 3 sigma escalates the watch to an alarm");
        (await fixture.Incidents.ListAsync(CancellationToken.None)).Should().NotBeEmpty("an alarm opens an incident for investigation");
        fixture.Ticketing.Tickets.Should().NotBeEmpty("an alarm opens a ticket");

        // The fault ends: back to center with noise for a sustained run, then every alert clears.
        for (var index = 0; index < 20; index++)
        {
            values.Add(index % 2 == 0 ? 0.2d : -0.2d);
        }

        var all = values.Select((value, index) => new TelemetryPoint(SpcSeries.Start.AddSeconds(15 * index), value)).ToArray();
        await fixture.Service.RaiseAsync(new SpcSample(SpcSeries.Tool, "A", "etchRate", SpcSeries.Unit, all.Skip(all.Length - 20).ToArray()), CancellationToken.None);
        fixture.Alerts.Alerts.Should().OnlyContain(alert => !alert.IsActive);
    }

    [Fact]
    public async Task EightReadingsOnOneSideInsideTwoSigmaDoNotClearAnAlert()
    {
        var fixture = new Fixture();
        await fixture.Service.RaiseAsync(SpcSeries.Sample(0d, 4d), CancellationToken.None);
        var oneSide = new SpcSample(SpcSeries.Tool, "A", "escTemperature", SpcSeries.Unit, SpcSeries.Shifted(TimeSpan.FromMinutes(2), -0.5d, -0.6d, -0.4d, -0.7d, -0.5d, -0.6d, -0.8d, -0.5d, -0.6d));
        await fixture.Service.ClearRecoveredAsync(oneSide, CancellationToken.None);
        fixture.Alerts.Alerts.Should().Contain(alert => alert.IsActive);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Alerts = new InMemoryAlertLog();
            Incidents = new InMemoryIncidentLog();
            Ticketing = new InMemoryTicketingPort();
            var fleet = new InMemoryFleetCatalog();
            fleet.Add(new Tool(SpcSeries.Tool, "Etch 02", ToolFamily.Etch, "MPS-E7", "Bay-2", [new Chamber("A", "Chamber A")]));
            var intake = new IntakeService(fleet, Incidents, Ticketing, NullQualityNotifier.Instance, TimeProvider.System, DemoRules.Engine);
            Service = new SpcAlertService(Alerts, intake, NullQualityNotifier.Instance, SpcAlertService.DefaultDebounce, DemoRules.Engine);
        }

        public InMemoryAlertLog Alerts { get; }

        public InMemoryIncidentLog Incidents { get; }

        public InMemoryTicketingPort Ticketing { get; }

        public SpcAlertService Service { get; }
    }
}
