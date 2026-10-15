// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.RegularExpressions;

using Mps.Simulator.Glossary;

namespace Mps.Simulator.Tests;

public sealed partial class GlossaryTests
{
    private static readonly string[] RequiredTerms =
    [
        "SPC", "CAPA", "PM", "OEE", "MTBF", "MTTR", "EC", "ECN", "UCL", "LCL", "Cpk", "FDC", "NCR", "COPQ", "RF", "MFC",
        "sccm", "mTorr", "Torr", "kW", "L/min", "mm/s", "nm/min", "wafers/hour", "atm", "C", "W", "nm", "mm", "s", "percent", "count", "wafers",
        "SiH4", "Ar", "CF4", "CHF3", "O2", "N2", "H2", "NF3", "NH3", "N2O", "N2H2", "SC1", "SC2", "DIW", "PECVD", "PVD", "CoA",
    ];

    [Fact]
    public void GlossaryLoadsEveryRequiredTerm()
    {
        var catalog = GlossaryCatalog.Load(Path.Combine(RepoPaths.Root(), "data", "glossary.yaml"));

        catalog.Terms.Should().NotBeEmpty();
        catalog.Terms.Should().OnlyHaveUniqueItems(entry => entry.Term);
        var missing = MissingTerms(catalog);
        missing.Should().BeEmpty("every acronym and unit in SPEC, data, and docs is defined. Missing: " + string.Join(", ", missing));
    }

    private static List<string> MissingTerms(GlossaryCatalog catalog)
    {
        var root = RepoPaths.Root();
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in SourceFiles(root))
        {
            var text = StripFences(File.ReadAllText(file));
            foreach (Match match in Acronym().Matches(text))
            {
                var token = match.Value;
                if (KeepAcronym(token))
                {
                    found.Add(token);
                }
            }

            foreach (Match match in UnitToken().Matches(text))
            {
                found.Add(match.Value);
            }

            foreach (Match match in UnitField().Matches(text))
            {
                found.Add(match.Groups[1].Value);
            }
        }

        found.UnionWith(RequiredTerms);
        return found.Where(term => !catalog.Contains(term)).Order(StringComparer.Ordinal).ToList();
    }

    private static IEnumerable<string> SourceFiles(string root)
    {
        foreach (var directory in new[] { Path.Combine(root, "docs"), Path.Combine(root, "data") })
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}print{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}fonts{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.EndsWith($"{Path.DirectorySeparatorChar}glossary.yaml", StringComparison.Ordinal))
                {
                    continue;
                }

                var extension = Path.GetExtension(file);
                if (extension is ".md" or ".yaml" or ".yml" or ".csv")
                {
                    yield return file;
                }
            }
        }
    }

    private static string StripFences(string text)
    {
        var stripped = Fence().Replace(text, " ");
        var lines = stripped.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].Contains("trademark", StringComparison.OrdinalIgnoreCase)
                || lines[index].Contains("do-not-use", StringComparison.OrdinalIgnoreCase))
            {
                lines[index] = string.Empty;
            }
        }

        return string.Join('\n', lines);
    }

    private static bool KeepAcronym(string token)
    {
        if (StopWords.Contains(token))
        {
            return false;
        }

        if (token.Length == 6 && Hex().IsMatch(token) && token.Any(char.IsDigit))
        {
            return false;
        }

        if (IssueId().IsMatch(token) && !DigitTerms.Contains(token))
        {
            return false;
        }

        return true;
    }

    private static readonly HashSet<string> DigitTerms = new(StringComparer.Ordinal)
    {
        "O2", "N2", "H2", "CF4", "NF3", "SC1", "SC2", "N2O", "NH3", "CHF3", "N2H2", "SIH4", "BM25", "B2B", "E2E", "SHA256",
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "AGENT", "ERROR", "MANUAL", "MX", "RISK", "ALARM", "APPROVE", "REASONER", "WATCH", "AMBIENT", "APPLY", "APPROVAL", "ARCH", "ASH", "BATH", "BIAS", "BRAND", "BUDGET", "BUILD", "CALLS", "CENTER",
        "CHAMBER", "CHUCK", "CON", "CONTRA", "DET", "DETERMINISTIC", "DRAFT", "DRIFT", "ENDPOINT", "ETCH", "GAS", "GET",
        "GROUND", "GUESTS", "HEATER", "HIGH", "HUMAN", "IMPACT", "INTAKE", "LICENSE", "LIVE", "MAIN", "MAN", "MANOMETER",
        "MOCK", "MODE", "NITRIDE", "NOT", "OK", "OUTPUT", "OXIDE", "PARTICLES", "PENDING", "PLAN", "PLATEN", "POLY", "POST",
        "PROBABILISTIC", "PUMP", "PUSH", "RANK", "RATE", "READ", "README", "RED", "REMOTE", "REMOVED", "REPLAY", "REPRODUCIBILITY", "RESIDUE", "REST", "SCREENS",
        "REV", "RING", "RINSE", "ROUTE", "RUN", "SIL", "SIM", "SMOKE", "SPEC", "STD", "SUP", "TEMP", "TEST", "THICKNESS",
        "THROUGHPUT", "TOOL", "TRACE", "UNIFORMITY", "WALL", "WHAT", "YIELD",
        "AA", "AAA", "ACL", "ACR", "AM", "CD", "DB", "DR", "EF", "HA", "IBM", "KV", "NNN", "OFL", "RG", "US", "VS", "WCAG", "USERPROFILE",
    };

    [GeneratedRegex(@"(?s)```.*?```")]
    private static partial Regex Fence();

    [GeneratedRegex(@"\b[A-Z][A-Z0-9]{1,}\b")]
    private static partial Regex Acronym();

    [GeneratedRegex(@"mTorr|sccm|wafers/hour|L/min|mm/s|nm/min|kW|°C|\bTorr\b|\batm\b|\bCpk\b")]
    private static partial Regex UnitToken();

    [GeneratedRegex(@"unit:\s*([^\s,}]+)")]
    private static partial Regex UnitField();

    [GeneratedRegex(@"^[A-F0-9]{6}$")]
    private static partial Regex Hex();

    [GeneratedRegex(@"^(?:DS|UX|SEC|GB)?[A-Z]{0,3}\d+[A-Z0-9]*$")]
    private static partial Regex IssueId();
}
