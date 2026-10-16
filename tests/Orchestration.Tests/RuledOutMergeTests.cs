// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Orchestration.Tests;

public sealed class RuledOutMergeTests
{
    [Fact]
    public void TheHypothesisListsRuledOutRecordsOnce()
    {
        var merged = Mps.Orchestration.MafInvestigationWorkflow.WithRuledOut(
            ["ECO ECO-61234 is a confounder."],
            ["ECO-61234 is a document-only change. Setpoints were not changed.", "Work order WO-71234 (preventive maintenance) was an inspection with no findings. Nothing was changed."]);

        merged.Should().HaveCount(2);
        merged.Should().Contain(item => item.Contains("WO-71234", StringComparison.Ordinal));
    }
}
