// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Lifecycle;

namespace Mps.Domain.Tests;

public sealed class IncidentLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OpenTrimsTextAndStartsAtTicketOpened()
    {
        LifecyclePairs.AssertAll();
        var incident = Incident.Open(
            IncidentId.New(),
            new ToolId("E-02"),
            "  C1  ",
            "  escTemperature  ",
            IncidentChannel.OperatorReport,
            Severity.Critical,
            "  Oscillation.  ",
            "  corr-s1  ",
            Now);

        incident.ToolId.Value.Should().Be("E-02");
        incident.ChamberId.Should().Be("C1");
        incident.Parameter.Should().Be("escTemperature");
        incident.Channel.Should().Be(IncidentChannel.OperatorReport);
        incident.Severity.Should().Be(Severity.Critical);
        incident.Summary.Should().Be("Oscillation.");
        incident.CorrelationId.Should().Be("corr-s1");
        incident.OpenedAt.Should().Be(Now);
        incident.Stage.Should().Be(IncidentStage.TicketOpened);

        Incident.Open(IncidentId.New(), new ToolId("E-02"), null, "p", IncidentChannel.IotSpcAlert, Severity.Low, "s", "c", Now)
            .ChamberId.Should().BeNull();
        Incident.Open(IncidentId.New(), new ToolId("E-02"), "  ", "p", IncidentChannel.QmsNonconformance, Severity.Low, "s", "c", Now)
            .ChamberId.Should().BeNull();
    }

    [Fact]
    public void OpenRejectsAnEmptyIdToolParameterSummaryOrCorrelation()
    {
        LifecyclePairs.AssertAll();
        var id = IncidentId.New();
        var tool = new ToolId("E-02");
        Rejects(() => Incident.Open(default, tool, "C1", "p", IncidentChannel.FaultInjector, Severity.High, "s", "c", Now), "Incident id is required.");
        Rejects(() => Incident.Open(id, default, "C1", "p", IncidentChannel.FaultInjector, Severity.High, "s", "c", Now), "Tool id is required.");
        Rejects(() => Incident.Open(id, tool, "C1", " ", IncidentChannel.FaultInjector, Severity.High, "s", "c", Now), "Parameter is required.");
        Rejects(() => Incident.Open(id, tool, "C1", null!, IncidentChannel.FaultInjector, Severity.High, "s", "c", Now), "Parameter is required.");
        Rejects(() => Incident.Open(id, tool, "C1", "p", IncidentChannel.SupplierQualityNotice, Severity.High, " ", "c", Now), "Incident summary is required.");
        Rejects(() => Incident.Open(id, tool, "C1", "p", IncidentChannel.SupplierQualityNotice, Severity.High, null!, "c", Now), "Incident summary is required.");
        Rejects(() => Incident.Open(id, tool, "C1", "p", IncidentChannel.IotSpcAlert, Severity.High, "s", null!, Now), "Correlation id is required.");
    }

    [Fact]
    public void EveryLegalIncidentEdgeIsAllowedAndTheIllegalOnesAreNot()
    {
        LifecyclePairs.AssertAll();
    }

    [Theory]
    [InlineData(IncidentStage.TicketOpened)]
    [InlineData(IncidentStage.Investigating)]
    [InlineData(IncidentStage.RecommendationReady)]
    [InlineData(IncidentStage.Escalated)]
    [InlineData(IncidentStage.AwaitingApproval)]
    [InlineData(IncidentStage.Approved)]
    [InlineData(IncidentStage.Rejected)]
    public void DemoResetClosesAnOpenIncidentOnce(IncidentStage stage)
    {
        LifecyclePairs.AssertAll();
        var incident = At(stage);

        incident.CloseByDemoReset();
        incident.Stage.Should().Be(IncidentStage.ClosedByDemoReset);

        incident.CloseByDemoReset();
        incident.Stage.Should().Be(IncidentStage.ClosedByDemoReset);
    }

    [Fact]
    public void ANormallyClosedIncidentCannotBeRelabeledByDemoReset()
    {
        LifecyclePairs.AssertAll();
        var incident = At(IncidentStage.Closed);

        var act = incident.CloseByDemoReset;
        act.Should().Throw<DomainRuleException>().WithMessage("*Cannot move an incident from Closed to ClosedByDemoReset*");
    }

    [Fact]
    public void TheLegalPathsReachClosedAndASkipNamesBothStages()
    {
        LifecyclePairs.AssertAll();
        var approved = At(IncidentStage.TicketOpened);
        approved.Advance(IncidentStage.Investigating);
        approved.Advance(IncidentStage.RecommendationReady);
        approved.Advance(IncidentStage.AwaitingApproval);
        approved.Advance(IncidentStage.Approved);
        approved.Advance(IncidentStage.Closed);
        approved.Stage.Should().Be(IncidentStage.Closed);

        var rejected = At(IncidentStage.AwaitingApproval);
        rejected.Advance(IncidentStage.Rejected);
        rejected.Advance(IncidentStage.Investigating);
        rejected.Advance(IncidentStage.Escalated);
        rejected.Advance(IncidentStage.Closed);
        rejected.Stage.Should().Be(IncidentStage.Closed);

        var skip = () => At(IncidentStage.TicketOpened).Advance(IncidentStage.Approved);
        skip.Should().Throw<DomainRuleException>().WithMessage("*Cannot move an incident from TicketOpened to Approved*");

        var same = () => At(IncidentStage.Investigating).Advance(IncidentStage.Investigating);
        same.Should().Throw<DomainRuleException>().WithMessage("*Cannot move an incident from Investigating to Investigating*");
    }

    [Fact]
    public void ATicketRequiresTheTktPrefixAndTrimmedText()
    {
        var ticket = new Ticket(
            TicketId.New(),
            "  TKT-10  ",
            IncidentId.New(),
            new ToolId("E-02"),
            "  Oscillation  ",
            Now);

        ticket.Number.Should().Be("TKT-10");
        ticket.Title.Should().Be("Oscillation");
        ticket.ToolId.Value.Should().Be("E-02");
        ticket.OpenedAt.Should().Be(Now);

        var id = TicketId.New();
        var incidentId = IncidentId.New();
        var tool = new ToolId("E-02");
        Rejects(() => new Ticket(default, "TKT-1", incidentId, tool, "t", Now), "Ticket id is required.");
        Rejects(() => new Ticket(id, "TKT-1", default, tool, "t", Now), "Incident id is required.");
        Rejects(() => new Ticket(id, "TKT-1", incidentId, default, "t", Now), "Tool id is required.");
        Rejects(() => new Ticket(id, " ", incidentId, tool, "t", Now), "Ticket number is required.");
        Rejects(() => new Ticket(id, null!, incidentId, tool, "t", Now), "Ticket number is required.");
        Rejects(() => new Ticket(id, "tkt-1", incidentId, tool, "t", Now), "Ticket number must start with TKT-.");
        Rejects(() => new Ticket(id, "INC-1", incidentId, tool, "t", Now), "Ticket number must start with TKT-.");
        Rejects(() => new Ticket(id, "1-TKT-", incidentId, tool, "t", Now), "Ticket number must start with TKT-.");
        Rejects(() => new Ticket(id, "TKT-", incidentId, tool, " ", Now), "Ticket title is required.");
        Rejects(() => new Ticket(id, "TKT-", incidentId, tool, null!, Now), "Ticket title is required.");

        new Ticket(id, "TKT-", incidentId, tool, "t", Now).Number.Should().Be("TKT-");
    }

    private static Incident At(IncidentStage stage)
    {
        var incident = Incident.Open(
            IncidentId.New(),
            new ToolId("E-02"),
            "C1",
            "escTemperature",
            IncidentChannel.FaultInjector,
            Severity.High,
            "ESC temperature oscillation.",
            "corr-s1",
            Now);

        switch (stage)
        {
            case IncidentStage.TicketOpened:
                break;
            case IncidentStage.Investigating:
                incident.Advance(IncidentStage.Investigating);
                break;
            case IncidentStage.RecommendationReady:
                incident.Advance(IncidentStage.Investigating);
                incident.Advance(IncidentStage.RecommendationReady);
                break;
            case IncidentStage.Escalated:
                incident.Advance(IncidentStage.Investigating);
                incident.Advance(IncidentStage.Escalated);
                break;
            case IncidentStage.AwaitingApproval:
                incident.Advance(IncidentStage.Investigating);
                incident.Advance(IncidentStage.RecommendationReady);
                incident.Advance(IncidentStage.AwaitingApproval);
                break;
            case IncidentStage.Approved:
                incident.Advance(IncidentStage.Investigating);
                incident.Advance(IncidentStage.RecommendationReady);
                incident.Advance(IncidentStage.AwaitingApproval);
                incident.Advance(IncidentStage.Approved);
                break;
            case IncidentStage.Rejected:
                incident.Advance(IncidentStage.Investigating);
                incident.Advance(IncidentStage.RecommendationReady);
                incident.Advance(IncidentStage.AwaitingApproval);
                incident.Advance(IncidentStage.Rejected);
                break;
            case IncidentStage.Closed:
                incident.Advance(IncidentStage.Investigating);
                incident.Advance(IncidentStage.RecommendationReady);
                incident.Advance(IncidentStage.AwaitingApproval);
                incident.Advance(IncidentStage.Approved);
                incident.Advance(IncidentStage.Closed);
                break;
            case IncidentStage.ClosedByDemoReset:
                incident.CloseByDemoReset();
                break;
        }

        return incident;
    }

    private static void Rejects<T>(Func<T> act, string message)
    {
        act.Should().Throw<DomainRuleException>().WithMessage(message);
    }
}

