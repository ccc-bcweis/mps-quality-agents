// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.RegularExpressions;

namespace Mps.Simulator.Tests;

/// <summary>
/// Guard: limits, thresholds, counts, timings, rule sentences, labels and plants live in data (data/fleet,
/// data/metadata) loaded at startup. Server, simulator, agent and rule code may not carry a literal limit table, a
/// numeric spec range, a threshold or count constant, a literal time span, a sigma multiplier, a rule sentence with
/// its numbers, a parameter label map or a plant name or tool prefix. A legitimate engineering constant is listed,
/// with its reason, in tools/guards/hardcoded-allowlist.txt (shared with web/src/test/noHardcodedLimits.test.ts).
/// </summary>
public sealed partial class NoHardcodedLimitsTests
{
    [Fact]
    public void ServerSimulatorAgentAndRuleCodeHasNoLiteralLimits()
    {
        var root = RepoPaths.Root();
        var allow = GuardAllowlist.Load(root);
        var patterns = Patterns(root);
        var hits = new List<string>();
        foreach (var (file, index, code) in CodeLines(Path.Combine(root, "src")))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            foreach (var (name, pattern) in patterns)
            {
                if (pattern.IsMatch(code) && !allow.Allows(relative, code))
                {
                    hits.Add($"{relative}:{index + 1}: [{name}] {code}");
                }
            }
        }

