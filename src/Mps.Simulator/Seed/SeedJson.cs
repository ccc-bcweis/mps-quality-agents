// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Mps.Domain.Json;

namespace Mps.Simulator.Seed;

public static class SeedJson
{
    private static readonly JsonSerializerOptions Options = EnterpriseJsonDefaults.Create(writeIndented: true);

    public static void Write(EnterpriseSeed seed, string path)
    {
        ArgumentNullException.ThrowIfNull(seed);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(seed, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        File.WriteAllText(path, json);
    }

    public static EnterpriseSeed Read(string path)
    {
        return JsonSerializer.Deserialize<EnterpriseSeed>(Mps.Simulator.Telemetry.RecordDates.Rebase(File.ReadAllText(path)), Options)
            ?? throw new InvalidDataException($"Empty enterprise seed {path}.");
    }
}
