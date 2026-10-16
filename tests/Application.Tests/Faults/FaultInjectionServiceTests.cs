// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Mps.Application.Bus;
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

    [Theory]
    [InlineData(RootCauseCategory.EngineeringChange, "WO-7")]
    [InlineData(RootCauseCategory.EquipmentCalibrationOrAlignment, "LOT-AR-")]
    [InlineData(RootCauseCategory.SupplyChainMaterial, "ECO-6")]
    [InlineData(RootCauseCategory.ProcessDrift, "WO-8")]
    public async Task MisleadingRecordsOnWritesOneOrTwoRecordsThatFitTheCategory(RootCauseCategory category, string prefix)
    {
        var fixture = new Fixture();

        var accepted = await fixture.Service.InjectAsync(Request(category, confounders: true), CancellationToken.None);

        var ids = fixture.Board.ConfounderIds(accepted.FaultId);
        ids.Should().HaveCountGreaterThanOrEqualTo(1).And.HaveCountLessThanOrEqualTo(2);
        ids.Should().Contain(id => id.StartsWith(prefix, StringComparison.Ordinal));
        var payload = JsonSerializer.Serialize(fixture.Events.Published.Single().Payload);
        payload.Should().Contain("\"confounders\":true");
        foreach (var id in ids)
        {
            payload.Should().Contain(id, "the fault-injected event names the misleading records");
        }

        // A confounder is a business record only: still one fault, no extra signal, nothing that could raise an alert.
        fixture.Faults.Records.Should().ContainSingle();
    }

    [Fact]
    public async Task MisleadingRecordsOffWritesNone()
    {
        var fixture = new Fixture();

        var accepted = await fixture.Service.InjectAsync(Request(RootCauseCategory.EngineeringChange, confounders: false), CancellationToken.None);

        fixture.Board.All.Should().BeEmpty();
        fixture.Board.ConfounderIds(accepted.FaultId).Should().BeEmpty();
        JsonSerializer.Serialize(fixture.Events.Published.Single().Payload).Should().Contain("\"confounders\":false");
    }

    [Fact]
    public async Task ScriptedScenariosKeepTheirOwnConfounders()
    {
        var fixture = new Fixture();

        await fixture.Service.InjectAsync(Request(RootCauseCategory.SupplyChainMaterial, confounders: true) with { Scripted = true }, CancellationToken.None);

        fixture.Board.All.Should().BeEmpty();
    }

    [Fact]
    public void MisleadingRecordsAreDatedTheSameWeekBeforeTheFault()
    {
        foreach (var category in Enum.GetValues<RootCauseCategory>())
        {
            var records = ConfounderFactory.For(category, new ToolId("E-02"), "A", Guid.NewGuid(), Now);
            records.WorkOrders.Select(order => order.PerformedAt)
                .Concat(records.Ecos.Select(eco => eco.EffectiveAt))
                .Concat(records.Notices.Select(notice => notice.IssuedAt))
                .Should().OnlyContain(at => at < Now && at > Now.AddDays(-7));
        }
    }

    private static FaultInjectionRequest Request(RootCauseCategory category, bool confounders) =>
        new("E-02", "A", "gasFlow", "step", 3d, category, confounders);

    private sealed class Fixture
    {
        public Fixture()
        {
            Faults = new InMemoryFaultLog();
            var fleet = new InMemoryFleetCatalog();
            fleet.Add(new Tool(new ToolId("E-02"), "Etch 02", ToolFamily.Etch, "MPS-E7", "Bay-2", [new Chamber("A", "Chamber A")]));
            var clock = new FixedClock(Now);
            Service = new FaultInjectionService(fleet, Faults, NullQualityNotifier.Instance, clock, Events, confounders: Board);
        }

        public ConfounderBoard Board { get; } = new();

        public RecordingSink Events { get; } = new();

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

    private sealed class RecordingSink : ILifecycleEventSink
    {
        public List<LifecycleEvent> Published { get; } = [];

        public ValueTask<string> PublishAsync(LifecycleEvent draft, CancellationToken cancellationToken = default)
        {
            Published.Add(draft);
            return ValueTask.FromResult(Guid.NewGuid().ToString("N"));
        }

        public ValueTask<IReadOnlyList<string>> PublishManyAsync(IReadOnlyList<LifecycleEvent> drafts, CancellationToken cancellationToken = default)
        {
            Published.AddRange(drafts);
            IReadOnlyList<string> ids = drafts.Select(_ => Guid.NewGuid().ToString("N")).ToArray();
            return ValueTask.FromResult(ids);
        }
    }
}
