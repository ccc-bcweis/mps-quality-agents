// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.RegularExpressions;

namespace Mps.Simulator.Tests;

/// <summary>
/// Guard: limits, spec windows and envelopes live in data (data/fleet, data/metadata) loaded at startup. Server, simulator,
/// agent and rule code may not carry a literal limit table, a numeric spec range with a process unit, or a made-up window.
/// </summary>
public sealed partial class NoHardcodedLimitsTests
{
    [Fact]
    public void ServerSimulatorAgentAndRuleCodeHasNoLiteralLimits()
    {
        var src = Path.Combine(RepoPaths.Root(), "src");
        var hits = new List<string>();
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                var code = lines[index].Trim();
                if (code.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                if (LimitTable().IsMatch(code) || UnitRange().IsMatch(code) || WindowLiteral().IsMatch(code))
                {
                    hits.Add($"{Path.GetRelativePath(src, file)}:{index + 1}: {code}");
                }
            }
        }

        hits.Should().BeEmpty("limits come from data/fleet and data/metadata, not code");
    }

    [GeneratedRegex(@"\bLimit\(""\w+"",\s*""[^""]*"",\s*-?\d")]
    private static partial Regex LimitTable();

    [GeneratedRegex(@"""[^""]*\d(?:\.\d+)?\s?[–-]\s?\d+(?:\.\d+)?\s?(?:°C|Torr|mTorr|sccm|kW|mm/s)\b[^""]*""")]
    private static partial Regex UnitRange();

    [GeneratedRegex(@"new (?:OperatingWindow|ParameterLimit|ControlBaseline)\([^)]*\b\d+(?:\.\d+)?d?\s*,\s*\d+(?:\.\d+)?d?")]
    private static partial Regex WindowLiteral();
}
