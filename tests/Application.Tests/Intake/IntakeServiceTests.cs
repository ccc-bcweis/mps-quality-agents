// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Errors;
using Mps.Application.Intake;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Application.Tests.Fakes;
using Mps.Domain.Equipment;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;

namespace Mps.Application.Tests.Intake;

public sealed class IntakeServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task IotSpcAlertNormalizesToAnIncidentAndTicket()
    {
        var fixture = new Fixture();

        var result = await fixture.Accept(IncidentChannel.IotSpcAlert, "E-02", "escTemperature", "Chamber temperature is beyond 3 sigma.", Severity.High);

        result.LinkedToExisting.Should().BeFalse();
        result.Incident.Channel.Should().Be(IncidentChannel.IotSpcAlert);
        result.Incident.Severity.Should().Be(Severity.High);
        result.Incident.ToolId.Should().Be(new ToolId("E-02"));
        result.Ticket.Should().NotBeNull();
        result.Ticket!.Number.Should().StartWith("TKT-");
        result.Ticket.IncidentId.Should().Be(result.Incident.Id);
    }

    [Fact]
    public async Task OperatorReportNormalizesToAnIncident()
    {
        var fixture = new Fixture();

        var result = await fixture.Accept(IncidentChannel.OperatorReport, "D-01", "chamberPressure", "Pressure stepped up after the recipe start.", null);

        result.Incident.Channel.Should().Be(IncidentChannel.OperatorReport);
        result.Incident.Severity.Should().Be(Severity.Medium);
        result.Ticket.Should().NotBeNull();
    }

    [Fact]
    public async Task QmsNonconformanceNormalizesToAnIncident()
    {
        var fixture = new Fixture();

        var result = await fixture.Accept(IncidentChannel.QmsNonconformance, "S-01", "ashRate", "NCR-441 ash rate below the recipe window.", null);

        result.Incident.Channel.Should().Be(IncidentChannel.QmsNonconformance);
        result.Incident.Severity.Should().Be(Severity.High);
        result.Ticket.Should().NotBeNull();
    }

    [Fact]
    public async Task SupplierQualityNoticeNormalizesToAnIncident()
    {
        var fixture = new Fixture();

        var result = await fixture.Accept(
            IncidentChannel.SupplierQualityNotice,
            "E-01",
            "gasFlow",
            "Gas lot NF-3317 moisture is out of spec.",
            null);

        result.Incident.Channel.Should().Be(IncidentChannel.SupplierQualityNotice);
        result.Incident.Severity.Should().Be(Severity.High);
        result.Incident.Parameter.Should().Be("gasFlow");
        result.Ticket.Should().NotBeNull();
    }

    [Fact]
    public async Task FaultInjectorNormalizesToAnIncident()
    {
        var fixture = new Fixture();

        var result = await fixture.Accept(IncidentChannel.FaultInjector, "C-02", "particles", "Injected particle excursion on the clean tool.", null);

        result.Incident.Channel.Should().Be(IncidentChannel.FaultInjector);
        result.Incident.Severity.Should().Be(Severity.High);
        result.Ticket.Should().NotBeNull();
        result.EscalationRequired.Should().BeFalse();
    }

    [Fact]
    public async Task ASecondOpenReportOnTheSameToolAndParameterLinksToTheExistingIncident()
    {
        var fixture = new Fixture();
        var first = await fixture.Accept(IncidentChannel.OperatorReport, "E-02", "escTemperature", "First report.", Severity.Medium);

        var second = await fixture.Accept(IncidentChannel.QmsNonconformance, "E-02", "ESCTemperature", "Same signal from the QMS.", Severity.High);

        second.LinkedToExisting.Should().BeTrue();
        second.Incident.Id.Should().Be(first.Incident.Id);
        second.Ticket.Should().BeNull();
        fixture.Ticketing.Tickets.Should().ContainSingle();
        fixture.Incidents.Incidents.Should().ContainSingle();
    }

    [Fact]
    public async Task AClosedIncidentDoesNotAbsorbANewReport()
    {
        var fixture = new Fixture();
        var first = await fixture.Accept(IncidentChannel.OperatorReport, "E-02", "escTemperature", "First report.", Severity.Low);
        first.Incident.Advance(IncidentStage.Investigating);
        first.Incident.Advance(IncidentStage.RecommendationReady);
        first.Incident.Advance(IncidentStage.AwaitingApproval);
        first.Incident.Advance(IncidentStage.Approved);
        first.Incident.Advance(IncidentStage.Closed);

        var second = await fixture.Accept(IncidentChannel.OperatorReport, "E-02", "escTemperature", "It came back.", Severity.Medium);

        second.LinkedToExisting.Should().BeFalse();
        second.Incident.Id.Should().NotBe(first.Incident.Id);
        fixture.Ticketing.Tickets.Should().HaveCount(2);
    }

    [Fact]
    public async Task ADifferentParameterOpensASeparateIncident()
    {
        var fixture = new Fixture();
        await fixture.Accept(IncidentChannel.OperatorReport, "E-02", "escTemperature", "Temperature.", Severity.Medium);

        var other = await fixture.Accept(IncidentChannel.OperatorReport, "E-02", "chamberPressure", "Pressure.", Severity.Medium);

        other.LinkedToExisting.Should().BeFalse();
        fixture.Incidents.Incidents.Should().HaveCount(2);
    }

    [Fact]
    public async Task ANonconformanceRequestedAsLowIsRaisedToHigh()
    {
        var fixture = new Fixture();

        var result = await fixture.Accept(IncidentChannel.QmsNonconformance, "E-02", "etchRate", "Etch rate miss.", Severity.Low);

        result.Incident.Severity.Should().Be(Severity.High);
        result.EscalationRequired.Should().BeFalse();
    }

    [Fact]
    public async Task CriticalSeverityForcesEscalation()
    {
        var fixture = new Fixture();

        var result = await fixture.Accept(IncidentChannel.OperatorReport, "E-02", "escTemperature", "Safety interlock.", Severity.Critical);

        result.Incident.Severity.Should().Be(Severity.Critical);
        result.EscalationRequired.Should().BeTrue();
        fixture.Incidents.Incidents.Should().ContainSingle().Which.EscalationRequired.Should().BeTrue();
    }

    [Fact]
    public async Task AnUnknownToolIsRejected()
    {
        var fixture = new Fixture();

        var act = () => fixture.Accept(IncidentChannel.OperatorReport, "Z-99", "escTemperature", "Unknown tool.", Severity.Medium);

        await act.Should().ThrowAsync<IntakeValidationException>().WithMessage("*Tool was not found*");
        fixture.Incidents.Incidents.Should().BeEmpty();
    }

    [Fact]
    public async Task AnOversizedSummaryIsRejected()
    {
        var fixture = new Fixture();
        var summary = new string('a', IntakeService.MaxSummaryLength + 1);

        var act = () => fixture.Accept(IncidentChannel.OperatorReport, "E-02", "escTemperature", summary, Severity.Medium);

        await act.Should().ThrowAsync<IntakeValidationException>().WithMessage("*too long*");
        fixture.Ticketing.Tickets.Should().BeEmpty();
    }

    [Fact]
    public async Task ASummaryWithANullCharacterIsRejected()
    {
        var fixture = new Fixture();

        var act = () => fixture.Accept(IncidentChannel.OperatorReport, "E-02", "escTemperature", "stop\0here", Severity.Medium);

        await act.Should().ThrowAsync<IntakeValidationException>().WithMessage("*control characters*");
        fixture.Incidents.Incidents.Should().BeEmpty();
    }

    [Fact]
    public async Task AMissingParameterIsRejected()
    {
        var fixture = new Fixture();

        var act = () => fixture.Accept(IncidentChannel.FaultInjector, "E-02", "  ", "No parameter.", Severity.High);

        await act.Should().ThrowAsync<IntakeValidationException>().WithMessage("*Parameter*");
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Incidents = new InMemoryIncidentLog();
            Ticketing = new InMemoryTicketingPort();
            var fleet = new InMemoryFleetCatalog();
            foreach (var tool in DemoTools())
            {
                fleet.Add(tool);
            }

            Service = new IntakeService(fleet, Incidents, Ticketing, NullQualityNotifier.Instance, new FixedTime(Now));
        }

        public InMemoryIncidentLog Incidents { get; }

        public InMemoryTicketingPort Ticketing { get; }

        public IntakeService Service { get; }

        public Task<IntakeResult> Accept(
            IncidentChannel channel,
            string toolId,
            string parameter,
            string summary,
            Severity? severity)
        {
            return Service.AcceptAsync(
                new IntakeSubmission(channel, toolId, "A", parameter, summary, severity, signalSeverity: null, correlationId: null, occurredAt: null),
                CancellationToken.None);
        }

        private static IEnumerable<Tool> DemoTools()
        {
            yield return ToolOf("D-01", "Deposition 01", ToolFamily.Deposition, "MPS-D7", "Bay-1");
            yield return ToolOf("E-01", "Etch 01", ToolFamily.Etch, "MPS-E7", "Bay-1");
            yield return ToolOf("E-02", "Etch 02", ToolFamily.Etch, "MPS-E7", "Bay-2");
            yield return ToolOf("S-01", "Strip 01", ToolFamily.Strip, "MPS-S7", "Bay-1");
            yield return ToolOf("C-02", "Clean 02", ToolFamily.Clean, "MPS-C7", "Bay-2");
        }

        private static Tool ToolOf(string id, string name, ToolFamily family, string model, string bay) =>
            new(new ToolId(id), name, family, model, bay, [new Chamber("A", "Chamber A")]);
    }

    private sealed class FixedTime : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTime(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
