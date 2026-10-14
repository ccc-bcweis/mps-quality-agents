// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Faults;
using Mps.Application.Intake;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Application.Spc;
using Mps.Application.Tests.Fakes;
using Mps.Domain.Equipment;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;

namespace Mps.Application.Tests.Faults;

public sealed class FaultInjectionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DirectTicketDefaultsOffAndStillRecordsTheFault()
    {
        var fixture = new Fixture(directTicket: false);

        var accepted = await fixture.InjectAsync(4.5d);

        accepted.FaultId.Should().NotBeEmpty();
        accepted.CorrelationId.Should().StartWith("fault-");
        accepted.IncidentId.Should().Be(Guid.Empty);
        fixture.Faults.Records.Should().ContainSingle();
        fixture.Incidents.Incidents.Should().BeEmpty();
        fixture.Tickets.Tickets.Should().BeEmpty();
        fixture.Alerts.Alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task DirectTicketOpensAnIncidentAndALargeMagnitudeRaisesAnAlert()
    {
        var fixture = new Fixture(directTicket: true);

        var accepted = await fixture.InjectAsync(4.5d);

        accepted.IncidentId.Should().NotBeEmpty();
        fixture.Incidents.Incidents.Should().ContainSingle();
        fixture.Incidents.Incidents[0].Incident.Channel.Should().Be(IncidentChannel.FaultInjector);
        fixture.Incidents.Incidents[0].Incident.Id.Value.Should().Be(accepted.IncidentId);
        fixture.Tickets.Tickets.Should().ContainSingle();
        fixture.Alerts.Alerts.Should().ContainSingle();
        fixture.Alerts.Alerts[0].Parameter.Should().Be("gasFlow");
    }

    private sealed class Fixture
    {
        public Fixture(bool directTicket)
        {
            Incidents = new InMemoryIncidentLog();
            Tickets = new InMemoryTicketingPort();
            Alerts = new InMemoryAlertLog();
            Faults = new InMemoryFaultLog();
            var fleet = new InMemoryFleetCatalog();
            fleet.Add(new Tool(new ToolId("E-02"), "Etch 02", ToolFamily.Etch, "MPS-E7", "Bay-2", [new Chamber("A", "Chamber A")]));
            var clock = new FixedClock(Now);
            var intake = new IntakeService(fleet, Incidents, Tickets, NullQualityNotifier.Instance, clock);
            var spc = new SpcAlertService(Alerts, intake, NullQualityNotifier.Instance, SpcAlertService.DefaultDebounce);
            Service = new FaultInjectionService(
                fleet,
                Faults,
                intake,
                spc,
                NullQualityNotifier.Instance,
                clock,
                new InjectorPolicy(directTicket));
        }

        public InMemoryIncidentLog Incidents { get; }

        public InMemoryTicketingPort Tickets { get; }

        public InMemoryAlertLog Alerts { get; }

        public InMemoryFaultLog Faults { get; }

        public FaultInjectionService Service { get; }

        public Task<FaultAccepted> InjectAsync(double magnitude) =>
            Service.InjectAsync(
                new FaultInjectionRequest("E-02", "A", "gasFlow", "step", magnitude, RootCauseCategory.SupplyChainMaterial, true),
                CancellationToken.None);
    }

    private sealed class InMemoryFaultLog : IFaultLog
    {
        public List<FaultRecord> Records { get; } = [];

        public Task AddAsync(FaultRecord fault, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(fault);
            _ = cancellationToken;
            Records.Add(fault);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedClock(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
