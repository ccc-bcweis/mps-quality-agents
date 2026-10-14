// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Approval;
using Mps.Domain.Capa;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;

namespace Mps.Domain.Tests;

public sealed class CapaInvariantTests
{
    private static readonly byte[] Key = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

    private static readonly DateTimeOffset Now = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OpenCapaConsumesTheApprovalToken()
    {
        var incidentId = IncidentId.New();
        const string summary = "Hold lot NF-3317 and replace the edge ring.";
        var token = TokenFor(incidentId, summary);

        var capa = CapaAction.Open(CapaId.New(), incidentId, summary, token, Now, Key);

        capa.Stage.Should().Be(CapaStage.Open);
        capa.ApprovalTokenId.Should().Be(token.Id);
        token.IsConsumed.Should().BeTrue();
    }

    [Fact]
    public void OpenCapaRejectsAMissingToken()
    {
        var act = () => CapaAction.Open(
            CapaId.New(),
            IncidentId.New(),
            "Contain the tool.",
            null!,
            Now,
            Key);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RehydrateRejectsACapaWithoutAnApprovalToken()
    {
        var incidentId = IncidentId.New();
        const string summary = "Contain the tool.";
        var act = () => CapaAction.Rehydrate(
            CapaId.New(),
            incidentId,
            summary,
            default,
            CapaAction.ComputeActionHash(incidentId, summary),
            CapaStage.Open,
            Now);

        act.Should().Throw<DomainRuleException>().WithMessage("*approval token*");
    }

    [Fact]
    public void ATokenCannotOpenTwoCapas()
    {
        var incidentId = IncidentId.New();
        const string summary = "Replace the showerhead.";
        var token = TokenFor(incidentId, summary);
        _ = CapaAction.Open(CapaId.New(), incidentId, summary, token, Now, Key);

        var act = () => CapaAction.Open(CapaId.New(), incidentId, summary, token, Now, Key);

        act.Should().Throw<DomainRuleException>().WithMessage("*single-use*");
    }

    [Fact]
    public void AnExpiredTokenCannotOpenACapa()
    {
        var incidentId = IncidentId.New();
        const string summary = "Retire the gas lot.";
        var hash = CapaAction.ComputeActionHash(incidentId, summary);
        var token = ApprovalToken.Issue(
            ApprovalTokenId.New(),
            "eng-17",
            hash,
            Now.AddHours(-2),
            TimeSpan.FromMinutes(15),
            Key);

        var act = () => CapaAction.Open(CapaId.New(), incidentId, summary, token, Now, Key);

        act.Should().Throw<DomainRuleException>().WithMessage("*expired*");
    }

    [Fact]
    public void ATokenBoundToAnotherActionCannotOpenACapa()
    {
        var incidentId = IncidentId.New();
        var token = TokenFor(incidentId, "Hold the lot.");

        var act = () => CapaAction.Open(CapaId.New(), incidentId, "Scrap the lot.", token, Now, Key);

        act.Should().Throw<DomainRuleException>().WithMessage("*bound*");
    }

    [Fact]
    public void ATamperedSignatureCannotOpenACapa()
    {
        var incidentId = IncidentId.New();
        const string summary = "Recalibrate the manometer.";
        var issued = TokenFor(incidentId, summary);
        var tampered = ApprovalToken.Rehydrate(
            issued.Id,
            issued.ApproverId,
            issued.ActionHash,
            issued.IssuedAt,
            issued.ExpiresAt,
            new string('A', issued.Signature.Length),
            consumed: false);

        var act = () => CapaAction.Open(CapaId.New(), incidentId, summary, tampered, Now, Key);

        act.Should().Throw<DomainRuleException>().WithMessage("*signature*");
    }

    [Fact]
    public void TokenLifetimeMustBePositive()
    {
        var act = () => ApprovalToken.Issue(
            ApprovalTokenId.New(),
            "eng-17",
            "ABC",
            Now,
            TimeSpan.Zero,
            Key);

        act.Should().Throw<DomainRuleException>().WithMessage("*lifetime*");
    }

    [Fact]
    public void CapaAdvancesOneStageAtATime()
    {
        var incidentId = IncidentId.New();
        const string summary = "Clean the chamber.";
        var capa = CapaAction.Open(CapaId.New(), incidentId, summary, TokenFor(incidentId, summary), Now, Key);

        var skip = () => capa.Advance(CapaStage.Closed);
        skip.Should().Throw<DomainRuleException>().WithMessage("*Cannot move a CAPA*");

        capa.Advance(CapaStage.Containment);
        capa.Advance(CapaStage.RootCauseConfirmed);
        capa.Advance(CapaStage.CorrectiveAction);
        capa.Advance(CapaStage.Verification);
        capa.Advance(CapaStage.Closed);

        capa.Stage.Should().Be(CapaStage.Closed);
    }

    private static ApprovalToken TokenFor(IncidentId incidentId, string summary)
    {
        return ApprovalToken.Issue(
            ApprovalTokenId.New(),
            "eng-17",
            CapaAction.ComputeActionHash(incidentId, summary),
            Now,
            TimeSpan.FromMinutes(15),
            Key);
    }
}

public sealed class HypothesisInvariantTests
{
    [Fact]
    public void HypothesisRequiresCitedEvidence()
    {
        var act = () => new Hypothesis(
            HypothesisId.New(),
            RootCauseCategory.EngineeringChange,
            "Edge ring revision changed.",
            0.8,
            [],
            ["PM was performed the same week."],
            ["Compare the ECO to the run header."]);

        act.Should().Throw<DomainRuleException>().WithMessage("*cite at least one*");
    }

