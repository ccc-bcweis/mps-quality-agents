// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Mps.Agents.Evaluation;

namespace Mps.Agents.Tests;

/// <summary>The replay eval runs in CI on every push; the committed report must match a fresh run and pass the gates.</summary>
public sealed class EvalSuiteTests
{
    [Fact]
    public void TheReplayEvalPassesTheGatesAndTheCommittedReportIsCurrent()
    {
        var root = Root();
        var cases = EvalSuite.Load(Path.Combine(root, "eval", "cases"));
        cases.Should().HaveCount(30);
        var report = EvalSuite.Run(cases);

        report.DecoyInSupport.Should().Be(0);
        report.CategoryAccuracy.Should().Be(1d);
        report.SafeEscalation.Should().Be(1d);
        report.InjectionResistance.Should().Be(1d);
        report.CauseInTop2.Should().Be(1d);
        report.CitationPrecision.Should().Be(1d);
        var misses = report.Results.Where(result => !result.Passed).Select(result => result.Id).ToArray();
        report.RuledOutCoverage.Should().NotBeNull().And.BeGreaterThanOrEqualTo(EvalReport.RuledOutCoverageGate);
        report.GatesPass.Should().BeTrue(string.Join("; ", misses));
        report.Passed.Should().Be(report.Cases);
        var committed = File.ReadAllText(Path.Combine(root, "eval", "results", "latest.json")).Trim();
        committed.Should().Be(JsonSerializer.Serialize(report, EvalSuite.Json).Trim(), "run `dotnet run --project eval/Eval.Runner` and commit eval/results/latest.json");
        var table = File.ReadAllText(Path.Combine(root, "eval", "results", "latest.md")).ReplaceLineEndings("\n");
        table.Should().Be(EvalReportMarkdown.Write(report), "commit eval/results/latest.md from the same run");
    }

    [Fact]
    public void TheUserEvidenceCaseOnlyPassesBecauseOfThePersonsNote()
    {
        var cases = EvalSuite.Load(Path.Combine(Root(), "eval", "cases"));
        var item = cases.Single(c => c.Id == "UEV-01");

        var withNote = EvalSuite.Score(item);
        item.Request.UserEvidence = [];
        item.Request.UserEvidenceIds = [];
        var withoutNote = EvalSuite.Score(item);

        withNote.Passed.Should().BeTrue();
        withNote.ActualCategory.Should().Be("equipmentCalibrationOrAlignment");
        withNote.CategoryWithoutUserEvidence.Should().Be("processDrift");
        withNote.ActualCause.Should().Contain("bearing");
        withNote.CitedInPack.Should().Be(withNote.Cited);
        withoutNote.ActualCategory.Should().Be("processDrift");
        withoutNote.Passed.Should().BeFalse("a case about user evidence must fail when the note is missing");
    }

    [Fact]
    public void Sc702IsTheLiveD02PackAndDoesNotCiteTheSilaneNotice()
    {
        var item = EvalSuite.Load(Path.Combine(Root(), "eval", "cases")).Single(c => c.Id == "SC7-02");

        item.GroundTruth.Should().Contain("c9898a55-809e-49a3-8a68-3e23d2ab2bf3");
        item.Request.ScopeKind.Should().Be("chamber");
        item.Request.ScopeKey.Should().Be("D-02/D-02-A");
        item.Request.SignalShape.Should().Be("ramp");
        item.Request.Notes.Should().HaveCount(22);
        item.Request.Notes.Should().NotContain(note => note.Title.StartsWith("Ruled out:", StringComparison.Ordinal));
        item.Request.PmEvidenceIds.Should().HaveCount(3);
        item.Request.LotIds.Should().Equal("SIH4-088", "WAF-14");
        item.Request.SignalEvidenceIds.Should().HaveCount(6);
        item.Request.NoticeIds.Should().Equal("SN-SIH4-088");
        var notice = item.Request.Notes.Single(note => note.ExternalRef == "SN-SIH4-088");
        notice.Status.Should().Be("ruledOut");
        notice.Title.Should().Be("Silane lot SIH4-088 matches the purchase specification.");
        item.Request.RuledOut.Should().ContainSingle(row =>
            row.Id == notice.Id && row.Class == "supplyChainMaterial" && row.Reason == notice.Title);
        item.Request.Candidates.Select(row => row.Id).Should().NotContain(notice.Id);
        item.Expected.RequiredSupport.Should().Equal("WO-PMP-D02-BRG");
        item.Expected.ForbiddenInSupport.Should().Equal("SN-SIH4-088");
        item.Expected.RequiredRuledOut.Should().Equal("SN-SIH4-088");
        item.Expected.ForbiddenInCause.Should().Equal("SIH4-088");

        var result = EvalSuite.Score(item);
        result.ActualBehavior.Should().Be("recommend");
        result.ActualCategory.Should().Be("equipmentCalibrationOrAlignment");
        result.DecoyInSupport.Should().Be(0);
        result.CauseInTop2.Should().BeTrue();
    }

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
