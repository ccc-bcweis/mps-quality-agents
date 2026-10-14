// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;

namespace Mps.Api.IntegrationTests;

/// <summary>
/// Approval is a human decision. The server issues the single-use token. A client cannot
/// post a CAPA, send a signature, or approve twice. Spec sections 2.5, 5, and 7.4.
/// </summary>
[Trait("Category", "PendingLane")]
public sealed class ApprovalEnforcementTests : IClassFixture<MpsApiFactory>
{
    private readonly MpsApiFactory _factory;

    public ApprovalEnforcementTests(MpsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ApproveWithoutApproverIdDoesNotCreateACapa()
    {
        using var client = _factory.CreateClient();
        var started = await IncidentFlow.OpenAndStartAsync(
            client,
            "E-02",
            ApiClient.Unique("chamberPressure"),
            "Pressure step with no approval identity.");

        var decision = await IncidentFlow.DecideAsync(client, started.InvestigationId, new { kind = "approve" });
        decision.Status.Should().BeOneOf(
            new[] { HttpStatusCode.BadRequest, HttpStatusCode.Forbidden },
            "response body was {0}",
            decision.Body);
        await IncidentFlow.AssertNoStoredCapaAsync(client, started.IncidentId);
    }

    [Fact]
    public async Task ClientSuppliedSignatureIsRejected()
    {
        using var client = _factory.CreateClient();
        var started = await IncidentFlow.OpenAndStartAsync(
            client,
            "E-02",
            ApiClient.Unique("rfPower"),
            "Client tries to send its own approval signature.");

        var decision = await IncidentFlow.DecideAsync(
            client,
            started.InvestigationId,
            new { kind = "approve", approverId = "eng-17", signature = "deadbeef" });
        _ = decision.Require(HttpStatusCode.BadRequest);
        await IncidentFlow.AssertNoStoredCapaAsync(client, started.IncidentId);
    }

    [Fact]
    public async Task RejectOnAnEscalatedInvestigationDoesNotCreateACapa()
    {
        using var client = _factory.CreateClient();
        var started = await IncidentFlow.OpenAndStartAsync(
            client,
            "E-02",
            ApiClient.Unique("gasFlow"),
            "No evidence is available for this parameter.");
        started.Investigation.GetProperty("stage").GetString().Should().Be("escalated");

        var decision = await IncidentFlow.DecideAsync(
            client,
            started.InvestigationId,
            new { kind = "reject", comment = "Not enough evidence." });
        decision.Status.Should().BeOneOf(
            new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Forbidden },
            "response body was {0}",
            decision.Body);
        if (decision.Status == HttpStatusCode.OK)
        {
            var body = decision.Json();
            IncidentFlow.AssertDecisionHasNoCapa(body);
            body.GetProperty("investigation").RequiredString("stage").Should().NotBe("approved");
        }

        await IncidentFlow.AssertNoStoredCapaAsync(client, started.IncidentId);
    }

    [Fact]
    public async Task RequestInfoDoesNotCreateACapa()
    {
        using var client = _factory.CreateClient();
        var started = await IncidentFlow.OpenAndStartAsync(
            client,
            "D-01",
            ApiClient.Unique("heaterTemperature"),
            "Calibration record is missing.");

        var decision = await IncidentFlow.DecideAsync(
            client,
            started.InvestigationId,
            new { kind = "requestInfo", comment = "Need the calibration record." });
        var body = decision.Require(HttpStatusCode.OK);
        IncidentFlow.AssertDecisionHasNoCapa(body);
        body.GetProperty("investigation").RequiredString("stage").Should().NotBe("approved");
        await IncidentFlow.AssertNoStoredCapaAsync(client, started.IncidentId);
    }

    [Fact]
    public async Task ApprovedRecommendationOpensOneCapaAndRejectsReuseAndSkips()
    {
        using var client = _factory.CreateClient();
        var started = await IncidentFlow.OpenAndStartAsync(
            client,
            "E-02",
            "escTemperature",
            "ESC temperature oscillation after ECO-4471 edge ring rev C. A PM was done the same week.");
        var stage = started.Investigation.RequiredString("stage");
        stage.Should().BeOneOf("recommendationReady", "awaitingApproval");
        var hypotheses = started.Investigation.GetProperty("hypotheses");
        hypotheses.GetArrayLength().Should().BeGreaterThan(0);
        hypotheses[0].GetProperty("category").GetString().Should().Be("engineeringChange");
        hypotheses[0].GetProperty("counterEvidence").GetArrayLength().Should().BeGreaterThan(0);

        var approved = (await IncidentFlow.DecideAsync(
            client,
            started.InvestigationId,
            new { kind = "approve", approverId = "eng-17" })).Require(HttpStatusCode.OK);
        approved.GetProperty("investigation").RequiredString("stage").Should().Be("approved");
        var capa = approved.GetProperty("capa");
        capa.RequiredString("stage").Should().Be("open");
        Guid.TryParse(capa.RequiredString("approvalTokenId"), out _).Should().BeTrue();
        capa.RequiredString("actionHash").Should().NotBeNullOrWhiteSpace();
        var capaId = capa.RequiredString("id");

        var again = await IncidentFlow.DecideAsync(
            client,
            started.InvestigationId,
            new { kind = "approve", approverId = "eng-17" });
        again.Status.Should().BeOneOf(
            new[] { HttpStatusCode.BadRequest, HttpStatusCode.Forbidden },
            "response body was {0}",
            again.Body);
        var listed = (await ApiClient.SendAsync(client, HttpMethod.Get, "/api/capas")).Require(HttpStatusCode.OK);
        listed.EnumerateArray().Count(item => item.RequiredString("incidentId") == started.IncidentId).Should().Be(1);

        var skip = await ApiClient.SendAsync(
            client,
            HttpMethod.Post,
            "/api/capas/" + capaId + "/advance",
            new { stage = "closed" });
        _ = skip.Require(HttpStatusCode.Conflict);

        var step = (await ApiClient.SendAsync(
            client,
            HttpMethod.Post,
            "/api/capas/" + capaId + "/advance",
            new { stage = "containment" })).Require(HttpStatusCode.OK);
        step.RequiredString("stage").Should().Be("containment");

        var backward = await ApiClient.SendAsync(
            client,
            HttpMethod.Post,
            "/api/capas/" + capaId + "/advance",
            new { stage = "open" });
        _ = backward.Require(HttpStatusCode.Conflict);
    }
}
