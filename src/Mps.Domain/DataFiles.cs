// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Domain;

/// <summary>Finds a file under the data folder: Mps__DataDirectory first, then the nearest data/ folder above the app.</summary>
public static class DataFiles
{
    public static string? Find(string relative)
    {
        var configured = Environment.GetEnvironmentVariable("Mps__DataDirectory");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(Path.Combine(configured, relative)))
        {
            return Path.Combine(configured, relative);
        }

        foreach (var seed in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = string.IsNullOrWhiteSpace(seed) ? null : new DirectoryInfo(seed); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "data", relative);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
