// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using Mps.Api.Http;
using Mps.Api.Persistence;
using Mps.Application.Evidence;
using Mps.Application.Persistence;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;
using Mps.Domain.Investigations;

namespace Mps.Api.Tests;

public sealed class RuledOutApiTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly DateTimeOffset At = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ARoundTripKeepsRuledOutAndMarksEvidenceStatus()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var opened = await (await client.PostAsJsonAsync("/api/incidents", new { toolId = "E-03", parameter = "rfBiasPower-" + Guid.NewGuid().ToString("N"), summary = "Ruled out" }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var cited = Item(EvidenceSource.Mes, "Run header");
        var record = Item(EvidenceSource.Cmms, "Inspection");
        var agent = Item(EvidenceSource.Qms, "Notice");
        var spc = Item(EvidenceSource.Spc, "Chart", new EvidenceReading(11, "C", 0, 10, "Etch 03", "Chamber A"));
        var telemetry = Item(EvidenceSource.Telemetry, "Trace");
        var ticket = Item(EvidenceSource.Ticketing, "Ticket");
        var manual = Item(EvidenceSource.ManualMeasurement, "Bench reading", new EvidenceReading(4.2, "Torr", null, null, "Etch 03", "Chamber A"));
        var packOnly = Item(EvidenceSource.Document, "Specification");
        var hypothesis = new Hypothesis(
            HypothesisId.New(),
            RootCauseCategory.ProcessDrift,
            "The process drifted after the run.",
            0.7,
            [cited.Id],
            ["A confounder."],
            ["Watch the next lot."],
            [
                new RuledOutEntry(record.Id, "The inspection changed nothing.", RuledOutSource.Record, RootCauseCategory.EquipmentCalibrationOrAlignment),
                new RuledOutEntry(agent.Id, "The answer dismissed the notice.", RuledOutSource.Agent, null),
            ]);
        var snapshot = new InvestigationSnapshot(
            InvestigationId.New(),
            new IncidentId(opened.GetProperty("id").GetGuid()),
            InvestigationStage.RecommendationReady,
            [cited, record, agent, spc, telemetry, ticket, manual, packOnly],
            [hypothesis],
            null,
            opened.GetProperty("correlationId").GetString()!);
        var narrative = InvestigationNarrative.Create(
            [],
            [],
            snapshot.Evidence.Select(item => item.Id.Value).ToHashSet(),
            null,
            [new RuledOutEntry(packOnly.Id, "The specification already covers it.", RuledOutSource.Record, RootCauseCategory.SupplyChainMaterial)]);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IInvestigationNarrativeLog>().Stage(snapshot.Id, narrative);
            await scope.ServiceProvider.GetRequiredService<IInvestigationLog>().AddAsync(snapshot, CancellationToken.None);
        }

        var body = await client.GetFromJsonAsync<JsonElement>("/api/investigations/" + snapshot.Id.Value.ToString("D"), Json);
        AssertStatus(body, cited.Id.Value, "candidate");
        AssertStatus(body, record.Id.Value, "ruledOut");
        AssertStatus(body, agent.Id.Value, "candidate");
        AssertStatus(body, spc.Id.Value, "context");
        AssertStatus(body, telemetry.Id.Value, "context");
        AssertStatus(body, ticket.Id.Value, "context");
        AssertStatus(body, manual.Id.Value, "context");
        AssertStatus(body, packOnly.Id.Value, "ruledOut");
        var ruledOut = body.GetProperty("hypotheses")[0].GetProperty("ruledOut").EnumerateArray().ToArray();
        ruledOut.Should().HaveCount(2);
        ruledOut.Should().Contain(item =>
            item.GetProperty("evidenceId").GetGuid() == record.Id.Value
            && item.GetProperty("source").GetString() == "record"
            && item.GetProperty("category").GetString() == "equipmentCalibrationOrAlignment"
            && item.GetProperty("reason").GetString() == "The inspection changed nothing.");
        ruledOut.Should().Contain(item =>
            item.GetProperty("evidenceId").GetGuid() == agent.Id.Value
            && item.GetProperty("source").GetString() == "agent"
            && item.GetProperty("category").ValueKind == JsonValueKind.Null);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var log = scope.ServiceProvider.GetRequiredService<IInvestigationLog>();
            var stored = await log.GetAsync(snapshot.Id, CancellationToken.None);
            stored!.Hypotheses[0].RuledOut.Should().HaveCount(2);
            await log.UpdateAsync(stored, CancellationToken.None);
        }

        var again = await client.GetFromJsonAsync<JsonElement>("/api/investigations/" + snapshot.Id.Value.ToString("D"), Json);
        again.GetProperty("hypotheses")[0].GetProperty("ruledOut").GetArrayLength().Should().Be(2);
        AssertStatus(again, packOnly.Id.Value, "ruledOut");
        var recommendation = InvestigationRoundPayload.For(snapshot, narrative);
        recommendation.Kind.Should().Be("recommendation");
        recommendation.EvidenceIds.Should().Equal(cited.Id.Value);
        recommendation.RuledOut.Should().HaveCount(2);
        recommendation.OffendingId.Should().BeNull();
        var payload = JsonSerializer.SerializeToElement(recommendation, Json);
        payload.GetProperty("ruledOut")[0].GetProperty("id").GetGuid().Should().Be(record.Id.Value);
        payload.TryGetProperty("offendingId", out _).Should().BeFalse();
    }

    [Fact]
    public async Task AnEscalationWithoutAHypothesisStillMarksThePackList()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var opened = await (await client.PostAsJsonAsync("/api/incidents", new { toolId = "E-03", parameter = "rfBiasPower-" + Guid.NewGuid().ToString("N"), summary = "Escalated" }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var record = Item(EvidenceSource.Plm, "Document change");
        var proposed = Item(EvidenceSource.Qms, "Notice");
        var snapshot = new InvestigationSnapshot(
            InvestigationId.New(),
            new IncidentId(opened.GetProperty("id").GetGuid()),
            InvestigationStage.Escalated,
            [record, proposed],
            [],
            "The answer cited a record the plant data rules out.",
            opened.GetProperty("correlationId").GetString()!);
        var rejected = new RejectedAnswer(
            "The notice caused it.",
            RootCauseCategory.SupplyChainMaterial,
            [record.Id.Value],
            [new RejectedRuledOut(proposed.Id.Value, "The answer dismissed the notice.")]);
        var hit = new GuardListHit(record.Id.Value, GuardListHit.EvidenceIds);
        var narrative = InvestigationNarrative.Create(
            [],
            [],
            snapshot.Evidence.Select(item => item.Id.Value).ToHashSet(),
            rejected,
            [new RuledOutEntry(record.Id, "Document only. Setpoints were not changed.", RuledOutSource.Record, RootCauseCategory.EngineeringChange)],
            hit);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IInvestigationNarrativeLog>().Stage(snapshot.Id, narrative);
            await scope.ServiceProvider.GetRequiredService<IInvestigationLog>().AddAsync(snapshot, CancellationToken.None);
        }

        var body = await client.GetFromJsonAsync<JsonElement>("/api/investigations/" + snapshot.Id.Value.ToString("D"), Json);
        body.GetProperty("hypotheses").GetArrayLength().Should().Be(0);
        AssertStatus(body, record.Id.Value, "ruledOut");
        AssertStatus(body, proposed.Id.Value, "candidate");
        var answer = body.GetProperty("rejectedAnswer");
        answer.GetProperty("ruledOutIds").EnumerateArray().Should().ContainSingle(item =>
            item.GetProperty("evidenceId").GetGuid() == proposed.Id.Value
            && item.GetProperty("reason").GetString() == "The answer dismissed the notice.");

        var escalated = InvestigationRoundPayload.For(snapshot, narrative);
        escalated.Kind.Should().Be("escalated");
        escalated.EvidenceIds.Should().Equal(record.Id.Value);
        escalated.OffendingId.Should().Be(record.Id.Value);
        escalated.List.Should().Be("evidenceIds");
        escalated.RuledOut.Select(item => (item.Id, item.Source)).Should().Equal([
            (record.Id.Value, "record"),
            (proposed.Id.Value, "agent"),
        ]);
    }

    [Fact]
    public async Task ARowSavedWithoutTheNewPropertiesReadsAsEmptyLists()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var opened = await (await client.PostAsJsonAsync("/api/incidents", new { toolId = "E-02", parameter = "escTemperature-" + Guid.NewGuid().ToString("N"), summary = "Old row" }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var investigationId = Guid.NewGuid();
        var evidenceId = Guid.NewGuid();
        var hypothesisId = Guid.NewGuid();
        var evidenceJson = "[" + OldEvidence(evidenceId) + "]";
        var hypothesesJson = "[" + OldHypothesis(hypothesisId, evidenceId) + "]";
        var rejectedJson = "{\"cause\":\"The ring changed the path.\",\"category\":\"ProcessDrift\",\"evidenceIds\":[\"" + evidenceId.ToString("D") + "\"]}";

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QualityDbContext>();
            db.Investigations.Add(new InvestigationRow
            {
                Id = investigationId,
                IncidentId = opened.GetProperty("id").GetGuid(),
                Sequence = 1,
                Stage = "Escalated",
                EscalationReason = "The old row names a gap.",
                CorrelationId = opened.GetProperty("correlationId").GetString()!,
                EvidenceJson = evidenceJson,
                HypothesesJson = hypothesesJson,
                EvidenceMatrixJson = "[]",
                RejectedAnswerJson = rejectedJson,
            });
            await db.SaveChangesAsync();

            var stored = await scope.ServiceProvider.GetRequiredService<IInvestigationLog>().GetAsync(new InvestigationId(investigationId), CancellationToken.None);
            stored!.Hypotheses.Should().ContainSingle();
            stored.Hypotheses[0].RuledOut.Should().BeEmpty();
            var narrative = await scope.ServiceProvider.GetRequiredService<IInvestigationNarrativeLog>().GetAsync(new InvestigationId(investigationId), CancellationToken.None);
            narrative.Rejected.Should().NotBeNull();
            narrative.Rejected!.RuledOutIds.Should().BeEmpty();
            narrative.RecordRuledOut.Should().BeEmpty();
            narrative.GuardHit.Should().BeNull();
        }

        var body = await client.GetFromJsonAsync<JsonElement>("/api/investigations/" + investigationId.ToString("D"), Json);
        body.GetProperty("hypotheses")[0].GetProperty("ruledOut").GetArrayLength().Should().Be(0);
        body.GetProperty("rejectedAnswer").GetProperty("ruledOutIds").GetArrayLength().Should().Be(0);
        AssertStatus(body, evidenceId, "candidate");
    }

    private static void AssertStatus(JsonElement body, Guid evidenceId, string status)
    {
        var item = body.GetProperty("evidence").EnumerateArray().Single(row => row.GetProperty("id").GetGuid() == evidenceId);
        item.GetProperty("status").GetString().Should().Be(status);
    }

    private static EvidenceItem Item(EvidenceSource source, string title, EvidenceReading? reading = null) =>
        new(EvidenceId.New(), source, title, title + " excerpt.", At, "E-03", "REF-" + title, reading);

    private static string OldEvidence(Guid id) =>
        "{\"id\":\"" + id.ToString("D") + "\",\"source\":\"Plm\",\"title\":\"ECO-1\",\"excerpt\":\"A document change.\",\"observedAt\":\"2026-10-12T08:00:00Z\",\"toolId\":\"E-02\",\"externalRef\":\"ECO-1\"}";

    private static string OldHypothesis(Guid id, Guid evidenceId) =>
        "{\"id\":\"" + id.ToString("D") + "\",\"category\":\"EngineeringChange\",\"cause\":\"The ring changed the path.\",\"confidence\":0.7,\"evidenceIds\":[\"" + evidenceId.ToString("D") + "\"],\"counterEvidence\":[\"A maintenance note.\"],\"actions\":[\"Hold the lot.\"]}";
}
