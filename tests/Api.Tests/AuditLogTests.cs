// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Mps.Api.Tests;

public sealed class AuditLogTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task AFreshProcessHasAnEmptyAuditLog()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var audit = await client.GetFromJsonAsync<JsonElement>("/api/audit");

        audit.GetProperty("entries").GetArrayLength().Should().Be(0);
        audit.GetProperty("traces").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task AFaultAndAnApprovalAreOnTheAuditTrail()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var injected = await client.PostAsJsonAsync("/api/faults", new
        {
            toolId = "E-02",
            parameter = "escTemperature",
            shape = "step",
            magnitude = 2.4,
            rootCauseCategory = "processDrift",
        });
        var accepted = await injected.Content.ReadFromJsonAsync<JsonElement>(Json);
        var faultCorrelation = accepted.GetProperty("correlationId").GetString();
        var opened = await client.PostAsJsonAsync("/api/incidents", new
        {
            toolId = "E-02",
            parameter = "escTemperature",
            summary = "Audit probe",
            severity = "high",
        });
        var incident = await opened.Content.ReadFromJsonAsync<JsonElement>(Json);
        var incidentId = incident.GetProperty("id").GetGuid();
        var incidentCorrelation = incident.GetProperty("correlationId").GetString();
        var started = await client.PostAsync("/api/incidents/" + incidentId + "/investigations", null);
        var investigationId = (await started.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var approved = await client.PostAsJsonAsync("/api/investigations/" + investigationId + "/decisions", new
        {
            kind = "approve",
            approverId = "Bryan Weis",
            comment = "Hold the lot.",
        });
        var decision = await approved.Content.ReadFromJsonAsync<JsonElement>(Json);
        var audit = await client.GetFromJsonAsync<JsonElement>("/api/audit");
        var faultTrace = audit.GetProperty("traces").EnumerateArray().Single(row => row.GetProperty("correlationId").GetString() == faultCorrelation);
        var approvalTrace = audit.GetProperty("traces").EnumerateArray().Single(row => row.GetProperty("correlationId").GetString() == incidentCorrelation);
        var approvalKinds = approvalTrace.GetProperty("events").EnumerateArray().Select(row => row.GetProperty("kind").GetString()).ToArray();

        injected.StatusCode.Should().Be(HttpStatusCode.Accepted);
        opened.StatusCode.Should().Be(HttpStatusCode.Created);
        started.StatusCode.Should().Be(HttpStatusCode.Created);
        approved.StatusCode.Should().Be(HttpStatusCode.OK);
        decision.GetProperty("capa").GetProperty("approvedBy").GetString().Should().Be("Bryan Weis");
        decision.GetProperty("capa").GetProperty("approvedAt").GetString().Should().NotBeNullOrWhiteSpace();
        faultTrace.GetProperty("events").EnumerateArray().Should().Contain(row => row.GetProperty("kind").GetString() == "fault");
        approvalKinds.Should().Contain("incident").And.Contain("ticket").And.Contain("investigation").And.Contain("approval").And.Contain("capa");
        approvalTrace.GetProperty("events").EnumerateArray().Should().Contain(row =>
            row.GetProperty("kind").GetString() == "approval"
            && row.GetProperty("summary").GetString() == "Approved by Bryan Weis. Note: Hold the lot."
            && row.GetProperty("actor").GetString() == "Bryan Weis");
    }

    [Fact]
    public async Task ResetClearsTheAuditLog()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var injected = await client.PostAsJsonAsync("/api/faults", new
        {
            toolId = "E-02",
            parameter = "escTemperature",
            shape = "step",
            magnitude = 1.5,
            rootCauseCategory = "processDrift",
        });
        injected.EnsureSuccessStatusCode();
        var reset = await client.PostAsync("/api/admin/reset", null);
        var audit = await client.GetFromJsonAsync<JsonElement>("/api/audit");

        reset.StatusCode.Should().Be(HttpStatusCode.OK);
        audit.GetProperty("entries").GetArrayLength().Should().Be(0);
        audit.GetProperty("traces").GetArrayLength().Should().Be(0);
    }
}