    [Fact]
    public void ConfidenceMustBeAFraction()
    {
        var act = () => new Hypothesis(
            HypothesisId.New(),
            RootCauseCategory.ProcessDrift,
            "RF hours since clean.",
            1.2,
            [EvidenceId.New()],
            [],
            []);

        act.Should().Throw<DomainRuleException>().WithMessage("*between 0 and 1*");
    }

    [Fact]
    public void HypothesisKeepsCounterEvidence()
    {
        var evidenceId = EvidenceId.New();
        var hypothesis = new Hypothesis(
            HypothesisId.New(),
            RootCauseCategory.SupplyChainMaterial,
            "Gas lot moisture is out of spec.",
            0.66,
            [evidenceId],
            ["An ECO landed the same week on one tool."],
            ["Quarantine the lot."]);

        hypothesis.EvidenceIds.Should().Contain(evidenceId);
        hypothesis.CounterEvidence.Should().Contain(item => item.Contains("ECO", StringComparison.Ordinal));
    }
}

public sealed class IncidentInvariantTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IncidentRequiresACorrelationId()
    {
        var act = () => Incident.Open(
            IncidentId.New(),
            new ToolId("E-02"),
            "C1",
            "escTemperature",
            IncidentChannel.IotSpcAlert,
            Severity.High,
            "ESC temperature oscillation.",
            "  ",
            Now);

        act.Should().Throw<DomainRuleException>().WithMessage("*Correlation id*");
    }

    [Fact]
    public void TicketNumberUsesTheTktPrefix()
    {
        var act = () => new Ticket(TicketId.New(), "INC-10", IncidentId.New(), new ToolId("E-02"), "Oscillation", Now);

        act.Should().Throw<DomainRuleException>().WithMessage("*TKT-*");
    }

    [Fact]
    public void IllegalIncidentTransitionIsRejected()
    {
        var incident = OpenIncident();

        var act = () => incident.Advance(IncidentStage.Approved);

        act.Should().Throw<DomainRuleException>().WithMessage("*Cannot move an incident*");
    }

    [Fact]
    public void IncidentCanReachApprovalThroughTheLegalPath()
    {
        var incident = OpenIncident();

        incident.Advance(IncidentStage.Investigating);
        incident.Advance(IncidentStage.RecommendationReady);
        incident.Advance(IncidentStage.AwaitingApproval);
        incident.Advance(IncidentStage.Approved);

        incident.Stage.Should().Be(IncidentStage.Approved);
    }

    [Fact]
    public void ApprovalDecisionRequiresAToken()
    {
        var act = () => new HumanDecision(HumanDecisionKind.Approve, "Looks grounded.", null);

        act.Should().Throw<DomainRuleException>().WithMessage("*approval token*");
    }

    [Fact]
    public void EscalationNamesTheGap()
    {
        var act = () => new InvestigationSnapshot(
            InvestigationId.New(),
            IncidentId.New(),
            InvestigationStage.Escalated,
            [],
            [],
            "  ",
            "corr-1");

        act.Should().Throw<DomainRuleException>().WithMessage("*gap or conflict*");
    }

    private static Incident OpenIncident()
    {
        return Incident.Open(
            IncidentId.New(),
            new ToolId("E-02"),
            "C1",
            "escTemperature",
            IncidentChannel.FaultInjector,
            Severity.High,
            "ESC temperature oscillation.",
            "corr-s1",
            Now);
    }
}

public sealed class ToolInvariantTests
{
    [Fact]
    public void ToolRequiresAChamber()
    {
        var act = () => new Mps.Domain.Equipment.Tool(
            new ToolId("E-02"),
            "Etch 02",
            Mps.Domain.Equipment.ToolFamily.Etch,
            "MPS-E7",
            "Bay B",
            []);

        act.Should().Throw<DomainRuleException>().WithMessage("*at least one chamber*");
    }
}