        string.Join(Environment.NewLine, hits).Should().BeEmpty("limits, counts, timings, labels and plants come from data/fleet and data/metadata, not code (or are listed in tools/guards/hardcoded-allowlist.txt)");
    }

    [Fact]
    public void EveryAllowlistEntryStillMatchesALine()
    {
        var root = RepoPaths.Root();
        var stale = GuardAllowlist.Load(root).Entries
            .Where(entry => !File.Exists(Path.Combine(root, entry.Path))
                || !File.ReadAllLines(Path.Combine(root, entry.Path)).Any(line => line.Contains(entry.Text, StringComparison.Ordinal)))
            .Select(entry => $"{entry.Path} | {entry.Text}")
            .ToArray();
        string.Join(Environment.NewLine, stale).Should().BeEmpty("an allowlist entry whose line is gone must be removed");
    }

    [Theory]
    [InlineData("public const int InControlReadings = 8;")]
    [InlineData("private const double StepShare = 0.7d;")]
    [InlineData("private static readonly TimeSpan WatchFor = TimeSpan.FromMinutes(30);")]
    [InlineData("var limit = center + (3d * sigma);")]
    [InlineData("if (Math.Abs(z) > 3)")]
    [InlineData("return \"Western Electric rule 2 (2 of 3 readings more than 2 sigma out on one side)\";")]
    [InlineData("why = \"8 readings in a row inside the band\",")]
    [InlineData("[\"cf4Flow\"] = \"CF4 flow\",")]
    [InlineData("if (toolId.StartsWith(\"I-\", StringComparison.Ordinal))")]
    [InlineData("Ironwood => \"Ironwood Fab\",")]
    public void ThePatternsCatchTheKindsOfLiteralThatUsedToSlipThrough(string code)
    {
        Patterns(RepoPaths.Root()).Any(pattern => pattern.Pattern.IsMatch(code)).Should().BeTrue(code);
    }

    [Theory]
    [InlineData("var limit = center + (settings.ControlLimitSigma * sigma);")]
    [InlineData("if (sigma <= 0d)")]
    [InlineData("public const string Window = \"window\";")]
    [InlineData("var delay = TimeSpan.FromSeconds(wait.DelaySeconds);")]
    public void ThePatternsLeaveDataDrivenCodeAlone(string code)
    {
        Patterns(RepoPaths.Root()).Where(pattern => pattern.Pattern.IsMatch(code)).Select(pattern => pattern.Name).Should().BeEmpty(code);
    }

    private static IEnumerable<(string File, int Index, string Code)> CodeLines(string src)
    {
        var separator = Path.DirectorySeparatorChar;
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{separator}obj{separator}", StringComparison.Ordinal) || file.Contains($"{separator}bin{separator}", StringComparison.Ordinal))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                var code = lines[index].Trim();
                if (!code.StartsWith("//", StringComparison.Ordinal))
                {
                    yield return (file, index, code);
                }
            }
        }
    }

    private static List<(string Name, Regex Pattern)> Patterns(string root)
    {
        var patterns = new List<(string Name, Regex Pattern)>
        {
            ("limit table", LimitTable()),
            ("unit range", UnitRange()),
            ("window literal", WindowLiteral()),
            ("sigma literal", SigmaLiteral()),
            ("threshold constant", ThresholdConstant()),
            ("time span literal", TimeSpanLiteral()),
            ("rule sentence with numbers", RuleSentence()),
        };
        var parameters = Regex.Matches(File.ReadAllText(Path.Combine(root, "data", "metadata", "parameters.yaml")), @"id:\s*(\w+)")
            .Select(match => Regex.Escape(match.Groups[1].Value)).ToArray();
        patterns.Add(("parameter label map", new Regex($@"\[""(?:{string.Join('|', parameters)})""\]\s*=\s*""", RegexOptions.CultureInvariant)));
        var sites = File.ReadAllText(Path.Combine(root, "data", "fleet", "sites.yaml"));
        var plantNames = Regex.Matches(sites, @"^  - id: \w+\s*\n\s+name: (.+)$", RegexOptions.Multiline).Select(match => Regex.Escape(match.Groups[1].Value.Trim())).ToArray();
        var prefixes = Regex.Matches(sites, @"toolPrefix: ""([^""]+)""").Select(match => Regex.Escape(match.Groups[1].Value)).ToArray();
        patterns.Add(("plant name or prefix", new Regex($@"""(?:{string.Join('|', plantNames.Concat(prefixes))})""", RegexOptions.CultureInvariant)));
        return patterns;
    }

    [GeneratedRegex(@"\bLimit\(""\w+"",\s*""[^""]*"",\s*-?\d")]
    private static partial Regex LimitTable();

    [GeneratedRegex(@"""[^""]*\d(?:\.\d+)?\s?[–-]\s?\d+(?:\.\d+)?\s?(?:°C|Torr|mTorr|sccm|kW|mm/s)\b[^""]*""")]
    private static partial Regex UnitRange();

    /// <summary>A literal times a sigma, a z or sigma compared with a non-zero literal, or a literal sigma argument.</summary>
    [GeneratedRegex(@"(?:\b\d+(?:\.\d+)?d?\s*\*\s*\w*[Ss]igma\b|\b(?:z|Z\(\w*\)|zScore|\w*[Ss]igmas?)\)?\s*[<>]=?\s*(?:[1-9]\d*(?:\.\d+)?|0\.\d*[1-9]\d*)d?\b|SameSideBeyond\([^)]*,\s*\d+(?:\.\d+)?d\))")]
    private static partial Regex SigmaLiteral();

    [GeneratedRegex(@"new (?:OperatingWindow|ParameterLimit|ControlBaseline)\([^)]*\b\d+(?:\.\d+)?d?\s*,\s*\d+(?:\.\d+)?d?")]
    private static partial Regex WindowLiteral();

    /// <summary>A numeric constant whose name says it is a limit, threshold, count or timing.</summary>
    [GeneratedRegex(@"\b(?:const|static\s+readonly)\s+(?:int|double|float|decimal|long)\s+\w*(?:Sigma|Reading|Threshold|Limit|Min|Max|Count|Minutes|Seconds|Hours|Window|Debounce|Share|Ratio|Percent|Pace|Attempts|Retries|Points|Samples|Calls|Depth|Length|Nodes|Children|Files|Hours|Stride|Smoothing|Side)\w*\s*=\s*-?\d")]
    private static partial Regex ThresholdConstant();

    [GeneratedRegex(@"TimeSpan\.From(?:Minutes|Seconds|Hours|Milliseconds|Days)\(\s*-?\d")]
    private static partial Regex TimeSpanLiteral();

    /// <summary>A string literal with a sigma or a count in it: "3-sigma", "2 of 3", "8 readings in a row".</summary>
    [GeneratedRegex(@"""[^""]*(?:\b\d+(?:\.\d+)?[ -]?(?:sigma|σ)\b|\b\d+ of (?:the last )?\d+\b|\b\d+ (?:readings|points|samples) in a row)[^""]*""", RegexOptions.IgnoreCase)]
    private static partial Regex RuleSentence();
}

/// <summary>tools/guards/hardcoded-allowlist.txt: "path | text the line contains | why". Shared by the C# and web guards.</summary>
internal sealed record GuardAllowlist(IReadOnlyList<GuardAllowlist.Entry> Entries)
{
    public static GuardAllowlist Load(string root)
    {
        var entries = File.ReadAllLines(Path.Combine(root, "tools", "guards", "hardcoded-allowlist.txt"))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('|', 3, StringSplitOptions.TrimEntries))
            .Select(parts => parts.Length == 3 && parts[0].Length > 0 && parts[1].Length > 0 && parts[2].Length > 0
                ? new Entry(parts[0], parts[1], parts[2])
                : throw new InvalidOperationException($"Allowlist line needs 'path | text | why': {string.Join('|', parts)}"))
            .ToArray();
        return new GuardAllowlist(entries);
    }

    public bool Allows(string relativePath, string code) =>
        Entries.Any(entry => entry.Path == relativePath && code.Contains(entry.Text, StringComparison.Ordinal));

    internal sealed record Entry(string Path, string Text, string Why);
}
