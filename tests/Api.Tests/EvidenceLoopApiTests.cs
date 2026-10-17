// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Mps.Api.Tests;

/// <summary>Add evidence: each request for more evidence closes a round and the agents run the next one.</summary>
public sealed class EvidenceLoopApiTests
{
    private const string Note = "The pump bearing on this tool is past its service interval.";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task AddingEvidenceClosesTheRoundAndTheAgentsRunTheNextOne()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var (incidentId, firstId) = await StartAsync(client);

        var added = await client.PostAsJsonAsync("/api/investigations/" + firstId.ToString("D") + "/decisions", new
        {
            kind = "requestInfo",
            comment = Note,
            approverId = "Bryan Weis",
        });
        var answer = await added.Content.ReadFromJsonAsync<JsonElement>(Json);
        var second = await EvidenceRounds.WaitForRoundAsync(client, incidentId, 2);
        var rounds = await client.GetFromJsonAsync<JsonElement>("/api/incidents/" + incidentId.ToString("D") + "/investigations", Json);
        var first = rounds[0];
        var userItems = second.GetProperty("evidence").EnumerateArray()
            .Where(item => item.GetProperty("source").GetString() == "userProvided")
            .ToArray();
        var latest = await client.GetFromJsonAsync<JsonElement>("/api/incidents/" + incidentId.ToString("D") + "/investigation", Json);

        added.StatusCode.Should().Be(HttpStatusCode.OK);
        answer.GetProperty("investigation").GetProperty("round").GetInt32().Should().Be(2);
        answer.GetProperty("investigation").GetProperty("maxRounds").GetInt32().Should().Be(5);
        rounds.GetArrayLength().Should().Be(2);
        first.GetProperty("id").GetGuid().Should().Be(firstId);
        first.GetProperty("round").GetInt32().Should().Be(1);
        first.GetProperty("stage").GetString().Should().Be("superseded");
        first.GetProperty("decision").GetProperty("kind").GetString().Should().Be("requestInfo");
        first.GetProperty("decision").GetProperty("note").GetString().Should().Be(Note);
        second.GetProperty("id").GetGuid().Should().NotBe(firstId);
        userItems.Should().ContainSingle();
        userItems[0].GetProperty("suppliedBy").GetString().Should().Be("Bryan Weis");
        userItems[0].GetProperty("excerpt").GetString().Should().Contain("pump bearing");
        latest.GetProperty("id").GetGuid().Should().Be(second.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task AClosedRoundCannotBeDecidedAgain()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var (incidentId, firstId) = await StartAsync(client);
        await client.PostAsJsonAsync("/api/investigations/" + firstId.ToString("D") + "/decisions", new { kind = "requestInfo", comment = Note, approverId = "Bryan Weis" });
        await EvidenceRounds.WaitForRoundAsync(client, incidentId, 2);

        var again = await client.PostAsJsonAsync("/api/investigations/" + firstId.ToString("D") + "/decisions", new { kind = "approve", approverId = "eng-17", comment = "Hold the lot." });

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AddEvidenceNeedsText()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var (incidentId, firstId) = await StartAsync(client);

        var empty = await client.PostAsJsonAsync("/api/investigations/" + firstId.ToString("D") + "/decisions", new { kind = "requestInfo", comment = "  ", approverId = "Bryan Weis" });
        var rounds = await client.GetFromJsonAsync<JsonElement>("/api/incidents/" + incidentId.ToString("D") + "/investigations", Json);

        empty.IsSuccessStatusCode.Should().BeFalse();
        rounds.GetArrayLength().Should().Be(1);
        rounds[0].GetProperty("stage").GetString().Should().NotBe("superseded");
        var hasDecision = rounds[0].TryGetProperty("decision", out var decision) && decision.ValueKind != JsonValueKind.Null;
        hasDecision.Should().BeFalse();
    }

    [Fact]
    public async Task RejectClosesTheTicketWithNoAction()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var (incidentId, firstId) = await StartAsync(client);

        var rejected = await client.PostAsJsonAsync("/api/investigations/" + firstId.ToString("D") + "/decisions", new { kind = "reject", comment = "The signal returned to the window." });
        var body = await rejected.Content.ReadFromJsonAsync<JsonElement>(Json);
        var incident = await client.GetFromJsonAsync<JsonElement>("/api/incidents/" + incidentId.ToString("D"), Json);
        var audit = await client.GetFromJsonAsync<JsonElement>("/api/audit", Json);

        rejected.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("investigation").GetProperty("stage").GetString().Should().Be("rejected");
        var hasCapa = body.TryGetProperty("capa", out var capa) && capa.ValueKind != JsonValueKind.Null;
        hasCapa.Should().BeFalse();
        incident.GetProperty("stage").GetString().Should().Be("closed");
        audit.GetProperty("entries").EnumerateArray()
            .Select(entry => entry.GetProperty("summary").GetString())
            .Should().Contain(summary => summary!.Contains("the ticket closed with no action", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheAuditLogRecordsWhoAddedEvidence()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var (incidentId, firstId) = await StartAsync(client);
        await client.PostAsJsonAsync("/api/investigations/" + firstId.ToString("D") + "/decisions", new { kind = "requestInfo", comment = Note, approverId = "Bryan Weis" });
        await EvidenceRounds.WaitForRoundAsync(client, incidentId, 2);

        var audit = await client.GetFromJsonAsync<JsonElement>("/api/audit", Json);
        var entry = audit.GetProperty("entries").EnumerateArray().Single(row => row.GetProperty("kind").GetString() == "userEvidence");

        entry.GetProperty("actor").GetString().Should().Be("Bryan Weis");
        entry.GetProperty("summary").GetString().Should().Contain("round 1").And.Contain("round 2").And.Contain("pump bearing");
    }

    [Fact]
    public async Task RoundsForAnUnknownIncidentAreNotFound()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/incidents/" + Guid.NewGuid().ToString("D") + "/investigations");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<(Guid IncidentId, Guid InvestigationId)> StartAsync(HttpClient client)
    {
        var opened = await client.PostAsJsonAsync("/api/incidents", new
        {
            toolId = "E-02",
            parameter = "vibration-" + Guid.NewGuid().ToString("N"),
            summary = "Opened for vibration.",
        });
        opened.EnsureSuccessStatusCode();
        var incidentId = (await opened.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var started = await client.PostAsync("/api/incidents/" + incidentId.ToString("D") + "/investigations", null);
        started.EnsureSuccessStatusCode();
        var investigationId = (await started.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        return (incidentId, investigationId);
    }
}
