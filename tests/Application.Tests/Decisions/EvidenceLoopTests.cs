// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Decisions;
using Mps.Application.Errors;
using Mps.Application.Lifecycle;
using Mps.Application.Metrics;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Application.Tests.Fakes;
using Mps.Application.Writes;
using Mps.Domain.Capa;
using Mps.Domain.Evidence;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;

namespace Mps.Application.Tests.Decisions;

/// <summary>The evidence loop: add evidence, the agents run again as the next round, each round's decision stays on file.</summary>
public sealed class EvidenceLoopTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddedEvidenceStartsTheNextRoundWithItAndKeepsTheClosedRound()
    {
        var loop = await LoopAsync();
        var first = await loop.Investigations.StartAsync(loop.Incident.Id, CancellationToken.None);

        var decided = await loop.Decisions.DecideAsync(first.Id, HumanDecisionKind.RequestInfo, "The drift was operator error during the clean.", "bryan", CancellationToken.None);
        var second = await loop.Investigations.StartRoundAsync(loop.Incident.Id, decided.UserEvidence, CancellationToken.None);

        first.Round.Should().Be(1);
        second.Round.Should().Be(2);
        loop.Workflow.LastUserEvidence.Should().ContainSingle().Which.Excerpt.Should().Be("The drift was operator error during the clean.");
        second.Evidence.Should().Contain(item => item.Source == EvidenceSource.UserProvided && item.SuppliedBy == "bryan");
        second.Decision.Should().BeNull("only the closed round has a decision");
        var rounds = await loop.Investigations.ListRoundsAsync(loop.Incident.Id, CancellationToken.None);
        rounds.Select(round => (round.Round, round.Stage)).Should().Equal((1, InvestigationStage.Superseded), (2, InvestigationStage.RecommendationReady));
        rounds[0].Decision.Should().BeEquivalentTo(new { Kind = "requestInfo", Note = "The drift was operator error during the clean.", DecidedBy = "bryan" });
        loop.Incident.Stage.Should().Be(IncidentStage.RecommendationReady, "the ticket stays open while the loop runs");
    }

    [Fact]
    public async Task EvidenceFromEveryEarlierRoundIsCarriedForward()
    {
        var loop = await LoopAsync();
        var first = await loop.Investigations.StartAsync(loop.Incident.Id, CancellationToken.None);
        var one = await loop.Decisions.DecideAsync(first.Id, HumanDecisionKind.RequestInfo, "Operator note one.", "bryan", CancellationToken.None);
        var second = await loop.Investigations.StartRoundAsync(loop.Incident.Id, one.UserEvidence, CancellationToken.None);

        var two = await loop.Decisions.DecideAsync(second.Id, HumanDecisionKind.RequestInfo, "Technician note two.", "dana", CancellationToken.None);
        var third = await loop.Investigations.StartRoundAsync(loop.Incident.Id, two.UserEvidence, CancellationToken.None);

        third.Round.Should().Be(3);
        third.Evidence.Where(item => item.Source == EvidenceSource.UserProvided).Select(item => (item.Excerpt, item.SuppliedBy, item.ExternalRef))
            .Should().Equal(("Operator note one.", "bryan", "USER-R1"), ("Technician note two.", "dana", "USER-R2"));
    }

    [Fact]
    public async Task ADecisionIsFinalPerRound()
    {
        var loop = await LoopAsync();
        var first = await loop.Investigations.StartAsync(loop.Incident.Id, CancellationToken.None);
        await loop.Decisions.DecideAsync(first.Id, HumanDecisionKind.RequestInfo, "Operator note.", "bryan", CancellationToken.None);

        var again = () => loop.Decisions.DecideAsync(first.Id, HumanDecisionKind.Approve, null, "bryan", CancellationToken.None);

        await again.Should().ThrowAsync<LifecycleConflictException>().WithMessage("Round 1 was already closed when more evidence was added*round 2*cannot be changed*");
    }

    [Fact]
    public async Task ApproveInALaterRoundEndsTheLoopWithACapa()
    {
        var loop = await LoopAsync();
        var first = await loop.Investigations.StartAsync(loop.Incident.Id, CancellationToken.None);
        var one = await loop.Decisions.DecideAsync(first.Id, HumanDecisionKind.RequestInfo, "Operator note.", "bryan", CancellationToken.None);
        var second = await loop.Investigations.StartRoundAsync(loop.Incident.Id, one.UserEvidence, CancellationToken.None);

        var approved = await loop.Decisions.DecideAsync(second.Id, HumanDecisionKind.Approve, null, "bryan", CancellationToken.None);

        approved.Capa.Should().NotBeNull();
        approved.Investigation.Round.Should().Be(2);
        loop.Incident.Stage.Should().Be(IncidentStage.Approved);
        var next = () => loop.Investigations.StartAsync(loop.Incident.Id, CancellationToken.None);
        await next.Should().ThrowAsync<LifecycleConflictException>();
    }

    [Fact]
    public async Task RejectInALaterRoundClosesTheTicketWithNoAction()
    {
        var loop = await LoopAsync();
        var first = await loop.Investigations.StartAsync(loop.Incident.Id, CancellationToken.None);
        var one = await loop.Decisions.DecideAsync(first.Id, HumanDecisionKind.RequestInfo, "Operator note.", "bryan", CancellationToken.None);
        var second = await loop.Investigations.StartRoundAsync(loop.Incident.Id, one.UserEvidence, CancellationToken.None);

        var rejected = await loop.Decisions.DecideAsync(second.Id, HumanDecisionKind.Reject, "Not a real excursion.", "bryan", CancellationToken.None);

        rejected.Capa.Should().BeNull();
        loop.Writer.Calls.Should().Be(0);
        loop.Incident.Stage.Should().Be(IncidentStage.Closed);
    }

    [Fact]
    public async Task TheRoundCapStopsAnotherRound()
    {
        var loop = await LoopAsync(maxRounds: 2);
        var first = await loop.Investigations.StartAsync(loop.Incident.Id, CancellationToken.None);
        var one = await loop.Decisions.DecideAsync(first.Id, HumanDecisionKind.RequestInfo, "Operator note.", "bryan", CancellationToken.None);
        var second = await loop.Investigations.StartRoundAsync(loop.Incident.Id, one.UserEvidence, CancellationToken.None);

        var third = () => loop.Decisions.DecideAsync(second.Id, HumanDecisionKind.RequestInfo, "One more note.", "bryan", CancellationToken.None);

        await third.Should().ThrowAsync<LifecycleConflictException>().WithMessage("*reached 2 rounds*");
        var still = await loop.Decisions.DecideAsync(second.Id, HumanDecisionKind.Reject, null, "bryan", CancellationToken.None);
        still.Investigation.Stage.Should().Be(InvestigationStage.Rejected);
    }

    [Fact]
    public async Task AnEscalatedRoundTakesEvidenceAndTheNextRoundCanRecommend()
    {
        var loop = await LoopAsync();
        loop.Workflow.Stage = InvestigationStage.Escalated;
        var first = await loop.Investigations.StartAsync(loop.Incident.Id, CancellationToken.None);
        loop.Incident.Stage.Should().Be(IncidentStage.Escalated);

        var one = await loop.Decisions.DecideAsync(first.Id, HumanDecisionKind.RequestInfo, "The calibration certificate is attached.", "bryan", CancellationToken.None);
        loop.Workflow.Stage = InvestigationStage.RecommendationReady;
        var second = await loop.Investigations.StartRoundAsync(loop.Incident.Id, one.UserEvidence, CancellationToken.None);

        second.Stage.Should().Be(InvestigationStage.RecommendationReady);
        loop.Incident.Stage.Should().Be(IncidentStage.RecommendationReady);
    }

    [Fact]
    public async Task APlainStartAfterAClosedRoundStillCarriesThePersonsEvidence()
    {
        var loop = await LoopAsync();
        var first = await loop.Investigations.StartAsync(loop.Incident.Id, CancellationToken.None);
        await loop.Decisions.DecideAsync(first.Id, HumanDecisionKind.RequestInfo, "Operator note.", "bryan", CancellationToken.None);

        var second = await loop.Investigations.StartAsync(loop.Incident.Id, CancellationToken.None);

        second.Round.Should().Be(2);
        loop.Workflow.LastUserEvidence.Should().ContainSingle().Which.Excerpt.Should().Be("Operator note.");
    }

    [Fact]
    public async Task OnlyUserProvidedRecordsCanStartARound()
    {
        var loop = await LoopAsync();
        var record = new EvidenceItem(EvidenceId.New(), EvidenceSource.Cmms, "Work order", "text", Now, "E-01", "WO-1");

        var act = () => loop.Investigations.StartRoundAsync(loop.Incident.Id, [record], CancellationToken.None);

        await act.Should().ThrowAsync<IntakeValidationException>();
    }

    private static async Task<Loop> LoopAsync(int maxRounds = 5)
    {
        var incidents = new InMemoryIncidentLog();
        var investigations = new InMemoryInvestigationLog();
        var incident = IncidentRestorer.Restore(
            IncidentId.New(),
            new ToolId("E-01"),
            "E-01-A",
            "escTemperature",
            IncidentChannel.IotSpcAlert,
            Severity.Medium,
            "ESC temperature drifted.",
            "corr-loop",
            Now,
            IncidentStage.TicketOpened);
        await incidents.AddAsync(new StoredIncident(incident, false, false), CancellationToken.None);
        var workflow = new StubWorkflow();
        var service = new InvestigationService(
            incidents,
            investigations,
            workflow,
            NullQualityNotifier.Instance,
            TimeProvider.System,
            NullMetricsLog.Instance,
            new MetricsPolicy(0d, 2d));
        var writer = new CountingWriter();
        var decisions = new HumanDecisionService(
            investigations,
            incidents,
            new InMemoryCapaLog(),
            new InMemoryApprovalLog(),
            writer,
            NullQualityNotifier.Instance,
            TimeProvider.System,
            new ApprovalPolicy([.. Enumerable.Range(1, 32).Select(value => (byte)value)], TimeSpan.FromMinutes(15)),
            NullMetricsLog.Instance,
            rounds: new EvidenceRoundSettings(maxRounds));
        return new Loop(service, decisions, workflow, writer, incident);
    }

    private sealed record Loop(InvestigationService Investigations, HumanDecisionService Decisions, StubWorkflow Workflow, CountingWriter Writer, Incident Incident);

    private sealed class CountingWriter : IQmsCapaWriter
    {
        public int Calls { get; private set; }

        public Task<string> CreateAsync(CapaAction capa, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult("CAPA-" + Calls.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
