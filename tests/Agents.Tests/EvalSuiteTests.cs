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
        cases.Should().HaveCount(24);
        var report = EvalSuite.Run(cases);

        report.GatesPass.Should().BeTrue(string.Join("; ", report.Results.Where(r => !r.Passed).Select(r => r.Id)));
        var committed = File.ReadAllText(Path.Combine(root, "eval", "results", "latest.json")).Trim();
        committed.Should().Be(JsonSerializer.Serialize(report, EvalSuite.Json).Trim(), "run `dotnet run --project eval/Eval.Runner` and commit eval/results/latest.json");
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
