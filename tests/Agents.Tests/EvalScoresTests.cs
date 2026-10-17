// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Evaluation;

namespace Mps.Agents.Tests;

/// <summary>The report scores beside category accuracy: confusion matrix, expected cause in the top 2, citation precision.</summary>
public sealed class EvalScoresTests
{
    private static readonly string Cases = Path.Combine(Root(), "eval", "cases");

    [Fact]
    public void ConfusionMatrixPutsEachCaseInItsExpectedRowAndReturnedColumn()
    {
        var report = EvalSuite.Summarize(
        [
            Result("A", "supplyChainMaterial", "supplyChainMaterial"),
            Result("B", "supplyChainMaterial", "engineeringChange"),
            Result("C", null, null),
            Result("D", "processDrift", null),
        ]);

        var labels = report.ConfusionMatrix.Labels;
        labels[^1].Should().Be("none");
        int Cell(string? expected, string? returned) =>
            report.ConfusionMatrix.Counts[labels.ToList().IndexOf(expected ?? "none")][labels.ToList().IndexOf(returned ?? "none")];
        Cell("supplyChainMaterial", "supplyChainMaterial").Should().Be(1);
        Cell("supplyChainMaterial", "engineeringChange").Should().Be(1);
        Cell("processDrift", null).Should().Be(1);
        Cell(null, null).Should().Be(1);
        report.ConfusionMatrix.Counts.Sum(row => row.Sum()).Should().Be(4);
    }

    [Fact]
    public void ACitationOutsideTheEvidencePackLowersPrecisionAndFailsTheGate()
    {
        var report = EvalSuite.Summarize(
        [
            Result("A", "processDrift", "processDrift", cited: 1, inPack: 1),
            Result("B", "processDrift", "processDrift", cited: 1, inPack: 0),
        ]);

        report.CitationPrecision.Should().Be(0.5d);
        report.GatesPass.Should().BeFalse();
    }

    [Fact]
    public void AMissedSpecificCauseLowersTheTop2ScoreAndCasesWithoutTermsAreLeftOut()
    {
        var report = EvalSuite.Summarize(
        [
            Result("A", "processDrift", "processDrift", top2: true),
            Result("B", "processDrift", "processDrift", top2: false),
            Result("C", null, null, top2: null),
        ]);

        report.CauseInTop2.Should().Be(0.5d);
        report.GatesPass.Should().BeFalse();
    }

    [Fact]
    public void TheRivalTheRankerRuledOutCountsAsTheSecondCause()
    {
        var item = EvalSuite.Load(Cases).Single(c => c.Id == "CAL-01");

        item.Expected.CauseTerms = ["O2 MFC"];
        EvalSuite.Score(item).CauseInTop2.Should().BeTrue("the top cause names the instrument");

        item.Expected.CauseTerms = ["N2H2-190"];
        EvalSuite.Score(item).CauseInTop2.Should().BeTrue("the confounder lot is the ruled-out second cause");

        item.Expected.CauseTerms = ["ECO-0000"];
        EvalSuite.Score(item).CauseInTop2.Should().BeFalse();
    }

    [Fact]
    public void ThePackHoldsTheRecordsAndTypedIdsTheRankerWasGivenAndNothingElse()
    {
        var item = EvalSuite.Load(Cases).Single(c => c.Id == "SUP-01");
        var pack = EvalSuite.Pack(item.Request);

        pack.Should().Contain(item.Request.Notes.Select(note => note.Id));
        pack.Should().Contain(item.Request.SignalEvidenceIds);
        pack.Should().NotContain(Guid.NewGuid().ToString("D"));
        EvalSuite.Score(item).CitedInPack.Should().Be(EvalSuite.Score(item).Cited);
    }

    [Fact]
    public void TheMarkdownReportShowsTheGatesAndTheMatrix()
    {
        var report = EvalSuite.Run(EvalSuite.Load(Cases));
        var text = EvalReportMarkdown.Write(report);

        text.Should().Contain("| Expected cause in the top 2 | 100% | 80% |");
        text.Should().Contain("| Citation precision | 100% | 90% |");
        text.Should().Contain("| `supplyChainMaterial` | 0 | 0 | 4 | 0 | 0 |");
        text.Should().Contain("| SC7-01 |");
    }

    private static EvalCaseResult Result(string id, string? expected, string? returned, int cited = 1, int inPack = 1, bool? top2 = true) =>
        new(id, "g", expected is null ? "escalate" : "recommend", expected, returned is null ? "escalate" : "recommend", returned, expected == returned, null, top2, cited, inPack);

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MPS.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found.");
    }
}
