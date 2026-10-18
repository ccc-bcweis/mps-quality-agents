// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using Mps.Application.Evidence;
using Mps.Application.Persistence;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;
using Mps.Domain.Investigations;

using Xunit;

namespace Mps.Api.Tests;

/// <summary>
/// Live SC-8 (Oct 18) escalated with only a reason; what the AI had proposed was lost. The refused answer is now
/// stored with the round and returned on an escalated investigation.
/// </summary>
public sealed class RejectedAnswerApiTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task AnEscalatedInvestigationReturnsTheRefusedAnswer()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var opened = await (await client.PostAsJsonAsync("/api/incidents", new { toolId = "E-03", parameter = "rfBiasPower-" + Guid.NewGuid().ToString("N"), summary = "SC-8" }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var workOrder = new EvidenceItem(EvidenceId.New(), EvidenceSource.Cmms, "Recipe edit", "An operator edited the RF power recipe setpoint on E-03.", DateTimeOffset.UtcNow, "E-03", "WO-RCP-E03-RF");
        var snapshot = new InvestigationSnapshot(
            InvestigationId.New(),
            new IncidentId(opened.GetProperty("id").GetGuid()),
            InvestigationStage.Escalated,
            [workOrder],
            [],
            "The AI named SN-SIH4-088 as the cause, but that record says in its own words it is not the cause. A person should review it.",
            opened.GetProperty("correlationId").GetString()!);
        var rejected = new RejectedAnswer("WO-RCP-E03-RF: an operator edited the recipe setpoint with no engineering change order.", RootCauseCategory.ProcessDrift, [workOrder.Id.Value, Guid.NewGuid()]);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IInvestigationNarrativeLog>()
                .Stage(snapshot.Id, InvestigationNarrative.Create([], [], new HashSet<Guid> { workOrder.Id.Value }, rejected));
            await scope.ServiceProvider.GetRequiredService<IInvestigationLog>().AddAsync(snapshot, CancellationToken.None);
        }

        var body = await client.GetFromJsonAsync<JsonElement>("/api/investigations/" + snapshot.Id.Value.ToString("D"), Json);

        var answer = body.GetProperty("rejectedAnswer");
        answer.GetProperty("cause").GetString().Should().Contain("recipe setpoint");
        answer.GetProperty("category").GetString().Should().Be("processDrift");
        answer.GetProperty("evidenceIds").EnumerateArray().Select(id => id.GetGuid()).Should().Equal([workOrder.Id.Value], "an id that is not on the investigation is dropped");
    }
}
