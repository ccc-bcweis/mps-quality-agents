// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;

namespace Mps.Api.IntegrationTests;

/// <summary>
/// Missing or contradictory evidence escalates. The investigation names the gap or the conflict
/// and does not assert a root cause. Spec sections 2.4, 2.5, and 2.7 (S2).
/// </summary>
[Trait("Category", "PendingLane")]
public sealed class EvidenceEscalationTests : IClassFixture<MpsApiFactory>
{
    private readonly MpsApiFactory _factory;

    public EvidenceEscalationTests(MpsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MissingEvidenceEscalatesWithoutAssertingARootCause()
    {
        using var client = _factory.CreateClient();
        var started = await IncidentFlow.OpenAndStartAsync(
            client,
            "E-02",
            ApiClient.Unique("endpointTime"),
            "Endpoint time stepped. The calibration record is missing.");

        var investigation = started.Investigation;
        investigation.RequiredString("stage").Should().Be("escalated");
        investigation.RequiredString("escalationReason").Should().NotBeNullOrWhiteSpace();
        investigation.GetProperty("hypotheses").GetArrayLength().Should().Be(0);
        await IncidentFlow.AssertNoStoredCapaAsync(client, started.IncidentId);
    }

    [Fact]
    public async Task ContradictoryEvidenceNamesTheConflictAndDoesNotAssertARootCause()
    {
        using var client = _factory.CreateClient();
        var started = await IncidentFlow.OpenAndStartAsync(
            client,
            "D-01",
            "chamberPressure",
            "Deposition pressure step. PLM showerhead revision and the MES run header disagree, and the calibration record is missing.");

        var investigation = started.Investigation;
        investigation.RequiredString("stage").Should().Be("escalated");
        var reason = investigation.RequiredString("escalationReason");
        reason.Should().MatchRegex("(?i)(contradict|conflict|mismatch)");
        reason.Should().MatchRegex("(?i)rev(ision)?\\s*C");
        reason.Should().MatchRegex("(?i)rev(ision)?\\s*B");
        investigation.GetProperty("hypotheses").GetArrayLength().Should().Be(0);
        await IncidentFlow.AssertNoStoredCapaAsync(client, started.IncidentId);
    }
}
