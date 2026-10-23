// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Approval;
using Mps.Domain.Capa;
using Mps.Domain.Identity;
using Mps.Domain.Lifecycle;

namespace Mps.Domain.Tests;

public sealed class CapaActionTests
{
    private static readonly byte[] Key = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

    private static readonly DateTimeOffset Now = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);

    private static readonly IncidentId Incident = new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    private const string Summary = "Hold the lot.";

    private const string Hash = "2D66E8629E1AA2F8180155857D78AF172E566B8F2026F7AD04C605DAD9DE6734";

    [Fact]
    public void TheActionHashIsTheIncidentAndTheTrimmedSummary()
    {
        CapaPairs.AssertAll();
        CapaAction.ComputeActionHash(Incident, "  Hold the lot.  ").Should().Be(Hash);
        CapaAction.ComputeActionHash(Incident, Summary).Should().Be(Hash);
        CapaAction.ComputeActionHash(IncidentId.New(), Summary).Should().NotBe(Hash);
        CapaAction.ComputeActionHash(Incident, "Scrap the lot.").Should().NotBe(Hash);

        var emptyIncident = () => CapaAction.ComputeActionHash(default, Summary);
        emptyIncident.Should().Throw<DomainRuleException>().WithMessage("Incident id is required.");
        var emptySummary = () => CapaAction.ComputeActionHash(Incident, "  ");
        emptySummary.Should().Throw<DomainRuleException>().WithMessage("CAPA summary is required.");
        var nullSummary = () => CapaAction.ComputeActionHash(Incident, null!);
        nullSummary.Should().Throw<DomainRuleException>().WithMessage("CAPA summary is required.");
    }

    [Fact]
    public void OpenStoresTheTrimmedSummaryAndTheConsumedToken()
    {
        CapaPairs.AssertAll();
        var token = TokenFor("  " + Summary + "  ");
        var capa = CapaAction.Open(CapaId.New(), Incident, "  " + Summary + "  ", token, Now, Key);

        capa.IncidentId.Should().Be(Incident);
        capa.Summary.Should().Be(Summary);
        capa.ActionHash.Should().Be(Hash);
        capa.ApprovalTokenId.Should().Be(token.Id);
        capa.Stage.Should().Be(CapaStage.Open);
        capa.OpenedAt.Should().Be(Now);
        token.IsConsumed.Should().BeTrue();
    }

    [Fact]
    public void OpenRejectsAnEmptyIdOrSummaryAndLeavesAFailedTokenUnused()
    {
        CapaPairs.AssertAll();
        var token = TokenFor(Summary);
        var emptyId = () => CapaAction.Open(default, Incident, Summary, token, Now, Key);
        emptyId.Should().Throw<DomainRuleException>().WithMessage("CAPA id is required.");
        token.IsConsumed.Should().BeFalse();

        var emptySummary = () => CapaAction.Open(CapaId.New(), Incident, " ", token, Now, Key);
        emptySummary.Should().Throw<DomainRuleException>().WithMessage("CAPA summary is required.");
        token.IsConsumed.Should().BeFalse();
    }

    [Fact]
    public void RehydrateChecksTheHashAndKeepsTheStage()
    {
        CapaPairs.AssertAll();
        var id = CapaId.New();
        var tokenId = ApprovalTokenId.New();
        var capa = CapaAction.Rehydrate(id, Incident, "  Hold the lot.  ", tokenId, "  " + Hash + "  ", CapaStage.Verification, Now);

        capa.Id.Should().Be(id);
        capa.IncidentId.Should().Be(Incident);
        capa.Summary.Should().Be(Summary);
        capa.ApprovalTokenId.Should().Be(tokenId);
        capa.ActionHash.Should().Be(Hash);
        capa.Stage.Should().Be(CapaStage.Verification);
        capa.OpenedAt.Should().Be(Now);

        Rejects(() => CapaAction.Rehydrate(default, Incident, Summary, tokenId, Hash, CapaStage.Open, Now), "CAPA id is required.");
        Rejects(() => CapaAction.Rehydrate(id, default, Summary, tokenId, Hash, CapaStage.Open, Now), "Incident id is required.");
        Rejects(() => CapaAction.Rehydrate(id, Incident, " ", tokenId, Hash, CapaStage.Open, Now), "CAPA summary is required.");
        Rejects(() => CapaAction.Rehydrate(id, Incident, null!, tokenId, Hash, CapaStage.Open, Now), "CAPA summary is required.");
        Rejects(() => CapaAction.Rehydrate(id, Incident, Summary, tokenId, " ", CapaStage.Open, Now), "Action hash is required.");
        Rejects(() => CapaAction.Rehydrate(id, Incident, Summary, tokenId, null!, CapaStage.Open, Now), "Action hash is required.");
        Rejects(() => CapaAction.Rehydrate(id, Incident, Summary, tokenId, Hash.ToLowerInvariant(), CapaStage.Open, Now), "CAPA action hash does not match the incident and summary.");
        Rejects(() => CapaAction.Rehydrate(id, Incident, "Scrap the lot.", tokenId, Hash, CapaStage.Open, Now), "CAPA action hash does not match the incident and summary.");
    }

    [Fact]
    public void EveryCapaEdgeIsOneStepAndASkipOrRepeatIsRejected()
    {
        CapaPairs.AssertAll();
        var capa = CapaAction.Open(CapaId.New(), Incident, Summary, TokenFor(Summary), Now, Key);

        var repeat = () => capa.Advance(CapaStage.Open);
        repeat.Should().Throw<DomainRuleException>().WithMessage("*Cannot move a CAPA from Open to Open*");

        capa.Advance(CapaStage.Containment);
        var backward = () => capa.Advance(CapaStage.Open);
        backward.Should().Throw<DomainRuleException>().WithMessage("*Cannot move a CAPA from Containment to Open*");
        capa.Stage.Should().Be(CapaStage.Containment);

        capa.Advance(CapaStage.RootCauseConfirmed);
        capa.Advance(CapaStage.CorrectiveAction);
        capa.Advance(CapaStage.Verification);
        capa.Advance(CapaStage.Closed);
        capa.Stage.Should().Be(CapaStage.Closed);

        var afterClose = () => capa.Advance(CapaStage.Verification);
        afterClose.Should().Throw<DomainRuleException>().WithMessage("*Cannot move a CAPA from Closed to Verification*");
    }

    private static ApprovalToken TokenFor(string summary)
    {
        return ApprovalToken.Issue(
            ApprovalTokenId.New(),
            "eng-17",
            CapaAction.ComputeActionHash(Incident, summary.Trim()),
            Now,
            TimeSpan.FromMinutes(15),
            Key);
    }

    private static void Rejects<T>(Func<T> act, string message)
    {
        act.Should().Throw<DomainRuleException>().WithMessage(message);
    }
}

internal static class CapaPairs
{
    public static void AssertAll()
    {
        CapaLifecycle.CanTransition(CapaStage.Open, CapaStage.Containment).Should().BeTrue();
        CapaLifecycle.CanTransition(CapaStage.Containment, CapaStage.RootCauseConfirmed).Should().BeTrue();
        CapaLifecycle.CanTransition(CapaStage.RootCauseConfirmed, CapaStage.CorrectiveAction).Should().BeTrue();
        CapaLifecycle.CanTransition(CapaStage.CorrectiveAction, CapaStage.Verification).Should().BeTrue();
        CapaLifecycle.CanTransition(CapaStage.Verification, CapaStage.Closed).Should().BeTrue();

        CapaLifecycle.CanTransition(CapaStage.Open, CapaStage.Open).Should().BeFalse();
        CapaLifecycle.CanTransition(CapaStage.Open, CapaStage.Closed).Should().BeFalse();
        CapaLifecycle.CanTransition(CapaStage.Closed, CapaStage.Verification).Should().BeFalse();
        CapaLifecycle.CanTransition(CapaStage.Containment, CapaStage.Open).Should().BeFalse();
    }
}
