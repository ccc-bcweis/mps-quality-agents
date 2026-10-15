// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Faults;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Application.Tests.Fakes;
using Mps.Domain.Equipment;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;

namespace Mps.Application.Tests.Faults;

public sealed class FaultInjectionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task InjectRecordsTheFaultAndDoesNotOpenATicket()
    {
        var fixture = new Fixture();

        var accepted = await fixture.InjectAsync(1.5d);

        accepted.FaultId.Should().NotBeEmpty();
        accepted.CorrelationId.Should().StartWith("fault-");
        accepted.IncidentId.Should().Be(Guid.Empty);
        fixture.Faults.Records.Should().ContainSingle();
    }

    [Fact]
    public async Task ALargeMagnitudeStillOnlyRecordsTheFault()
    {
        var fixture = new Fixture();

        var accepted = await fixture.InjectAsync(4.5d);

        accepted.IncidentId.Should().Be(Guid.Empty);
        fixture.Faults.Records.Should().ContainSingle();
        fixture.Faults.Records[0].Parameter.Should().Be("gasFlow");
        fixture.Faults.Records[0].Magnitude.Should().Be(4.5d);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Faults = new InMemoryFaultLog();
            var fleet = new InMemoryFleetCatalog();
            fleet.Add(new Tool(new ToolId("E-02"), "Etch 02", ToolFamily.Etch, "MPS-E7", "Bay-2", [new Chamber("A", "Chamber A")]));
            var clock = new FixedClock(Now);
            Service = new FaultInjectionService(fleet, Faults, NullQualityNotifier.Instance, clock);
        }

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
