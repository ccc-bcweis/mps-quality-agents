// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Simulator.Demo;
using Mps.Simulator.Fleet;

namespace Mps.Api.Demo;

/// <summary>
/// Finds <c>data/fleet</c> and builds the seeded runtime. The seed is <c>Mps:Demo:Seed</c>, default 4242.
/// </summary>
public static class DemoRuntimeLoader
{
    public static DemoRuntime Load(IConfiguration configuration, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(time);
        var seed = configuration.GetValue("Mps:Demo:Seed", DemoRuntime.DefaultSeed);
        var fleet = FleetCatalogLoader.Load(FindDirectory("fleet"));
        return new DemoRuntime(fleet, seed, time);
    }

    public static string? FindReplayDirectory()
    {
        return FindOptional("replay", "scripted");
    }

    private static string FindDirectory(string name)
    {
        var found = FindOptional(name);
        if (found is null)
        {
            throw new InvalidOperationException("Required data directory was not found: " + name);
        }

        return found;
    }

    private static string? FindOptional(params string[] relative)
    {
        // The container copies data/ to Mps__DataDirectory; /app/data is the writable SQLite folder.
        var configured = Environment.GetEnvironmentVariable("Mps__DataDirectory");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var direct = Path.Combine(new[] { configured }.Concat(relative).ToArray());
            if (Directory.Exists(direct))
            {
                return direct;
            }
        }

        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var candidate = Path.Combine(new[] { dir.FullName, "data" }.Concat(relative).ToArray());
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                dir = dir.Parent;
            }
        }

        return null;
    }
}
