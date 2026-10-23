// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Identity;

namespace Mps.Domain.Tests;

public sealed class IdsTests
{
    private static readonly Guid Sample = Guid.Parse("11111111-1111-4111-8111-111111111111");

    [Fact]
    public void ToolIdTrimsAndRejectsABlank()
    {
        var id = new ToolId("  E-02  ");

        id.Value.Should().Be("E-02");
        id.ToString().Should().Be("E-02");
        id.IsEmpty.Should().BeFalse();

        default(ToolId).IsEmpty.Should().BeTrue();
        default(ToolId).ToString().Should().BeEmpty();

        Rejects(() => new ToolId(null!), "Tool id is required.");
        Rejects(() => new ToolId(string.Empty), "Tool id is required.");
        Rejects(() => new ToolId("   "), "Tool id is required.");
    }

    [Fact]
    public void IncidentIdRejectsEmptyAndPrintsTheGuid()
    {
        var id = new IncidentId(Sample);

        id.Value.Should().Be(Sample);
        id.IsEmpty.Should().BeFalse();
        id.ToString().Should().Be("11111111-1111-4111-8111-111111111111");
        default(IncidentId).IsEmpty.Should().BeTrue();
        default(IncidentId).ToString().Should().Be("00000000-0000-0000-0000-000000000000");
        IncidentId.New().IsEmpty.Should().BeFalse();
        (new IncidentId(Sample) == id).Should().BeTrue();

        Rejects(() => new IncidentId(Guid.Empty), "Incident id is required.");
    }

    [Fact]
    public void AlertIdRejectsEmptyAndPrintsTheGuid()
    {
        var id = new AlertId(Sample);

        id.Value.Should().Be(Sample);
        id.IsEmpty.Should().BeFalse();
        id.ToString().Should().Be("11111111-1111-4111-8111-111111111111");
        default(AlertId).IsEmpty.Should().BeTrue();
        AlertId.New().IsEmpty.Should().BeFalse();

        Rejects(() => new AlertId(Guid.Empty), "Alert id is required.");
    }

    [Fact]
    public void TicketIdRejectsEmptyAndPrintsTheGuid()
    {
        var id = new TicketId(Sample);

        id.Value.Should().Be(Sample);
        id.IsEmpty.Should().BeFalse();
        id.ToString().Should().Be("11111111-1111-4111-8111-111111111111");
        default(TicketId).IsEmpty.Should().BeTrue();
        TicketId.New().IsEmpty.Should().BeFalse();

        Rejects(() => new TicketId(Guid.Empty), "Ticket id is required.");
    }

    [Fact]
    public void EvidenceIdRejectsEmptyAndPrintsTheGuid()
    {
        var id = new EvidenceId(Sample);

        id.Value.Should().Be(Sample);
        id.IsEmpty.Should().BeFalse();
        id.ToString().Should().Be("11111111-1111-4111-8111-111111111111");
        default(EvidenceId).IsEmpty.Should().BeTrue();
        EvidenceId.New().IsEmpty.Should().BeFalse();

        Rejects(() => new EvidenceId(Guid.Empty), "Evidence id is required.");
    }

    [Fact]
    public void HypothesisIdRejectsEmptyAndPrintsTheGuid()
    {
        var id = new HypothesisId(Sample);

        id.Value.Should().Be(Sample);
        id.IsEmpty.Should().BeFalse();
        id.ToString().Should().Be("11111111-1111-4111-8111-111111111111");
        default(HypothesisId).IsEmpty.Should().BeTrue();
        HypothesisId.New().IsEmpty.Should().BeFalse();

        Rejects(() => new HypothesisId(Guid.Empty), "Hypothesis id is required.");
    }

    [Fact]
    public void ApprovalTokenIdRejectsEmptyAndPrintsTheGuid()
    {
        var id = new ApprovalTokenId(Sample);

        id.Value.Should().Be(Sample);
        id.IsEmpty.Should().BeFalse();
        id.ToString().Should().Be("11111111-1111-4111-8111-111111111111");
        default(ApprovalTokenId).IsEmpty.Should().BeTrue();
        ApprovalTokenId.New().IsEmpty.Should().BeFalse();

        Rejects(() => new ApprovalTokenId(Guid.Empty), "Approval token id is required.");
    }

    [Fact]
    public void CapaIdRejectsEmptyAndPrintsTheGuid()
    {
        var id = new CapaId(Sample);

        id.Value.Should().Be(Sample);
        id.IsEmpty.Should().BeFalse();
        id.ToString().Should().Be("11111111-1111-4111-8111-111111111111");
        default(CapaId).IsEmpty.Should().BeTrue();
        CapaId.New().IsEmpty.Should().BeFalse();

        Rejects(() => new CapaId(Guid.Empty), "CAPA id is required.");
    }

    [Fact]
    public void InvestigationIdRejectsEmptyAndPrintsTheGuid()
    {
        var id = new InvestigationId(Sample);

        id.Value.Should().Be(Sample);
        id.IsEmpty.Should().BeFalse();
        id.ToString().Should().Be("11111111-1111-4111-8111-111111111111");
        default(InvestigationId).IsEmpty.Should().BeTrue();
        InvestigationId.New().IsEmpty.Should().BeFalse();

        Rejects(() => new InvestigationId(Guid.Empty), "Investigation id is required.");
    }

    private static void Rejects<T>(Func<T> act, string message)
    {
        act.Should().Throw<DomainRuleException>().WithMessage(message);
    }
}
