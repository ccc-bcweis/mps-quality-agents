// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Simulator.Tests;

internal static class RepoPaths
{
    public static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MPS.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("MPS.sln was not found above the test output directory.");
    }

    public static string FleetDirectory() => Path.Combine(Root(), "data", "fleet");

    public static string FaultDirectory() => Path.Combine(Root(), "data", "faults");

    public static string GroundTruthDirectory() => Path.Combine(Root(), "data", "groundtruth");

    public static string SeedFile() => Path.Combine(Root(), "data", "seed", "enterprise.json");

    public static string TemplateDirectory() => Path.Combine(Root(), "data", "docs", "templates");

    public static string CorpusDirectory() => Path.Combine(Root(), "data", "docs", "corpus");
}
