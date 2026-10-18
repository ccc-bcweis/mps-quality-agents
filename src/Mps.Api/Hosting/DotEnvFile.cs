// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Hosting;

/// <summary>
/// Lets <c>dotnet run</c> read the untracked <c>.env</c> at the repo root (the same file docker compose reads).
/// A variable already set in the environment wins. Lines are <c>NAME=value</c>; blank values, comments and
/// <c>export</c> prefixes are handled. Values are never logged. Set <c>MPS_SKIP_DOTENV=1</c> to turn it off.
/// </summary>
public static class DotEnvFile
{
    public const string FileName = ".env";

    /// <summary>Finds .env from <paramref name="start"/> upward (stopping at the folder with MPS.sln) and loads it. Returns the path or null.</summary>
    public static string? LoadFromRepo(string start)
    {
        ArgumentNullException.ThrowIfNull(start);
        if (Environment.GetEnvironmentVariable("MPS_SKIP_DOTENV") is "1" or "true")
        {
            return null;
        }

        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, FileName);
            if (File.Exists(candidate))
            {
                Apply(Parse(File.ReadAllLines(candidate)));
                return candidate;
            }

            if (File.Exists(Path.Combine(dir.FullName, "MPS.sln")))
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>Parses NAME=value lines. Blank values are skipped so an empty placeholder never overrides a default.</summary>
    public static IReadOnlyDictionary<string, string> Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.Ordinal))
            {
                line = line["export ".Length..].TrimStart();
            }

            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0)
            {
                continue;
            }

            var name = line[..equals].Trim();
            var value = line[(equals + 1)..].Trim();
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
            {
                value = value[1..^1];
            }

            if (value.Length > 0)
            {
                values[name] = value;
            }
        }

        return values;
    }

    private static void Apply(IReadOnlyDictionary<string, string> values)
    {
        foreach (var (name, value) in values)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}