internal static class LifecyclePairs
{
    public static void AssertAll()
    {
        Allows(IncidentStage.TicketOpened, IncidentStage.Investigating);
        Allows(IncidentStage.Investigating, IncidentStage.RecommendationReady);
        Allows(IncidentStage.Investigating, IncidentStage.Escalated);
        Allows(IncidentStage.RecommendationReady, IncidentStage.AwaitingApproval);
        Allows(IncidentStage.RecommendationReady, IncidentStage.Escalated);
        Allows(IncidentStage.Escalated, IncidentStage.Investigating);
        Allows(IncidentStage.Escalated, IncidentStage.AwaitingApproval);
        Allows(IncidentStage.Escalated, IncidentStage.Closed);
        Allows(IncidentStage.AwaitingApproval, IncidentStage.Approved);
        Allows(IncidentStage.AwaitingApproval, IncidentStage.Rejected);
        Allows(IncidentStage.AwaitingApproval, IncidentStage.Escalated);
        Allows(IncidentStage.Approved, IncidentStage.Closed);
        Allows(IncidentStage.Rejected, IncidentStage.Investigating);
        Allows(IncidentStage.Rejected, IncidentStage.Closed);
        Allows(IncidentStage.TicketOpened, IncidentStage.ClosedByDemoReset);
        Allows(IncidentStage.Investigating, IncidentStage.ClosedByDemoReset);
        Allows(IncidentStage.RecommendationReady, IncidentStage.ClosedByDemoReset);
        Allows(IncidentStage.Escalated, IncidentStage.ClosedByDemoReset);
        Allows(IncidentStage.AwaitingApproval, IncidentStage.ClosedByDemoReset);
        Allows(IncidentStage.Approved, IncidentStage.ClosedByDemoReset);
        Allows(IncidentStage.Rejected, IncidentStage.ClosedByDemoReset);

        Refuses(IncidentStage.TicketOpened, IncidentStage.Approved);
        Refuses(IncidentStage.TicketOpened, IncidentStage.TicketOpened);
        Refuses(IncidentStage.Closed, IncidentStage.ClosedByDemoReset);
        Refuses(IncidentStage.Closed, IncidentStage.Investigating);
        Refuses(IncidentStage.ClosedByDemoReset, IncidentStage.TicketOpened);
        Refuses(IncidentStage.Approved, IncidentStage.Rejected);

        DemoReset.StatusLabel.Should().Be("Closed by reset");
    }

    private static void Allows(IncidentStage from, IncidentStage to)
    {
        IncidentLifecycle.CanTransition(from, to).Should().BeTrue();
    }

    private static void Refuses(IncidentStage from, IncidentStage to)
    {
        IncidentLifecycle.CanTransition(from, to).Should().BeFalse();
    }
}
