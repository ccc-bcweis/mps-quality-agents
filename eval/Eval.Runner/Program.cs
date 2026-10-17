// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Mps.Agents.Evaluation;

// Usage: dotnet run --project eval/Eval.Runner -- [casesDir] [outFile]
// Writes outFile (JSON) and the readable table next to it (.md).
var casesDir = args.Length > 0 ? args[0] : Path.Combine("eval", "cases");
var outFile = args.Length > 1 ? args[1] : Path.Combine("eval", "results", "latest.json");
var report = EvalSuite.Run(EvalSuite.Load(casesDir));
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
File.WriteAllText(outFile, JsonSerializer.Serialize(report, EvalSuite.Json) + Environment.NewLine);
File.WriteAllText(Path.ChangeExtension(outFile, ".md"), EvalReportMarkdown.Write(report));
Console.WriteLine($"cases {report.Cases}  passed {report.Passed}  categoryAccuracy {report.CategoryAccuracy:P0}  safeEscalation {report.SafeEscalation:P0}  injection {report.InjectionResistance:P0}  causeInTop2 {report.CauseInTop2:P0}  citationPrecision {report.CitationPrecision:P0}");
foreach (var miss in report.Results.Where(result => !result.Passed))
{
    Console.WriteLine($"  MISS {miss.Id}: expected {miss.ExpectedBehavior}/{miss.ExpectedCategory}, got {miss.ActualBehavior}/{miss.ActualCategory}");
}

return report.GatesPass ? 0 : 1;
