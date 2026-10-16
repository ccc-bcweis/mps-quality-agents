// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Orchestration.Tests;

public sealed class ReadableCountersTests
{
    [Fact]
    public void BareEvidenceIdsBecomeTitlesAndUnknownIdsDrop()
    {
        var counters = Mps.Orchestration.MafInvestigationWorkflow.Readable(
            ["c8db6d92-1e97-56bc-b447-3f434173fa37", "Lot SIH4-088 matches its specification."],
            []);

        counters.Should().Equal("Lot SIH4-088 matches its specification.");
    }
}
