// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

using Mps.Api.Fakes;
using Mps.Application.Persistence;
using Mps.Domain.Approval;
using Mps.Domain.Capa;
using Mps.Domain.Identity;

namespace Mps.Api.Tests;

public sealed class QualityApiTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory;

    public QualityApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task MetricsReportCompletionGroundingLatencyAndCost()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var opened = await OpenIncidentAsync(client, "D-02", "filmThickness");
        var incidentId = opened.GetProperty("id").GetGuid();
        var started = await client.PostAsync("/api/incidents/" + incidentId + "/investigations", null);
        var investigationId = (await started.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var before = await client.GetFromJsonAsync<JsonElement>("/api/metrics");
        var approved = await client.PostAsJsonAsync("/api/investigations/" + investigationId + "/decisions", new
        {
            kind = "approve",
            approverId = "eng-17",
            comment = "Hold the lot.",
        });
        var after = await client.GetFromJsonAsync<JsonElement>("/api/metrics");

        started.StatusCode.Should().Be(HttpStatusCode.Created);
        before.GetProperty("mode").GetString().Should().Be("replay");
        before.GetProperty("completionRate").GetDouble().Should().Be(0d);
        before.GetProperty("groundingScore").GetDouble().Should().Be(1d);
        before.GetProperty("categoryAccuracy").ValueKind.Should().Be(JsonValueKind.Null);
        before.GetProperty("toolFailureCount").GetInt32().Should().Be(0);
        before.GetProperty("latencyP95Ms").GetDouble().Should().BeGreaterThanOrEqualTo(0d);
        before.GetProperty("costPerInvestigationUsd").ValueKind.Should().Be(JsonValueKind.Null);
        before.GetProperty("spendTodayUsd").GetDouble().Should().Be(0d);
        before.GetProperty("spendCapUsd").GetDouble().Should().Be(2d);
        approved.StatusCode.Should().Be(HttpStatusCode.OK);
        after.GetProperty("completionRate").GetDouble().Should().Be(1d);
        after.GetProperty("costPerInvestigationUsd").GetDouble().Should().Be(0d);
        after.GetProperty("categoryAccuracy").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task HealthReportsOk()
    {
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<JsonElement>("/health");

        body.GetProperty("status").GetString().Should().Be("ok");
        body.GetProperty("service").GetString().Should().Be("Mps.Api");
    }

    [Fact]
    public async Task FleetListsEightFictionalTools()
    {
        var client = _factory.CreateClient();

        var tools = await client.GetFromJsonAsync<JsonElement>("/api/tools");
        var missing = await client.GetAsync("/api/tools/Z-99");

        tools.GetArrayLength().Should().Be(8);
        tools.EnumerateArray().Select(tool => tool.GetProperty("model").GetString()).Should().OnlyContain(model => model != null && model.StartsWith("MPS-", StringComparison.Ordinal));
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OperatorReportOpensAnIncidentAndASecondReportLinksToIt()
    {
        var client = _factory.CreateClient();
        var parameter = "escTemperature-" + Guid.NewGuid().ToString("N");
        var first = await client.PostAsJsonAsync("/api/incidents", new
        {
            toolId = "E-02",
            chamberId = "A",
            parameter,
            summary = "ESC temperature drifted after the recipe step.",
            severity = "medium",
        });
        var created = await first.Content.ReadFromJsonAsync<JsonElement>(Json);
        var second = await client.PostAsJsonAsync("/api/incidents", new
        {
            toolId = "E-02",
            parameter,
            summary = "Same drift, reported again.",
        });
        var linked = await second.Content.ReadFromJsonAsync<JsonElement>(Json);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        created.GetProperty("channel").GetString().Should().Be("operatorReport");
        created.GetProperty("stage").GetString().Should().Be("ticketOpened");
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        linked.GetProperty("id").GetString().Should().Be(created.GetProperty("id").GetString());
    }

    [Fact]
    public async Task AnUnknownToolAndAHostileSummaryAreRejected()
    {
        var client = _factory.CreateClient();

        var unknown = await client.PostAsJsonAsync("/api/incidents", new
        {
            toolId = "Z-99",
            parameter = "escTemperature",
            summary = "Missing tool.",
        });
        var hostile = await client.PostAsJsonAsync("/api/incidents", new
        {
            toolId = "E-02",
            parameter = "escTemperature",
            summary = "stop\0here",
        });

        unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        unknown.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        hostile.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task StartingAnInvestigationRejectsASecondStartAndAnIllegalWorkflowSkipIsNotExposed()
    {
        var client = _factory.CreateClient();
        var opened = await OpenIncidentAsync(client, "D-01", "chamberPressure");
        var incidentId = opened.GetProperty("id").GetGuid();

        var first = await client.PostAsync("/api/incidents/" + incidentId + "/investigations", null);
        var again = await client.PostAsync("/api/incidents/" + incidentId + "/investigations", null);
        var body = await first.Content.ReadFromJsonAsync<JsonElement>(Json);
        var incident = await client.GetFromJsonAsync<JsonElement>("/api/incidents/" + incidentId);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        body.GetProperty("stage").GetString().Should().Be("recommendationReady");
        body.GetProperty("correlationId").GetString().Should().Be(opened.GetProperty("correlationId").GetString());
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        incident.GetProperty("stage").GetString().Should().Be("recommendationReady");
    }

    [Fact]
    public async Task LatestInvestigationFollowsTheIncidentAndReturnsTheNewest()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var opened = await OpenIncidentAsync(client, "E-02", "vibration");
        var incidentId = opened.GetProperty("id").GetGuid();
        var unknown = await client.GetAsync("/api/incidents/" + Guid.NewGuid().ToString("D") + "/investigation");
        var before = await client.GetAsync("/api/incidents/" + incidentId.ToString("D") + "/investigation");

        var firstResponse = await client.PostAsync("/api/incidents/" + incidentId.ToString("D") + "/investigations", null);
        var first = await firstResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var firstId = first.GetProperty("id").GetGuid();
        var afterFirst = await client.GetFromJsonAsync<JsonElement>("/api/incidents/" + incidentId.ToString("D") + "/investigation");
        var rejected = await client.PostAsJsonAsync("/api/investigations/" + firstId.ToString("D") + "/decisions", new
        {
            kind = "reject",
            comment = "The signal returned to the window.",
        });
        var secondResponse = await client.PostAsync("/api/incidents/" + incidentId.ToString("D") + "/investigations", null);
        var second = await secondResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var latest = await client.GetFromJsonAsync<JsonElement>("/api/incidents/" + incidentId.ToString("D") + "/investigation");

        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        before.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await before.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("title").GetString().Should().Be("No such record.");
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        afterFirst.GetProperty("id").GetGuid().Should().Be(firstId);
        afterFirst.GetProperty("incidentId").GetGuid().Should().Be(incidentId);
        rejected.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        latest.GetProperty("id").GetGuid().Should().Be(second.GetProperty("id").GetGuid());
        latest.GetProperty("id").GetGuid().Should().NotBe(firstId);
        latest.GetProperty("incidentId").GetGuid().Should().Be(incidentId);
    }

    [Fact]
    public async Task InvestigationKeepsWhyItMattersSignalIdAndTheEvidenceMatrix()
    {
        var client = _factory.CreateClient();
        var opened = await OpenIncidentAsync(client, "E-02", "escTemperature");
        var incidentId = opened.GetProperty("id").GetGuid();

        var started = await client.PostAsync("/api/incidents/" + incidentId + "/investigations", null);
        var created = await started.Content.ReadFromJsonAsync<JsonElement>(Json);
        var investigationId = created.GetProperty("id").GetGuid();
        var decision = await client.PostAsJsonAsync("/api/investigations/" + investigationId + "/decisions", new
        {
            kind = "reject",
            comment = "The signal returned to the window.",
        });
        var rejected = await decision.Content.ReadFromJsonAsync<JsonElement>(Json);
        var loaded = await client.GetFromJsonAsync<JsonElement>("/api/investigations/" + investigationId);

        started.StatusCode.Should().Be(HttpStatusCode.Created);
        decision.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertNarrative(created);
        AssertNarrative(rejected.GetProperty("investigation"));
        AssertNarrative(loaded);
    }

    [Fact]
    public async Task RejectMovesTheIncidentAndDoesNotCreateACapa()
    {
        var client = _factory.CreateClient();
        var opened = await OpenIncidentAsync(client, "S-01", "ashRate");
        var incidentId = opened.GetProperty("id").GetGuid();
        var started = await client.PostAsync("/api/incidents/" + incidentId + "/investigations", null);
        var investigation = await started.Content.ReadFromJsonAsync<JsonElement>(Json);
        var investigationId = investigation.GetProperty("id").GetGuid();

        var decision = await client.PostAsJsonAsync("/api/investigations/" + investigationId + "/decisions", new
        {
            kind = "reject",
            comment = "The signal returned to the window.",
        });
        var body = await decision.Content.ReadFromJsonAsync<JsonElement>(Json);
        var capas = await client.GetFromJsonAsync<JsonElement>("/api/capas");

        decision.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("investigation").GetProperty("stage").GetString().Should().Be("rejected");
        body.GetProperty("capa").ValueKind.Should().Be(JsonValueKind.Null);
        capas.EnumerateArray().Should().NotContain(capa => capa.GetProperty("incidentId").GetGuid() == incidentId);
    }

    [Fact]
    public async Task ApproveRequiresAnApproverAndThenCreatesOneCapa()
    {
        var client = _factory.CreateClient();
        var opened = await OpenIncidentAsync(client, "C-01", "particles");
        var incidentId = opened.GetProperty("id").GetGuid();
        var started = await client.PostAsync("/api/incidents/" + incidentId + "/investigations", null);
        var investigationId = (await started.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var missingApprover = await client.PostAsJsonAsync("/api/investigations/" + investigationId + "/decisions", new { kind = "approve" });
        var approved = await client.PostAsJsonAsync("/api/investigations/" + investigationId + "/decisions", new
        {
            kind = "approve",
            approverId = "eng-17",
            comment = "Hold the lot.",
        });
        var body = await approved.Content.ReadFromJsonAsync<JsonElement>(Json);
        var repeat = await client.PostAsJsonAsync("/api/investigations/" + investigationId + "/decisions", new
        {
            kind = "approve",
            approverId = "eng-17",
            comment = "Hold the lot.",
        });
        var repeated = await repeat.Content.ReadFromJsonAsync<JsonElement>(Json);
        var incident = await client.GetFromJsonAsync<JsonElement>("/api/incidents/" + incidentId);

        missingApprover.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        approved.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("investigation").GetProperty("stage").GetString().Should().Be("approved");
        body.GetProperty("capa").GetProperty("summary").GetString().Should().Be("Hold the lot.");
        body.GetProperty("capa").GetProperty("stage").GetString().Should().Be("open");
        body.GetProperty("capa").GetProperty("approvalTokenId").GetGuid().Should().NotBeEmpty();
        repeat.StatusCode.Should().Be(HttpStatusCode.OK);
        repeated.GetProperty("capa").GetProperty("id").GetString().Should().Be(body.GetProperty("capa").GetProperty("id").GetString());
        incident.GetProperty("stage").GetString().Should().Be("approved");
    }

    [Fact]
    public async Task AnIllegalCapaTransitionIsRejectedAndTheNextStageIsAccepted()
    {
        var client = _factory.CreateClient();
        var opened = await OpenIncidentAsync(client, "E-01", "rfPower");
        var incidentId = opened.GetProperty("id").GetGuid();
        var capaId = await SeedCapaAsync(incidentId);

        var skip = await client.PostAsJsonAsync("/api/capas/" + capaId + "/advance", new { stage = "closed" });
        var next = await client.PostAsJsonAsync("/api/capas/" + capaId + "/advance", new { stage = "containment" });
        var body = await next.Content.ReadFromJsonAsync<JsonElement>(Json);

        skip.StatusCode.Should().Be(HttpStatusCode.Conflict);
        next.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("stage").GetString().Should().Be("containment");
    }

    [Fact]
    public async Task FaultInjectionAcceptsTheFaultWithoutOpeningATicketByDefault()
    {
        var client = _factory.CreateClient();
        var parameter = "gasFlow-" + Guid.NewGuid().ToString("N");

        var response = await client.PostAsJsonAsync("/api/faults", FaultBody(parameter));
        var accepted = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var incidents = await client.GetFromJsonAsync<JsonElement>("/api/incidents");
        var alerts = await client.GetFromJsonAsync<JsonElement>("/api/alerts?toolId=E-02");
        var tickets = await client.GetFromJsonAsync<JsonElement>("/api/tickets");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        accepted.GetProperty("faultId").GetGuid().Should().NotBeEmpty();
        accepted.GetProperty("correlationId").GetString().Should().NotBeNullOrWhiteSpace();
        incidents.EnumerateArray().Should().NotContain(incident => incident.GetProperty("parameter").GetString() == parameter);
        alerts.EnumerateArray().Should().NotContain(alert => alert.GetProperty("parameter").GetString() == parameter);
        tickets.EnumerateArray().Should().NotContain(ticket => ticket.GetProperty("title").GetString()!.Contains(parameter, StringComparison.Ordinal));
    }

    [Fact]
    public async Task FaultInjectionOpensTheInjectorChannelWhenDirectTicketIsEnabled()
    {
        await using var factory = new ApiFactory().WithWebHostBuilder(builder =>
            builder.UseSetting("Mps:Injector:DirectTicket", "true"));
        var client = factory.CreateClient();
        var parameter = "gasFlow-" + Guid.NewGuid().ToString("N");

        var response = await client.PostAsJsonAsync("/api/faults", FaultBody(parameter));
        var accepted = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var incidents = await client.GetFromJsonAsync<JsonElement>("/api/incidents");
        var alerts = await client.GetFromJsonAsync<JsonElement>("/api/alerts?toolId=E-02");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        accepted.GetProperty("faultId").GetGuid().Should().NotBeEmpty();
        accepted.GetProperty("correlationId").GetString().Should().NotBeNullOrWhiteSpace();
        incidents.EnumerateArray().Should().Contain(incident =>
            incident.GetProperty("parameter").GetString() == parameter
            && incident.GetProperty("channel").GetString() == "faultInjector");
        alerts.EnumerateArray().Should().Contain(alert => alert.GetProperty("parameter").GetString() == parameter);
    }

    [Fact]
    public async Task SignalRReceivesTheIncidentWhenAnOperatorReports()
    {
        var client = _factory.CreateClient();
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/fleet"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>("incidentChanged", payload => received.TrySetResult(payload));
        await connection.StartAsync();
        var parameter = "vibration-" + Guid.NewGuid().ToString("N");

        var posted = await client.PostAsJsonAsync("/api/incidents", new
        {
            toolId = "S-02",
            parameter,
            summary = "Pump vibration climbed during the ash step.",
        });
        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

        posted.StatusCode.Should().Be(HttpStatusCode.Created);
        message.GetProperty("parameter").GetString().Should().Be(parameter);
        message.GetProperty("channel").GetString().Should().Be("operatorReport");
        await connection.StopAsync();
    }

    private static void AssertNarrative(JsonElement investigation)
    {
        var evidence = investigation.GetProperty("evidence");
        evidence.GetArrayLength().Should().BeGreaterThan(0);
        var item = evidence[0];
        item.GetProperty("whyItMatters").GetString().Should().Be(FakeInvestigationWorkflow.StandInWhyItMatters);
        item.GetProperty("signalId").GetString().Should().Be(FakeInvestigationWorkflow.StandInSignalId);
        var matrix = investigation.GetProperty("evidenceMatrix");
        matrix.GetArrayLength().Should().Be(4);
        var evidenceId = item.GetProperty("id").GetGuid();
        matrix.EnumerateArray().Should().Contain(entry =>
            entry.GetProperty("category").GetString() == "processDrift"
            && entry.GetProperty("stance").GetString() == "supports"
            && entry.GetProperty("evidenceId").GetGuid() == evidenceId
            && entry.GetProperty("whyItMatters").GetString() == FakeInvestigationWorkflow.StandInWhyItMatters);
        matrix.EnumerateArray().Should().Contain(entry =>
            entry.GetProperty("category").GetString() == "engineeringChange"
            && entry.GetProperty("stance").GetString() == "rulesOut");
    }

    private static object FaultBody(string parameter) => new
    {
        toolId = "E-02",
        chamberId = "A",
        parameter,
        shape = "step",
        magnitude = 4.5,
        rootCauseCategory = "supplyChainMaterial",
        confounders = true,
    };

    private static async Task<JsonElement> OpenIncidentAsync(HttpClient client, string toolId, string parameter)
    {
        var response = await client.PostAsJsonAsync("/api/incidents", new
        {
            toolId,
            parameter = parameter + "-" + Guid.NewGuid().ToString("N"),
            summary = "Opened for " + parameter + ".",
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    private async Task<Guid> SeedCapaAsync(Guid incidentId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var capas = scope.ServiceProvider.GetRequiredService<ICapaLog>();
        var key = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        var now = new DateTimeOffset(2026, 10, 14, 18, 0, 0, TimeSpan.Zero);
        var id = new IncidentId(incidentId);
        const string summary = "Hold the lot.";
        var token = ApprovalToken.Issue(
            ApprovalTokenId.New(),
            "eng-17",
            CapaAction.ComputeActionHash(id, summary),
            now,
            TimeSpan.FromMinutes(15),
            key);
        var capa = CapaAction.Open(CapaId.New(), id, summary, token, now, key);
        await capas.AddAsync(capa, "CAPA-100", CancellationToken.None);
        return capa.Id.Value;
    }
}
