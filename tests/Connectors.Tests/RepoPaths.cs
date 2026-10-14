// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Connectors.Tests;

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

    public static string CorpusDirectory() => Path.Combine(Root(), "data", "docs", "corpus");

    public static string IndexFile() => Path.Combine(Root(), "data", "index", "embeddings.json");
}
