// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;

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
        tools.GetArrayLength().Should().Be(8);
    }

    [Fact]
    public async Task ResetRejectsANonAdminPrincipal()
    {
        await using var local = new ApiFactory();
        await using var azure = new ApiFactory().WithWebHostBuilder(builder => builder.UseSetting("Mps:Auth:Mode", "azure"));
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
        missing.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
        session.GetProperty("isAdmin").GetBoolean().Should().BeTrue();
        session.GetProperty("mode").GetString().Should().Be("azure");
        session.GetProperty("displayName").GetString().Should().Be("ada");
        localSession.GetProperty("isAdmin").GetBoolean().Should().BeTrue();
        localSession.GetProperty("displayName").GetString().Should().Be("dev-user");
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
        listed.GetArrayLength().Should().Be(4);
        listed.EnumerateArray().Select(row => row.GetProperty("id").GetString()).Should().Equal("SC-1", "SC-2", "SC-3", "SC-4");
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
