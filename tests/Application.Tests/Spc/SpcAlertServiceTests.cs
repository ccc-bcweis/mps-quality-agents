// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Intake;
using Mps.Application.Notifications;
using Mps.Application.Spc;
using Mps.Application.Tests.Fakes;
using Mps.Application.Tests.Metadata;
using Mps.Domain.Equipment;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;

namespace Mps.Application.Tests.Spc;

public sealed class SpcAlertServiceTests
{
    [Fact]
    public async Task AHighRuleOpensOneAlertAndOneTicket()
    {
        var drive = new SpcDrive();

        await drive.PushAsync("escTemperature", 0, 0d);
        var outcome = await drive.PushAsync("escTemperature", 1, 3.5d);

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
        drive.Ticketing.Tickets.Should().ContainSingle();
    }

    [Fact]
    public async Task ALowTrendAlertsWithoutOpeningATicket()
    {
        var drive = new SpcDrive();
        SpcAlertOutcome? outcome = null;
        var index = 0;
        foreach (var value in new[] { 0d, 0.4d, 0.8d, 1.2d, 1.6d, 2.0d })
        {
            outcome = await drive.PushAsync("rfHours", index, value);
            index++;
        }

        outcome!.Alert.Should().NotBeNull();
        outcome.Alert!.Severity.Should().Be(Severity.Low);
        outcome.Alert.Rule.Should().Contain(SpcRuleNames.NelsonSixRising);
        outcome.Ticket.Should().BeNull();
        outcome.Incident.Should().BeNull();
        drive.Ticketing.Tickets.Should().BeEmpty();
    }

