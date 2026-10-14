// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Extensions.AI;

using Mps.Agents.Explain;
using Mps.Agents.Grounding;
using Mps.Agents.Prompts;
using Mps.Agents.Resolution;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.ModelGateway.Replay;

namespace Mps.Agents.Tests;

public sealed class ResolutionReplayFixtureTests
{
    [Fact]
    public async Task TheS1FixtureParsesGroundsWithoutRepairAndReplays()
    {
        var json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "s1-resolution.json"));
        var parsed = ResolutionJson.TryParseClaim(json, out var claim, out var escalate, out var error);

        parsed.Should().BeTrue(error);
        escalate.Should().BeNull();
        claim.Should().NotBeNull();
        claim!.Category.Should().Be(RootCauseCategory.EngineeringChange);
        claim.Cause.Should().Contain("ECO-4471");
        claim.Explanations.Should().HaveCount(2);
        claim.Explanations.Should().Contain(item => item.EvidenceId == EvidenceSamples.Id(3) && item.SignalId == "esc-temp");
        claim.Explanations.Should().Contain(item => item.EvidenceId == EvidenceSamples.Id(1) && item.SignalId == null);
        claim.EvidenceMatrix.Select(entry => entry.Category).Distinct().Should().HaveCount(4);

        var result = GroundingValidator.Validate(claim, Pack(), WhyItMattersCatalog.Empty);

        result.Accepted.Should().BeTrue();
        result.Repaired.Should().BeFalse();
        result.Failures.Should().BeEmpty();
        result.Claim.Should().BeSameAs(claim);

        var prompt = PromptCatalog.Load().Resolution.Body;
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, prompt),
            new(ChatRole.User, json),
        };
        var catalog = new InMemoryReplayCatalog();
        catalog.Store(PromptHasher.Hash(messages, null), new ReplayTurn("replay-model", json, [], 8, 8));
        using var client = new ReplayChatClient(catalog);

        var response = await client.GetResponseAsync(messages);

        response.Text.Should().Contain("whyItMatters");
        response.Text.Should().Contain("evidenceMatrix");
        response.Text.Should().Contain("esc-temp");
    }

    private static IReadOnlyList<EvidenceItem> Pack()
    {
        return
        [
            EvidenceSamples.Item(1, EvidenceSource.Plm, "ECO-4471", "ECO-4471"),
            EvidenceSamples.Item(2, EvidenceSource.Cmms, "Preventive maintenance the same week", "WO-19"),
            EvidenceSamples.Item(
                3,
                EvidenceSource.Telemetry,
                "esc_temp window",
                "telemetry:MPS-E7:esc_temp:2026-10-14T15:00:00.0000000+00:00"),
        ];
    }
}
