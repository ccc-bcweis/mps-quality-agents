// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Bus;
using Mps.Application.Errors;
using Mps.Application.Lifecycle;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Application.Tests.Fakes;
using Mps.Domain.Approval;
using Mps.Domain.Capa;
using Mps.Domain.Events;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Lifecycle;

namespace Mps.Application.Tests.Lifecycle;

public sealed class CapaLifecycleEffectsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private static readonly byte[] Key = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

    [Fact]
    public async Task ContainmentRemovesTheToolFaultsAndRecordsATraceEvent()
    {
        var fixture = await Fixture.CreateAsync();

        await fixture.Service.AdvanceAsync(fixture.Capa.Id, CapaStage.Containment, CancellationToken.None);

        fixture.Plant.ClearedTools.Should().Equal("E-02");
        fixture.Events.Published.Should().ContainSingle(item => item.Type == EventTypes.FaultCleared && item.ToolId == "E-02" && item.CorrelationId == "corr-life");
    }

    [Fact]
    public async Task VerificationIsRefusedWhileAnAlertIsActiveAndAllowedOnceItClears()
    {
        var fixture = await Fixture.CreateAsync();
        var alert = new Alert(AlertId.New(), new ToolId("E-02"), "A", "escTemperature", Severity.High, "western-electric-1", Now, "corr-life");
        await fixture.Alerts.AddAsync(alert, CancellationToken.None);
        foreach (var stage in new[] { CapaStage.Containment, CapaStage.RootCauseConfirmed, CapaStage.CorrectiveAction })
        {
            await fixture.Service.AdvanceAsync(fixture.Capa.Id, stage, CancellationToken.None);
        }

        var blocked = () => fixture.Service.AdvanceAsync(fixture.Capa.Id, CapaStage.Verification, CancellationToken.None);
        await blocked.Should().ThrowAsync<LifecycleConflictException>().WithMessage("*not back in control*");
        fixture.Capa.Stage.Should().Be(CapaStage.CorrectiveAction);

        await fixture.Alerts.ClearAsync(alert.Id, Now.AddMinutes(10), CancellationToken.None);
        await fixture.Service.AdvanceAsync(fixture.Capa.Id, CapaStage.Verification, CancellationToken.None);
        fixture.Capa.Stage.Should().Be(CapaStage.Verification);
    }

    [Fact]
    public async Task ClosingTheCapaClosesTheIncident()
    {
        var fixture = await Fixture.CreateAsync();
        foreach (var stage in new[] { CapaStage.Containment, CapaStage.RootCauseConfirmed, CapaStage.CorrectiveAction, CapaStage.Verification })
        {
            await fixture.Service.AdvanceAsync(fixture.Capa.Id, stage, CancellationToken.None);
        }

        fixture.Incident.Incident.Stage.Should().Be(IncidentStage.Approved);
        await fixture.Service.AdvanceAsync(fixture.Capa.Id, CapaStage.Closed, CancellationToken.None);
        fixture.Incident.Incident.Stage.Should().Be(IncidentStage.Closed);
    }

    private sealed class Fixture
    {
        private Fixture(CapaLifecycleService service, CapaAction capa, StoredIncident incident, InMemoryAlertLog alerts, FakePlant plant, RecordingSink events)
        {
            Service = service;
            Capa = capa;
            Incident = incident;
            Alerts = alerts;
            Plant = plant;
            Events = events;
        }

        public CapaLifecycleService Service { get; }

        public CapaAction Capa { get; }

        public StoredIncident Incident { get; }

        public InMemoryAlertLog Alerts { get; }

        public FakePlant Plant { get; }

        public RecordingSink Events { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var incident = IncidentRestorer.Restore(
                IncidentId.New(),
                new ToolId("E-02"),
                "A",
                "escTemperature",
                IncidentChannel.IotSpcAlert,
                Severity.High,
                "Lifecycle fixture.",
                "corr-life",
                Now,
                IncidentStage.Approved);
            var stored = new StoredIncident(incident, false, hasEscalated: false);
            var incidents = new InMemoryIncidentLog();
            await incidents.AddAsync(stored, CancellationToken.None);
            const string summary = "Replace the edge ring.";
            var token = ApprovalToken.Issue(ApprovalTokenId.New(), "eng-17", CapaAction.ComputeActionHash(incident.Id, summary), Now, TimeSpan.FromMinutes(15), Key);
            var capa = CapaAction.Open(CapaId.New(), incident.Id, summary, token, Now, Key);
            var capas = new InMemoryCapaLog();
            await capas.AddAsync(capa, "CAPA-1", CancellationToken.None);
            var alerts = new InMemoryAlertLog();
            var plant = new FakePlant();
            var events = new RecordingSink();
            var service = new CapaLifecycleService(capas, NullQualityNotifier.Instance, incidents, alerts, plant, events);
            return new Fixture(service, capa, stored, alerts, plant, events);
        }
    }

    private sealed class FakePlant : IPlantFaultControl
    {
        public List<string> ClearedTools { get; } = [];

        public int ClearTool(string toolId)
        {
            ClearedTools.Add(toolId);
            return 1;
        }

        public int ClearAll() => 0;
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
