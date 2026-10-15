// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Errors;
using Mps.Application.Lifecycle;
using Mps.Application.Metrics;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Application.Tests.Fakes;
using Mps.Domain.Approval;
using Mps.Domain.Capa;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;
using Mps.Domain.Lifecycle;

namespace Mps.Application.Tests.Lifecycle;

public sealed class LifecycleMoveTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static readonly byte[] Key = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

    public static TheoryData<IncidentStage, IncidentStage> IllegalIncidentMoves { get; } = BuildIllegalIncidentMoves();

    public static TheoryData<CapaStage, CapaStage> IllegalCapaMoves { get; } = BuildIllegalCapaMoves();

    [Theory]
    [MemberData(nameof(IllegalIncidentMoves))]
    public void AnIllegalIncidentTransitionIsRejected(IncidentStage from, IncidentStage to)
    {
        var stored = StoredAt(from, escalationRequired: false);

        var act = () => LifecycleMoves.MoveOne(stored, to);

        act.Should().Throw<LifecycleConflictException>();
        stored.Incident.Stage.Should().Be(from);
    }

    [Theory]
    [MemberData(nameof(IllegalCapaMoves))]
    public void AnIllegalCapaTransitionIsRejected(CapaStage from, CapaStage to)
    {
        var capa = CapaAt(from);

        var act = () => LifecycleMoves.MoveOne(capa, to);

        act.Should().Throw<LifecycleConflictException>();
        capa.Stage.Should().Be(from);
    }

    [Fact]
    public void CriticalSeverityCannotSkipEscalationOnTheWayToApproval()
    {
        var stored = StoredAt(IncidentStage.RecommendationReady, escalationRequired: true);

        var act = () => LifecycleMoves.MoveOne(stored, IncidentStage.AwaitingApproval);

        act.Should().Throw<LifecycleConflictException>().WithMessage("*escalation*");
        stored.Incident.Stage.Should().Be(IncidentStage.RecommendationReady);
    }

    [Fact]
    public void CriticalSeverityCanBeApprovedAfterEscalation()
    {
        var stored = StoredAt(IncidentStage.RecommendationReady, escalationRequired: true);
        LifecycleMoves.MoveOne(stored, IncidentStage.Escalated);

        LifecycleMoves.MoveOne(stored, IncidentStage.AwaitingApproval);

        stored.HasEscalated.Should().BeTrue();
        stored.Incident.Stage.Should().Be(IncidentStage.AwaitingApproval);
    }

    [Fact]
    public void WalkingToRecommendationReadyUsesOnlyLegalSteps()
    {
        var stored = StoredAt(IncidentStage.TicketOpened, escalationRequired: false);

        LifecycleMoves.WalkTo(stored, IncidentStage.RecommendationReady);

        stored.Incident.Stage.Should().Be(IncidentStage.RecommendationReady);
    }

    [Fact]
    public void ACriticalIncidentReachesApprovalOnlyByPassingThroughEscalation()
    {
        var stored = StoredAt(IncidentStage.TicketOpened, escalationRequired: true);

        LifecycleMoves.WalkTo(stored, IncidentStage.AwaitingApproval);

        stored.HasEscalated.Should().BeTrue();
        stored.Incident.Stage.Should().Be(IncidentStage.AwaitingApproval);
    }

    [Theory]
    [InlineData(IncidentStage.TicketOpened)]
    [InlineData(IncidentStage.Investigating)]
    [InlineData(IncidentStage.RecommendationReady)]
    [InlineData(IncidentStage.Escalated)]
    [InlineData(IncidentStage.AwaitingApproval)]
    [InlineData(IncidentStage.Approved)]
    [InlineData(IncidentStage.Rejected)]
    [InlineData(IncidentStage.Closed)]
    public void AnIncidentCanBeRestoredAtItsStoredStage(IncidentStage stage)
    {
        var restored = IncidentRestorer.Restore(
            IncidentId.New(),
            new ToolId("E-02"),
            "A",
            "escTemperature",
            IncidentChannel.OperatorReport,
            Severity.High,
            "Restored.",
            "corr-restore",
            Now,
            stage);

        restored.Stage.Should().Be(stage);
    }

    [Fact]
    public void DemoResetClosesAnOpenIncidentWithoutShorteningANormalClose()
    {
        var incident = Incident.Open(
            IncidentId.New(),
            new ToolId("E-02"),
            "A",
            "escTemperature",
            IncidentChannel.OperatorReport,
            Severity.Medium,
            "Open work.",
            "corr-demo-reset",
            Now);
        incident.CloseByDemoReset();
        incident.Stage.Should().Be(IncidentStage.ClosedByDemoReset);
        incident.CloseByDemoReset();
        incident.Stage.Should().Be(IncidentStage.ClosedByDemoReset);

        var closedPath = IncidentStagePath.Between(IncidentStage.TicketOpened, IncidentStage.Closed, blockApproval: false);
        closedPath.Should().NotContain(IncidentStage.ClosedByDemoReset);
        closedPath.Should().Equal(
            IncidentStage.Investigating,
            IncidentStage.Escalated,
            IncidentStage.Closed);

        var restored = IncidentRestorer.Restore(
            IncidentId.New(),
            new ToolId("E-02"),
            "A",
            "escTemperature",
            IncidentChannel.OperatorReport,
            Severity.High,
            "Restored.",
            "corr-restore-demo",
            Now,
            IncidentStage.ClosedByDemoReset);
        restored.Stage.Should().Be(IncidentStage.ClosedByDemoReset);

        var investigation = new InvestigationSnapshot(
            InvestigationId.New(),
            IncidentId.New(),
            InvestigationStage.RecommendationReady,
            [],
            [],
            null,
            "corr-demo-reset");
        var closedInvestigation = InvestigationLifecycle.CloseByDemoReset(investigation);
        closedInvestigation.Stage.Should().Be(InvestigationStage.ClosedByDemoReset);
        InvestigationLifecycle.CloseByDemoReset(closedInvestigation).Should().BeSameAs(closedInvestigation);
        var approved = new InvestigationSnapshot(
            InvestigationId.New(),
            IncidentId.New(),
            InvestigationStage.Approved,
            [],
            [],
            null,
            "corr-approved");
        var act = () => InvestigationLifecycle.CloseByDemoReset(approved);
        act.Should().Throw<Mps.Domain.DomainRuleException>();
    }

    [Fact]
    public async Task StartingAnInvestigationMovesTheIncidentAndRejectsASecondStart()
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
            "corr-start",
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

        var started = await service.StartAsync(incident.Id, CancellationToken.None);
        var again = () => service.StartAsync(incident.Id, CancellationToken.None);

        started.Stage.Should().Be(InvestigationStage.RecommendationReady);
        started.CorrelationId.Should().Be("corr-start");
        incident.Stage.Should().Be(IncidentStage.RecommendationReady);
        await again.Should().ThrowAsync<LifecycleConflictException>().WithMessage("*already open*");
    }

    [Fact]
    public async Task AWorkflowThatSkipsToApprovalIsRejected()
    {
        var incidents = new InMemoryIncidentLog();
        var incident = Incident.Open(
            IncidentId.New(),
            new ToolId("E-02"),
            null,
            "escTemperature",
            IncidentChannel.IotSpcAlert,
            Severity.High,
            "Alert.",
            "corr-skip",
            Now);
        await incidents.AddAsync(new StoredIncident(incident, false, false), CancellationToken.None);
        var workflow = new StubWorkflow { Stage = InvestigationStage.Approved };
        var service = new InvestigationService(
            incidents,
            new InMemoryInvestigationLog(),
            workflow,
            NullQualityNotifier.Instance,
            TimeProvider.System,
            NullMetricsLog.Instance,
            new MetricsPolicy(0d, 2d));

        var act = () => service.StartAsync(incident.Id, CancellationToken.None);

        await act.Should().ThrowAsync<LifecycleConflictException>().WithMessage("*human decision*");
        incident.Stage.Should().Be(IncidentStage.TicketOpened);
    }

    [Fact]
    public async Task AdvancingACapaPastTheNextStageIsRejected()
    {
        var log = new InMemoryCapaLog();
        var capa = CapaAt(CapaStage.Open);
        await log.AddAsync(capa, "CAPA-1", CancellationToken.None);
        var service = new CapaLifecycleService(log, NullQualityNotifier.Instance);

        var act = () => service.AdvanceAsync(capa.Id, CapaStage.Closed, CancellationToken.None);

        await act.Should().ThrowAsync<LifecycleConflictException>();
        capa.Stage.Should().Be(CapaStage.Open);
    }

    private static StoredIncident StoredAt(IncidentStage stage, bool escalationRequired)
    {
        var incident = IncidentRestorer.Restore(
            IncidentId.New(),
            new ToolId("E-02"),
            "A",
            "escTemperature",
            IncidentChannel.OperatorReport,
            escalationRequired ? Severity.Critical : Severity.Medium,
            "Lifecycle fixture.",
            "corr-life",
            Now,
            stage);
        return new StoredIncident(incident, escalationRequired, hasEscalated: false);
    }

    private static CapaAction CapaAt(CapaStage stage)
    {
        var incidentId = IncidentId.New();
        const string summary = "Hold the lot.";
        var token = ApprovalToken.Issue(
            ApprovalTokenId.New(),
            "eng-17",
            CapaAction.ComputeActionHash(incidentId, summary),
            Now,
            TimeSpan.FromMinutes(15),
            Key);
        var capa = CapaAction.Open(CapaId.New(), incidentId, summary, token, Now, Key);
        foreach (var step in new[]
        {
            CapaStage.Containment,
            CapaStage.RootCauseConfirmed,
            CapaStage.CorrectiveAction,
            CapaStage.Verification,
            CapaStage.Closed,
        })
        {
            if (capa.Stage == stage)
            {
                break;
            }

            if (!CapaLifecycle.CanTransition(capa.Stage, step))
            {
                continue;
            }

            capa.Advance(step);
        }

        capa.Stage.Should().Be(stage);
        return capa;
    }

    private static TheoryData<IncidentStage, IncidentStage> BuildIllegalIncidentMoves()
    {
        var data = new TheoryData<IncidentStage, IncidentStage>();
        foreach (var from in Enum.GetValues<IncidentStage>())
        {
            foreach (var to in Enum.GetValues<IncidentStage>())
            {
                if (!IncidentLifecycle.CanTransition(from, to))
                {
                    data.Add(from, to);
                }
            }
        }

        return data;
    }

    private static TheoryData<CapaStage, CapaStage> BuildIllegalCapaMoves()
    {
        var data = new TheoryData<CapaStage, CapaStage>();
        foreach (var from in Enum.GetValues<CapaStage>())
        {
            foreach (var to in Enum.GetValues<CapaStage>())
            {
                if (!CapaLifecycle.CanTransition(from, to))
                {
                    data.Add(from, to);
                }
            }
        }

        return data;
    }
}
