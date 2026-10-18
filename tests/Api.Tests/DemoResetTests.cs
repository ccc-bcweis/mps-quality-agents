// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Mps.Api.Bus;
using Mps.Api.Live;
using Mps.Api.Persistence;

namespace Mps.Api.Tests;

public sealed class DemoResetTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly DateTimeOffset Origin = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AFreshProcessStartsNominalWithNoFaults()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var alerts = await client.GetFromJsonAsync<JsonElement>("/api/alerts");
        var tickets = await client.GetFromJsonAsync<JsonElement>("/api/tickets");
        var incidents = await client.GetFromJsonAsync<JsonElement>("/api/incidents");
        var capas = await client.GetFromJsonAsync<JsonElement>("/api/capas");
        var north = await client.GetFromJsonAsync<JsonElement>("/api/sites/northwater");
        var ironwood = await client.GetFromJsonAsync<JsonElement>("/api/sites/ironwood");

        alerts!.GetArrayLength().Should().Be(0);
        tickets!.GetArrayLength().Should().Be(0);
        incidents!.GetArrayLength().Should().Be(0);
        capas!.GetArrayLength().Should().Be(0);
        ToolStatus(north, "D-01").Should().Be("nominal");
        ToolStatus(north, "D-03").Should().Be("nominal");
        ToolStatus(ironwood, "I-D-01").Should().Be("nominal");
    }

    [Fact]
    public async Task ResetIsIdempotentAndRestoresTheSeededBaseline()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var firstNumber = await OpenAndApproveAsync(client, "E-02", "escTemperature");

        var first = await client.PostAsync("/api/admin/reset", null);
        var secondNumber = await OpenAndApproveAsync(client, "E-02", "escTemperature");
        (await client.PostAsJsonAsync("/api/demo/speed", new { speed = 10, strideSeconds = 30 })).EnsureSuccessStatusCode();
        var second = await client.PostAsync("/api/admin/reset", null);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>(Json);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>(Json);
        var incidents = await client.GetFromJsonAsync<JsonElement>("/api/incidents");
        var tickets = await client.GetFromJsonAsync<JsonElement>("/api/tickets");
        var tools = await client.GetFromJsonAsync<JsonElement>("/api/tools");
        var capas = await client.GetFromJsonAsync<JsonElement>("/api/capas");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        secondNumber.Should().Be(firstNumber);
        firstBody.GetProperty("seed").GetInt32().Should().Be(4242);
        secondBody.GetProperty("seed").GetInt32().Should().Be(firstBody.GetProperty("seed").GetInt32());
        firstBody.GetProperty("simulatedOrigin").GetDateTimeOffset().Should().Be(Origin);
        secondBody.GetProperty("simulatedOrigin").GetDateTimeOffset().Should().Be(Origin);
        secondBody.GetProperty("ticketCount").GetInt32().Should().Be(0);
        secondBody.GetProperty("investigationCount").GetInt32().Should().Be(0);
        secondBody.GetProperty("approvalCount").GetInt32().Should().Be(0);
        secondBody.GetProperty("capaCount").GetInt32().Should().Be(0);
        secondBody.GetProperty("fleetToolCount").GetInt32().Should().Be(8);
        incidents.GetArrayLength().Should().Be(0);
        tickets.GetArrayLength().Should().Be(0);
        capas.GetArrayLength().Should().Be(0);
        var speed = await client.GetFromJsonAsync<JsonElement>("/api/demo/speed");
        speed.GetProperty("speed").GetInt32().Should().Be(1);
        tools.GetArrayLength().Should().Be(32);
    }

    [Fact]
    public async Task ResetRejectsANonAdminPrincipal()
    {
        await using var local = new ApiFactory();
        await using var azure = new ApiFactory().WithWebHostBuilder(builder => builder.UseSetting("Mps:Auth:Mode", "azure").UseSetting("Mps:Auth:AdminEmails", "owner@example.com"));
        var localClient = local.CreateClient();
        var azureClient = azure.CreateClient();
        localClient.DefaultRequestHeaders.Add("X-MS-CLIENT-PRINCIPAL", Principal("reader"));
        azureClient.DefaultRequestHeaders.Add("X-MS-CLIENT-PRINCIPAL", Principal("admin", "ada"));

        var denied = await localClient.PostAsync("/api/admin/reset", null);
        var missing = await azure.CreateClient().PostAsync("/api/admin/reset", null);
        var allowed = await azureClient.PostAsync("/api/admin/reset", null);
        var session = await azureClient.GetFromJsonAsync<JsonElement>("/api/admin/session");
        var localSession = await local.CreateClient().GetFromJsonAsync<JsonElement>("/api/admin/session");

        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        missing.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
        session.GetProperty("isAdmin").GetBoolean().Should().BeTrue();
        session.GetProperty("mode").GetString().Should().Be("azure");
        session.GetProperty("displayName").GetString().Should().Be("ada");
        localSession.GetProperty("isAdmin").GetBoolean().Should().BeTrue();
        localSession.GetProperty("displayName").GetString().Should().Be("dev-user");
    }

    [Fact]
    public async Task ResetProgressStartsIdle()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var progress = await client.GetFromJsonAsync<JsonElement>("/api/admin/reset/progress");

        progress.GetProperty("running").GetBoolean().Should().BeFalse();
        progress.GetProperty("phase").GetString().Should().Be("idle");
    }

    [Fact]
    public async Task HardResetKeepsMetadataAndRefillsTelemetry()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var companyFile = MetadataFile("company.yaml");
        var rulesFile = MetadataFile("business-rules.yaml");
        var companyHash = Hash(companyFile);
        var rulesHash = Hash(rulesFile);
        var companyBefore = await client.GetStringAsync("/api/metadata/company");
        var rulesBefore = await client.GetStringAsync("/api/metadata/rules");
        await OpenAndApproveAsync(client, "E-02", "escTemperature");

        var reset = await client.PostAsync("/api/admin/reset", null);
        var body = await reset.Content.ReadFromJsonAsync<JsonElement>(Json);
        var incidents = await client.GetFromJsonAsync<JsonElement>("/api/incidents");
        var progress = await client.GetFromJsonAsync<JsonElement>("/api/admin/reset/progress");
        var trace = await client.GetFromJsonAsync<JsonElement>("/api/trace/" + body.GetProperty("resetEventId").GetString());

        reset.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("kind").GetString().Should().Be("hard");
        body.GetProperty("ticketCount").GetInt32().Should().Be(0);
        body.GetProperty("investigationCount").GetInt32().Should().Be(0);
        body.GetProperty("approvalCount").GetInt32().Should().Be(0);
        body.GetProperty("capaCount").GetInt32().Should().Be(0);
        body.GetProperty("fleetToolCount").GetInt32().Should().Be(8);
        body.GetProperty("telemetryCount").GetInt64().Should().BeGreaterThan(10000);
        body.GetProperty("eventCount").GetInt64().Should().BeGreaterThan(0);
        incidents.GetArrayLength().Should().Be(0);
        trace.GetProperty("tree").GetProperty("type").GetString().Should().Be("command.reset.hard");
        progress.GetProperty("running").GetBoolean().Should().BeFalse();
        progress.GetProperty("phase").GetString().Should().Be("done");
        progress.GetProperty("kind").GetString().Should().Be("hard");
        Hash(companyFile).Should().Be(companyHash);
        Hash(rulesFile).Should().Be(rulesHash);
        (await client.GetStringAsync("/api/metadata/company")).Should().Be(companyBefore);
        (await client.GetStringAsync("/api/metadata/rules")).Should().Be(rulesBefore);
    }

    [Fact]
    public async Task SoftResetClosesOpenWorkAndKeepsHistory()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var companyHash = Hash(MetadataFile("company.yaml"));
        var opened = await client.PostAsJsonAsync("/api/incidents", new
        {
            toolId = "E-02",
            parameter = "escTemperature",
            summary = "Soft reset probe",
            severity = "high",
        });
        opened.EnsureSuccessStatusCode();
        var incidentId = (await opened.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var started = await client.PostAsync("/api/incidents/" + incidentId + "/investigations", null);
        started.EnsureSuccessStatusCode();
        var investigationId = (await started.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var ticketNumber = (await client.GetFromJsonAsync<JsonElement>("/api/tickets")).EnumerateArray().Single().GetProperty("number").GetString();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QualityDbContext>();
            db.Approvals.Add(new ApprovalRow
            {
                TokenId = Guid.NewGuid(),
                InvestigationId = investigationId,
                IncidentId = incidentId,
                ApproverId = "eng-17",
                ActionHash = "hash",
                Signature = "sig",
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                Consumed = false,
                Summary = "Hold the lot.",
                Decision = "approve",
                Outcome = "writing",
                Attempts = 0,
            });
            await db.SaveChangesAsync();
        }

        var scenario = await client.PostAsync("/api/injector/scenarios/SC-1", null);
        scenario.EnsureSuccessStatusCode();
        var plant = factory.Services.GetRequiredService<PlantSimulation>();
        plant.Runtime.Simulator.ActiveFaults.Should().NotBeEmpty();
        var store = factory.Services.GetRequiredService<SqliteEventStore>();
        var telemetryBefore = store.CountTelemetry();
        var logBefore = store.CountLog();

        var reset = await client.PostAsJsonAsync("/api/admin/reset", new { kind = "soft" });
        var body = await reset.Content.ReadFromJsonAsync<JsonElement>(Json);
        var incident = await client.GetFromJsonAsync<JsonElement>("/api/incidents/" + incidentId);
        var investigation = await client.GetFromJsonAsync<JsonElement>("/api/investigations/" + investigationId);
        var tickets = await client.GetFromJsonAsync<JsonElement>("/api/tickets");
        var audit = await client.GetFromJsonAsync<JsonElement>("/api/audit");

        reset.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("kind").GetString().Should().Be("soft");
        body.GetProperty("ticketCount").GetInt32().Should().BeGreaterThan(0);
        body.GetProperty("investigationCount").GetInt32().Should().BeGreaterThan(0);
        body.GetProperty("telemetryCount").GetInt64().Should().BeGreaterThan(telemetryBefore);
        body.GetProperty("eventCount").GetInt64().Should().BeGreaterThan(logBefore);
        incident.GetProperty("stage").GetString().Should().Be("closedByDemoReset");
        incident.GetProperty("statusLabel").GetString().Should().Be("Closed by reset");
        investigation.GetProperty("stage").GetString().Should().Be("closedByDemoReset");
        investigation.GetProperty("statusLabel").GetString().Should().Be("Closed by reset");
        tickets.EnumerateArray().Select(row => row.GetProperty("number").GetString()).Should().Contain(ticketNumber);
        tickets.EnumerateArray().Single(row => row.GetProperty("number").GetString() == ticketNumber)
            .GetProperty("status").GetString().Should().Be("closed", "a ticket whose incident was closed is no longer open");
        plant.Runtime.Simulator.ActiveFaults.Should().BeEmpty();
        audit.GetProperty("entries").EnumerateArray().Select(row => row.GetProperty("summary").GetString())
            .Should().Contain(summary => summary != null && summary.Contains("Closed by reset", StringComparison.Ordinal));
        Hash(MetadataFile("company.yaml")).Should().Be(companyHash);

        using var check = factory.Services.CreateScope();
        var approvals = check.ServiceProvider.GetRequiredService<QualityDbContext>();
        approvals.Faults.Count().Should().BeGreaterThan(0);
        var row = approvals.Approvals.Single(item => item.InvestigationId == investigationId);
        row.Outcome.Should().Be("closedByDemoReset");
        row.Decision.Should().Be("Closed by reset");
        row.Consumed.Should().BeTrue();
        row.Comment.Should().Be("Closed by reset");
    }

    [Fact]
    public async Task ScriptedScenarioTelemetryIsStableAndFreeFormFaultsStillAccept()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var first = await client.PostAsync("/api/injector/scenarios/SC-1", null);
        var second = await client.PostAsync("/api/injector/scenarios/sc-1", null);
        var other = await client.PostAsync("/api/injector/scenarios/SC-4", null);
        var missing = await client.PostAsync("/api/injector/scenarios/SC-9", null);
        var freeForm = await client.PostAsJsonAsync("/api/faults", new
        {
            toolId = "E-02",
            parameter = "gasFlow-" + Guid.NewGuid().ToString("N"),
            shape = "step",
            magnitude = 2.4,
            rootCauseCategory = "supplyChainMaterial",
            confounders = true,
        });
        var listed = await client.GetFromJsonAsync<JsonElement>("/api/injector/scenarios");
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>(Json);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>(Json);
        var otherBody = await other.Content.ReadFromJsonAsync<JsonElement>(Json);

        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        second.StatusCode.Should().Be(HttpStatusCode.Accepted);
        other.StatusCode.Should().Be(HttpStatusCode.Accepted);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        freeForm.StatusCode.Should().Be(HttpStatusCode.Accepted);
        firstBody.GetProperty("seed").GetInt32().Should().Be(4242);
        firstBody.GetProperty("scenarioId").GetString().Should().Be("SC-1");
        Values(secondBody).Should().Equal(Values(firstBody));
        Values(otherBody).Should().NotEqual(Values(firstBody));
        listed.GetArrayLength().Should().Be(8);
        listed.EnumerateArray().Select(row => row.GetProperty("id").GetString()).Should().Equal("SC-1", "SC-2", "SC-3", "SC-4", "SC-6", "SC-5", "SC-7", "SC-8");
        listed.EnumerateArray().Should().OnlyContain(row => row.GetProperty("whatsHappening").GetString()!.Length > 0);
        listed.EnumerateArray().Single(row => row.GetProperty("id").GetString() == "SC-5").GetProperty("expectedCause").GetString().Should().Contain("MX4419");
    }

    [Fact]
    public void PrincipalRoleIsAdminOnlyWhenTheClaimSaysSo()
    {
        var admin = Mps.Api.Auth.EasyAuthPrincipal.Read(Principal("admin"), "azure");
        var reader = Mps.Api.Auth.EasyAuthPrincipal.Read(Principal("reader"), "local");
        var broken = Mps.Api.Auth.EasyAuthPrincipal.Read("not-base64", "azure");

        admin.IsAdmin.Should().BeTrue();
        reader.IsAdmin.Should().BeFalse();
        broken.IsAdmin.Should().BeFalse();
    }

    private static string ToolStatus(JsonElement site, string toolId)
    {
        return site.GetProperty("bays").EnumerateArray()
            .SelectMany(bay => bay.GetProperty("tools").EnumerateArray())
            .Single(tool => tool.GetProperty("id").GetString() == toolId)
            .GetProperty("status")
            .GetString()!;
    }

    private static IEnumerable<double> Values(JsonElement body)
    {
        return body.GetProperty("telemetry").EnumerateArray().Select(point => point.GetProperty("value").GetDouble());
    }

    private static async Task<string> OpenAndApproveAsync(HttpClient client, string toolId, string parameter)
    {
        var response = await client.PostAsJsonAsync("/api/incidents", new
        {
            toolId,
            parameter,
            summary = "Reset probe " + parameter,
            severity = "high",
        });
        response.EnsureSuccessStatusCode();
        var incidentId = (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var started = await client.PostAsync("/api/incidents/" + incidentId + "/investigations", null);
        started.EnsureSuccessStatusCode();
        var investigationId = (await started.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var approved = await client.PostAsJsonAsync("/api/investigations/" + investigationId + "/decisions", new
        {
            kind = "approve",
            approverId = "eng-17",
            comment = "Hold the lot.",
        });
        approved.EnsureSuccessStatusCode();
        var ticketList = await client.GetFromJsonAsync<JsonElement>("/api/tickets");
        return ticketList.EnumerateArray().Single().GetProperty("number").GetString()!;
    }

    private static string MetadataFile(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data", "metadata", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("data/metadata/" + name + " was not found.");
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static string Principal(string role, string? name = null)
    {
        var claims = new StringBuilder();
        claims.Append("{\"typ\":\"roles\",\"val\":\"").Append(role).Append("\"}");
        if (name is not null)
        {
            claims.Append(",{\"typ\":\"name\",\"val\":\"").Append(name).Append("\"}");
        }

        var json = "{\"auth_typ\":\"aad\",\"claims\":[" + claims + "],\"role_typ\":\"roles\"}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }
}
