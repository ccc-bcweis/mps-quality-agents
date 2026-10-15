// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Errors;
using Mps.Application.Lifecycle;
using Mps.Application.Metrics;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Application.Tests.Fakes;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;

namespace Mps.Application.Tests.Lifecycle;

public sealed class TicketInvestigationMapTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TheLatestInvestigationIsTheLastOneStartedForThatIncident()
    {
        var incidentId = IncidentId.New();
        var otherId = IncidentId.New();
        var older = Snapshot(incidentId, InvestigationStage.Started);
        var other = Snapshot(otherId, InvestigationStage.RecommendationReady);
        var newer = Snapshot(incidentId, InvestigationStage.RecommendationReady);
        var log = new InMemoryInvestigationLog();
        await log.AddAsync(older, CancellationToken.None);
        await log.AddAsync(other, CancellationToken.None);
        await log.AddAsync(newer, CancellationToken.None);

        var fromLog = await log.FindLatestByIncidentAsync(incidentId, CancellationToken.None);
        var fromMap = TicketInvestigationMap.Latest(incidentId, [older, other, newer]);

        fromLog.Should().NotBeNull();
        fromMap.Should().NotBeNull();
        fromLog!.Id.Should().Be(newer.Id);
        fromMap!.Id.Should().Be(newer.Id);
        fromLog.Id.Should().NotBe(older.Id);
        TicketInvestigationMap.Latest(IncidentId.New(), [older, newer]).Should().BeNull();
    }

    [Fact]
    public void ARejectedInvestigationStillCountsAsTheLatest()
    {
        var incidentId = IncidentId.New();
        var earlier = Snapshot(incidentId, InvestigationStage.RecommendationReady);
        var rejected = Snapshot(incidentId, InvestigationStage.Rejected);

        var latest = TicketInvestigationMap.Latest(incidentId, [earlier, rejected]);

        latest.Should().NotBeNull();
        latest!.Id.Should().Be(rejected.Id);
        latest.Stage.Should().Be(InvestigationStage.Rejected);
    }

    [Fact]
    public async Task GetLatestIsMissingUntilAnInvestigationStarts()
    {
        var incidents = new InMemoryIncidentLog();
        var investigations = new InMemoryInvestigationLog();
        var incident = Incident.Open(
            IncidentId.New(),
            new ToolId("E-02"),
            "A",
            "escTemperature",
            IncidentChannel.OperatorReport,
            Severity.Medium,
            "Operator report.",
            "corr-map",
            Now);
        await incidents.AddAsync(new StoredIncident(incident, false, false), CancellationToken.None);
        var service = new InvestigationService(
            incidents,
            investigations,
            new StubWorkflow(),
            NullQualityNotifier.Instance,
            TimeProvider.System,
            NullMetricsLog.Instance,
            new MetricsPolicy(0d, 2d));

        var missing = () => service.GetLatestByIncidentAsync(incident.Id, CancellationToken.None);
        await missing.Should().ThrowAsync<ResourceNotFoundException>().WithMessage("*Investigation*");

        var started = await service.StartAsync(incident.Id, CancellationToken.None);
        var latest = await service.GetLatestByIncidentAsync(incident.Id, CancellationToken.None);

        latest.Id.Should().Be(started.Id);
        latest.IncidentId.Should().Be(incident.Id);
    }

    [Fact]
    public async Task GetLatestRejectsAnUnknownIncident()
    {
        var service = new InvestigationService(
            new InMemoryIncidentLog(),
            new InMemoryInvestigationLog(),
            new StubWorkflow(),
            NullQualityNotifier.Instance,
            TimeProvider.System,
            NullMetricsLog.Instance,
            new MetricsPolicy(0d, 2d));

        var act = () => service.GetLatestByIncidentAsync(IncidentId.New(), CancellationToken.None);

        await act.Should().ThrowAsync<ResourceNotFoundException>().WithMessage("*Incident*");
    }

    private static InvestigationSnapshot Snapshot(IncidentId incidentId, InvestigationStage stage) =>
        new(InvestigationId.New(), incidentId, stage, [], [], null, "corr-map");
}
