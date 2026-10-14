// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Contract.Tests;

internal static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    public static string Contracts => Path.Combine(Root, "contracts");

    public static string Events => Path.Combine(Contracts, "events");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var solution = Path.Combine(dir.FullName, "MPS.sln");
            var openApi = Path.Combine(dir.FullName, "contracts", "openapi.yaml");
            if (File.Exists(solution) && File.Exists(openApi))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not find the repository root from " + AppContext.BaseDirectory);
    }
}