    [Fact]
    public async Task InControlSamplesDoNotAlert()
    {
        var drive = new SpcDrive();
        foreach (var value in new[] { 0.2d, -0.1d, 0.4d, -0.3d, 0.1d })
        {
            var outcome = await drive.PushAsync("escTemperature", drive.Next, value);
            outcome.Alert.Should().BeNull();
            outcome.Ticket.Should().BeNull();
        }

        drive.Alerts.Alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task ARepeatInsideTheDebounceWindowDoesNotOpenAnotherAlertOrTicket()
    {
        var drive = new SpcDrive();
        var first = await drive.PushAsync("escTemperature", 1, 3.5d);
        var second = await drive.PushAsync("escTemperature", 2, 3.5d);

        first.Ticket.Should().NotBeNull();
        second.Alert.Should().BeNull();
        second.Ticket.Should().BeNull();
        drive.Alerts.Alerts.Should().ContainSingle();
        drive.Ticketing.Tickets.Should().ContainSingle();
    }

    [Fact]
    public async Task AnotherParameterIsNotDebouncedWithTheFirst()
    {
        var drive = new SpcDrive();
        await drive.PushAsync("chamberPressure", 0, 4d);
        var second = await drive.PushAsync("escTemperature", 0, 4d);

        second.Debounced.Should().BeFalse();
        second.Ticket.Should().NotBeNull();
        drive.Alerts.Alerts.Should().HaveCount(2);
    }

    [Fact]
    public async Task ALaterPointOnTheSameRuleDoesNotOpenAnotherTicket()
    {
        var drive = new SpcDrive();
        var first = await drive.PushAsync("escTemperature", 0, 4d);
        var second = await drive.PushAsync("escTemperature", 10, 4d);

        second.Ticket.Should().BeNull();
        var alert = drive.Alerts.Alerts.Should().ContainSingle().Subject;
        alert.Id.Should().Be(first.Alert!.Id);
        alert.IsActive.Should().BeTrue();
        drive.Ticketing.Tickets.Should().ContainSingle();
    }

    [Fact]
    public async Task ASustainedStepKeepsExactlyOneAlertPerSignal()
    {
        var drive = new SpcDrive();
        for (var index = 0; index < 40; index++)
        {
            await drive.PushAsync("cf4Flow", index, -4d + (index % 2 == 0 ? 0.2d : -0.2d));
        }

        var alert = drive.Alerts.Alerts.Should().ContainSingle("one signal in one sustained condition is one alert").Subject;
        alert.IsActive.Should().BeTrue();
        alert.Severity.Should().BeOneOf(Severity.High, Severity.Critical);
        alert.Rule.Split('+').Should().OnlyHaveUniqueItems();
        alert.Occurrences.Should().BeLessThan(20, "the store is written when the rule state changes, not on every reading");
        drive.Ticketing.Tickets.Should().ContainSingle();
    }

    [Fact]
    public async Task AWatchThatEscalatesToAlarmUpgradesTheSameAlertAndOpensTheTicket()
    {
        var drive = new SpcDrive();
        SpcAlertOutcome? watch = null;
        var index = 0;
        foreach (var value in new[] { 0d, 0.4d, 0.8d, 1.2d, 1.6d, 2.0d })
        {
            watch = await drive.PushAsync("escTemperature", index, value);
            index++;
        }

        watch!.Alert!.Severity.Should().Be(Severity.Low);
        drive.Ticketing.Tickets.Should().BeEmpty();

        var alarm = await drive.PushAsync("escTemperature", index, 4.5d);

        var alert = drive.Alerts.Alerts.Should().ContainSingle().Subject;
        alert.Id.Should().Be(watch.Alert.Id);
        alert.Severity.Should().BeOneOf(Severity.High, Severity.Critical);
        alert.Rule.Should().Contain(SpcRuleNames.NelsonSixRising).And.Contain(SpcRuleNames.WesternElectric1);
        alarm.Ticket.Should().NotBeNull("an escalation to alarm opens the ticket, as a new alarm would");
        drive.Ticketing.Tickets.Should().ContainSingle();
    }

    [Fact]
    public async Task AClearFollowedByANewViolationRaisesASecondAlert()
    {
        var drive = new SpcDrive();
        await drive.PushAsync("escTemperature", 0, 4d);
        await drive.RecoverAsync("escTemperature", 1);
        drive.Alerts.Alerts.Single().IsActive.Should().BeFalse();

        var outcome = await drive.PushAsync("escTemperature", 30, 4.2d);

        outcome.Alert.Should().NotBeNull();
        drive.Alerts.Alerts.Should().HaveCount(2);
        drive.Alerts.Alerts.Count(item => item.IsActive).Should().Be(1);
    }

    [Fact]
    public async Task ASpecWindowBreachAboveTheThresholdOpensATicket()
    {
        var drive = new SpcDrive();
        await drive.PushAsync("chamberPressure", 0, 0d, raw: 50d, center: 50d, sigma: 5d, specLow: 40d, specHigh: 60d, chamber: null);
        var outcome = await drive.PushAsync("chamberPressure", 1, 2.2d, raw: 61d, center: 50d, sigma: 5d, specLow: 40d, specHigh: 60d, chamber: null);

        outcome.Alert.Should().NotBeNull();
        outcome.Alert!.Rule.Should().Contain(SpcRuleNames.Window);
        outcome.Ticket.Should().NotBeNull();
        outcome.Incident!.Channel.Should().Be(IncidentChannel.IotSpcAlert);
    }

    [Fact]
    public async Task AnAlertClearsAfterEightInControlReadingsInARow()
    {
        var drive = new SpcDrive();
        var raised = await drive.PushAsync("escTemperature", 0, 4d);
        raised.Alert.Should().NotBeNull();
        raised.Alert!.IsActive.Should().BeTrue();

        await drive.PushAsync("escTemperature", 1, 0d);
        await drive.PushAsync("escTemperature", 2, 0.1d);
        await drive.PushAsync("escTemperature", 3, -0.2d);
        await drive.PushAsync("escTemperature", 4, 0.3d);
        await drive.PushAsync("escTemperature", 5, 0d);
        await drive.PushAsync("escTemperature", 6, 0.1d);
        await drive.PushAsync("escTemperature", 7, -0.1d);
        drive.Alerts.Alerts.Single().IsActive.Should().BeTrue();

        var cleared = await drive.PushAsync("escTemperature", 8, 0.2d);
        var alert = drive.Alerts.Alerts.Single();
        alert.IsActive.Should().BeFalse();
        alert.Status.Should().Be("cleared");
        alert.ClearedAt.Should().Be(SpcSeries.Start.AddMinutes(8));
        cleared.Alert.Should().BeSameAs(alert);
    }

    [Fact]
    public async Task AClearedSeriesStaysClearUntilANewViolation()
    {
        var drive = new SpcDrive();
        await drive.PushAsync("escTemperature", 0, 4d);
        await drive.RecoverAsync("escTemperature", 1);
        await drive.PushAsync("escTemperature", 20, 0.1d);
        await drive.PushAsync("escTemperature", 21, -0.1d);

        drive.Alerts.Alerts.Should().ContainSingle().Which.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task AnAlertClearsAfterARestartWhenNominalReadingsArrive()
    {
        var drive = new SpcDrive();
        await drive.PushAsync("escTemperature", 0, 4d);
        var stored = drive.Alerts.Alerts.Should().ContainSingle().Subject;
        stored.IsActive.Should().BeTrue();

        var restarted = new SpcRuleEngine(drive.Settings);
        restarted.Rebuild(
        [
            new SpcSeriesSeed(stored.ToolId.ToString(), stored.ChamberId, stored.Parameter, stored.Severity, stored.Rule, stored.CorrelationId, stored.LastSeenAt),
        ]);
        for (var index = 1; index <= 8; index++)
        {
            var observation = restarted.Observe(SpcDrive.Reading("escTemperature", index, index % 2 == 0 ? 0.1d : -0.1d));
            if (observation is not null && observation.Action != SpcAction.None)
            {
                await drive.Service.ApplyAsync(observation, null, CancellationToken.None);
            }
        }

        stored.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SlowDriftInsideAndPastThreeSigmaEscalatesAndThenClears()
    {
        var drive = new SpcDrive();
        for (var index = 0; index < 120; index++)
        {
            var ramp = -3.5d * Math.Min(1d, index / 90d);
            await drive.PushAsync("etchRate", index, ramp + (index % 2 == 0 ? 0.15d : -0.15d));
        }

        drive.Alerts.Alerts.Should().Contain(alert => alert.IsActive && alert.Severity >= Severity.High);
        drive.Ticketing.Tickets.Should().NotBeEmpty();

        for (var index = 120; index < 140; index++)
        {
            await drive.PushAsync("etchRate", index, index % 2 == 0 ? 0.2d : -0.2d);
        }

        drive.Alerts.Alerts.Should().OnlyContain(alert => !alert.IsActive);
    }

    [Fact]
    public async Task EightReadingsOnOneSideInsideTwoSigmaDoNotClearAnAlert()
    {
        var drive = new SpcDrive();
        await drive.PushAsync("escTemperature", 0, 4d);
        for (var index = 1; index <= 8; index++)
        {
            await drive.PushAsync("escTemperature", index, -1d);
        }

        drive.Alerts.Alerts.Should().Contain(alert => alert.IsActive);
    }

    [Fact]
    public async Task AnUnchangedActiveAlertIsNotBroadcastAgainOnEveryReading()
    {
        var notifier = new CountingNotifier();
        var drive = new SpcDrive(notifier);
        for (var index = 0; index < 40; index++)
        {
            await drive.PushAsync("cf4Flow", index, -4d + (index % 2 == 0 ? 0.2d : -0.2d));
        }

        var alert = drive.Alerts.Alerts.Should().ContainSingle().Subject;
        alert.Occurrences.Should().BeLessThan(20);
        notifier.Alerts.Should().BeInRange(1, 8);
    }

    [Fact]
    public async Task ADriftInsideThreeSigmaIsCaughtByWe2We3AndWe4()
    {
        var drive = new SpcDrive();
        SpcAlertOutcome? last = null;
        for (var index = 0; index < 8; index++)
        {
            last = await drive.PushAsync("escTemperature", index, 2.2d);
        }

        var alert = drive.Alerts.Alerts.Should().ContainSingle().Subject;
        alert.Rule.Should().Contain(SpcRuleNames.WesternElectric2);
        alert.Rule.Should().Contain(SpcRuleNames.WesternElectric3);
        alert.Rule.Should().Contain(SpcRuleNames.WesternElectric4);
        alert.Rule.Should().NotContain(SpcRuleNames.WesternElectric1);
        alert.Severity.Should().Be(Severity.High);
        last.Should().NotBeNull();
        drive.Ticketing.Tickets.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AWatchExpiresAfterQuietReadings()
    {
        var settings = SpcRuleSettings.Shared.Copy();
        settings.WatchExpiry.Readings = 5;
        var drive = new SpcDrive(settings: settings);
        for (var index = 0; index < 8; index++)
        {
            await drive.PushAsync("escTemperature", index, 1d);
        }

        var watch = drive.Alerts.Alerts.Should().ContainSingle().Subject;
        watch.Severity.Should().Be(Severity.Medium);
        watch.IsActive.Should().BeTrue();

        for (var index = 8; index < 12; index++)
        {
            await drive.PushAsync("escTemperature", index, 0d);
        }

        watch.IsActive.Should().BeTrue();
        await drive.PushAsync("escTemperature", 12, 0d);
        watch.IsActive.Should().BeFalse();
        watch.ClearedAt.Should().Be(SpcSeries.Start.AddMinutes(12));
    }

    [Fact]
    public async Task TheHourlyTicketCapStillRaisesTheAlert()
    {
        var settings = SpcRuleSettings.Shared.Copy();
        settings.TicketCap.PerToolPerHour = 1;
        var drive = new SpcDrive(settings: settings, cap: new SpcTicketCap());

        var first = await drive.PushAsync("chamberPressure", 0, 4d);
        var second = await drive.PushAsync("escTemperature", 1, 4d);

        first.Ticket.Should().NotBeNull();
        second.Alert.Should().NotBeNull();
        second.Ticket.Should().BeNull();
        second.TicketSuppressed.Should().BeTrue();
        drive.Alerts.Alerts.Should().HaveCount(2);
        drive.Alerts.Alerts.Should().OnlyContain(alert => alert.IsActive);
        drive.Ticketing.Tickets.Should().ContainSingle();
    }

    [Fact]
    public async Task ADisabledParameterIsNotEvaluated()
    {
        var settings = SpcRuleSettings.Shared.Copy();
        settings.Sensors.DisabledParameters.Add("escTemperature");
        var drive = new SpcDrive(settings: settings);

        var outcome = await drive.PushAsync("escTemperature", 0, 4d);

        outcome.Alert.Should().BeNull();
        drive.Alerts.Alerts.Should().BeEmpty();
        drive.Engine.Evaluates("escTemperature", "periodic").Should().BeFalse();
        drive.Engine.Evaluates("chamberPressure", "periodic").Should().BeTrue();
        drive.Engine.Evaluates("waferCount", "cumulative").Should().BeFalse();
    }

    private sealed class CountingNotifier : IQualityNotifier
    {
        public int Alerts { get; private set; }

        public Task AlertRaisedAsync(Alert alert, CancellationToken cancellationToken)
        {
            Alerts++;
            return Task.CompletedTask;
        }

        public Task TicketOpenedAsync(Ticket ticket, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task IncidentChangedAsync(Incident incident, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task InvestigationChangedAsync(Mps.Domain.Investigations.InvestigationSnapshot snapshot, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task CapaChangedAsync(Mps.Domain.Capa.CapaAction capa, string eventName, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task FaultInjectedAsync(Guid faultId, string correlationId, string toolId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SpcDrive
    {
        private int _minute;

        public SpcDrive(IQualityNotifier? notifier = null, SpcRuleSettings? settings = null, SpcTicketCap? cap = null)
        {
            Settings = settings ?? SpcRuleSettings.Shared;
            Engine = new SpcRuleEngine(Settings);
            Alerts = new InMemoryAlertLog();
            Incidents = new InMemoryIncidentLog();
            Ticketing = new InMemoryTicketingPort();
            var fleet = new InMemoryFleetCatalog();
            fleet.Add(new Tool(SpcSeries.Tool, "Etch 02", ToolFamily.Etch, "MPS-E7", "Bay-2", [new Chamber("A", "Chamber A")]));
            var intake = new IntakeService(fleet, Incidents, Ticketing, NullQualityNotifier.Instance, TimeProvider.System, DemoRules.Engine);
            Service = new SpcAlertService(
                Alerts,
                intake,
                notifier ?? NullQualityNotifier.Instance,
                Settings.AlertDebounce,
                DemoRules.Engine,
                hints: null,
                events: null,
                ticketCap: cap,
                settings: Settings);
        }

        public SpcRuleSettings Settings { get; }

        public SpcRuleEngine Engine { get; }

        public InMemoryAlertLog Alerts { get; }

        public InMemoryIncidentLog Incidents { get; }

        public InMemoryTicketingPort Ticketing { get; }

        public SpcAlertService Service { get; }

        public int Next => _minute;

        public static SpcReading Reading(string parameter, int minute, double z, double? raw = null, double center = 0d, double sigma = 1d, double? specLow = null, double? specHigh = null, string? chamber = "A") =>
            new(
                SpcSeries.Tool.ToString(),
                chamber,
                parameter,
                "periodic",
                SpcSeries.Start.AddMinutes(minute),
                z,
                raw ?? (center + (z * sigma)),
                center,
                sigma,
                specLow,
                specHigh,
                "nominal-" + SpcSeries.Tool,
                null);

        public async Task<SpcAlertOutcome> PushAsync(
            string parameter,
            int minute,
            double z,
            double? raw = null,
            double center = 0d,
            double sigma = 1d,
            double? specLow = null,
            double? specHigh = null,
            string? chamber = "A")
        {
            _minute = minute + 1;
            var observation = Engine.Observe(Reading(parameter, minute, z, raw, center, sigma, specLow, specHigh, chamber));
            if (observation is null || observation.Action == SpcAction.None)
            {
                return new SpcAlertOutcome([], null, null, null, debounced: false);
            }

            var outcome = await Service.ApplyAsync(observation, null, CancellationToken.None);
            if (outcome.Debounced && observation.Action == SpcAction.Raise)
            {
                Engine.RevertRaise(SpcSeries.Tool.ToString(), chamber, parameter);
            }

            return outcome;
        }

        public async Task RecoverAsync(string parameter, int startMinute)
        {
            var values = new[] { 0d, 0.1d, -0.2d, 0.3d, 0d, 0.1d, -0.1d, 0.2d };
            for (var index = 0; index < values.Length; index++)
            {
                await PushAsync(parameter, startMinute + index, values[index]);
            }
        }
    }
}
