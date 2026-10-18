// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Lifecycle;
using Mps.Application.Metrics;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Application.Tests.Fakes;
using Mps.Domain.Capa;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;

namespace Mps.Application.Tests.Lifecycle;

/// <summary>
/// The save returns, then two notifier calls and a metrics write run, and only then did the placeholder used to end.
/// The list must show the saved record once through that window.
/// </summary>
public sealed class InvestigationListRaceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TheListHasOneEntryWhileTheNotifierIsHeldAndOneAfter()
    {
        var incidents = new InMemoryIncidentLog();
        var investigations = new InMemoryInvestigationLog();
        var incident = await Open(incidents, "corr-notifier");
        var gate = new TaskCompletionSource();
        var notifier = new HoldingNotifier(gate.Task);
        var inFlight = new InvestigationsInFlight();
        var service = Service(incidents, investigations, new StubWorkflow(), notifier, inFlight);

        var running = service.StartAsync(incident.Id, CancellationToken.None);
        try
        {
            await notifier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var during = await service.ListAsync(CancellationToken.None);
            during.Should().ContainSingle();
            during[0].Stage.Should().Be(InvestigationStage.RecommendationReady);
            during[0].Id.Should().NotBe(notifier.PlaceholderId);
            (await service.ListRoundsAsync(incident.Id, CancellationToken.None)).Should().ContainSingle().Which.Id.Should().Be(during[0].Id);
            (await service.GetAsync(notifier.PlaceholderId, CancellationToken.None)).Id.Should().Be(during[0].Id);
            (await service.GetLatestByIncidentAsync(incident.Id, CancellationToken.None)).Id.Should().Be(during[0].Id);

            gate.TrySetResult();
            var saved = await running.WaitAsync(TimeSpan.FromSeconds(5));

            saved.Id.Should().Be(during[0].Id);
            (await service.ListAsync(CancellationToken.None)).Should().ContainSingle().Which.Id.Should().Be(saved.Id);
            (await service.ListRoundsAsync(incident.Id, CancellationToken.None)).Should().ContainSingle().Which.Id.Should().Be(saved.Id);
        }
        finally
        {
            gate.TrySetResult();
        }
    }

    [Fact]
    public async Task ListDropsAPlaceholderWhenThatRoundIsAlreadySaved()
    {
        var incidents = new InMemoryIncidentLog();
        var investigations = new InMemoryInvestigationLog();
        var incident = await Open(incidents, "corr-filter");
        var saved = new InvestigationSnapshot(
            InvestigationId.New(),
            incident.Id,
            InvestigationStage.RecommendationReady,
            [],
            [],
            null,
            "corr-filter")
        {
            Round = 1,
        };
        await investigations.AddAsync(saved, CancellationToken.None);
        var inFlight = new InvestigationsInFlight();
        var placeholder = new InvestigationSnapshot(
            InvestigationId.New(),
            incident.Id,
            InvestigationStage.Started,
            [],
            [],
            null,
            "corr-filter")
        {
            Round = 1,
        };
        inFlight.TryBegin(placeholder, out _).Should().BeTrue();
        var service = Service(incidents, investigations, new StubWorkflow(), NullQualityNotifier.Instance, inFlight);

        var listed = await service.ListAsync(CancellationToken.None);
        var rounds = await service.ListRoundsAsync(incident.Id, CancellationToken.None);

        listed.Should().ContainSingle().Which.Id.Should().Be(saved.Id);
        rounds.Should().ContainSingle().Which.Id.Should().Be(saved.Id);
        inFlight.Running.Should().ContainSingle("the placeholder is still registered; the list is what drops it");
    }

    [Fact]
    public async Task ALaterRoundStillGatheringStaysBesideTheSavedRound()
    {
        var incidents = new InMemoryIncidentLog();
        var investigations = new InMemoryInvestigationLog();
        var incident = await Open(incidents, "corr-rounds");
        var workflow = new StubWorkflow();
        var service = Service(incidents, investigations, workflow, NullQualityNotifier.Instance, new InvestigationsInFlight());
        var first = await service.StartAsync(incident.Id, CancellationToken.None);
        await investigations.UpdateAsync(
            new InvestigationSnapshot(
                first.Id,
                first.IncidentId,
                InvestigationStage.Superseded,
                first.Evidence,
                first.Hypotheses,
                first.EscalationReason,
                first.CorrelationId)
            {
                Round = first.Round,
            },
            CancellationToken.None);
        var gate = new TaskCompletionSource();
        workflow.Gate = gate.Task;
        var second = service.StartAsync(incident.Id, CancellationToken.None);
        try
        {
            var during = await service.ListAsync(CancellationToken.None);
            during.Should().HaveCount(2);
            during.Should().ContainSingle(item => item.Id == first.Id);
            during.Should().ContainSingle(item => item.Stage == InvestigationStage.Started && item.Round == 2);
            (await service.GetLatestByIncidentAsync(incident.Id, CancellationToken.None)).Stage.Should().Be(InvestigationStage.Started);

            gate.TrySetResult();
            var savedSecond = await second.WaitAsync(TimeSpan.FromSeconds(5));
            var after = await service.ListAsync(CancellationToken.None);
            after.Should().HaveCount(2);
            after.Select(item => item.Id).Should().BeEquivalentTo([first.Id, savedSecond.Id]);
        }
        finally
        {
            gate.TrySetResult();
        }
    }

    private static async Task<Incident> Open(InMemoryIncidentLog incidents, string correlationId)
    {
        var incident = Incident.Open(
            IncidentId.New(),
            new ToolId("E-02"),
            "E-02-A",
            "escTemperature",
            IncidentChannel.IotSpcAlert,
            Severity.High,
            "Alert.",
            correlationId,
            Now);
        await incidents.AddAsync(new StoredIncident(incident, false, false), CancellationToken.None);
        return incident;
    }

    private static InvestigationService Service(
        InMemoryIncidentLog incidents,
        InMemoryInvestigationLog investigations,
        StubWorkflow workflow,
        IQualityNotifier notifier,
        InvestigationsInFlight inFlight) =>
        new(
            incidents,
            investigations,
            workflow,
            notifier,
            TimeProvider.System,
            NullMetricsLog.Instance,
            new MetricsPolicy(0d, 2d),
            inFlight: inFlight);

    private sealed class HoldingNotifier : IQualityNotifier
    {
        private readonly Task _gate;

        public HoldingNotifier(Task gate)
        {
            ArgumentNullException.ThrowIfNull(gate);
            _gate = gate;
            Entered = new TaskCompletionSource();
        }

        public TaskCompletionSource Entered { get; }

        public InvestigationId PlaceholderId { get; private set; }

        public Task AlertRaisedAsync(Alert alert, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(alert);
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task TicketOpenedAsync(Ticket ticket, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(ticket);
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task IncidentChangedAsync(Incident incident, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(incident);
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task InvestigationChangedAsync(InvestigationSnapshot snapshot, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            _ = cancellationToken;
            Entered.TrySetResult();
            return _gate;
        }

        public Task InvestigationRunningAsync(InvestigationSnapshot placeholder, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(placeholder);
            _ = cancellationToken;
            PlaceholderId = placeholder.Id;
            return Task.CompletedTask;
        }

        public Task CapaChangedAsync(CapaAction capa, string eventName, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(capa);
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task FaultInjectedAsync(Guid faultId, string correlationId, string toolId, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
