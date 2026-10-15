// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Mps.Bus;
using Mps.Simulator.Scenarios;

namespace Mps.Api.Demo;

/// <summary>
/// Recorded bus messages for one scenario, stored as messages.jsonl under the replay directory.
/// Saved model answers live in the scripted folder and are not written here.
/// </summary>
public sealed class ReplayStore
{
    private readonly string _root;

    public ReplayStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _root = directory;
    }

    public string Root => _root;

    public void Save(string scenarioId, IReadOnlyList<ReplayMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var directory = DirectoryFor(scenarioId);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "messages.jsonl");
        var temporary = path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream))
        {
            foreach (var message in messages)
            {
                writer.WriteLine(JsonSerializer.Serialize(message, EventJson.Options));
            }
        }

        File.Move(temporary, path, overwrite: true);
    }

    public bool TryLoad(string scenarioId, out IReadOnlyList<ReplayMessage> messages)
    {
        var path = Path.Combine(DirectoryFor(scenarioId), "messages.jsonl");
        if (!File.Exists(path))
        {
            messages = [];
            return false;
        }

        var loaded = new List<ReplayMessage>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var message = JsonSerializer.Deserialize<ReplayMessage>(line, EventJson.Options);
            if (message is not null && !string.IsNullOrWhiteSpace(message.Type))
            {
                loaded.Add(message);
            }
        }

        messages = loaded;
        return true;
    }

    private string DirectoryFor(string scenarioId)
    {
        if (!ScriptedScenarioCatalog.TryGet(scenarioId, out var scenario))
        {
            throw new ArgumentException("Unknown scenario.", nameof(scenarioId));
        }

        if (scenario.Id.IndexOfAny(['/', '\\', '.']) >= 0)
        {
            throw new InvalidOperationException("Scenario id is not a single folder name.");
        }

        var root = Path.GetFullPath(_root);
        var directory = Path.GetFullPath(Path.Combine(root, scenario.Id));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!directory.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Replay path escaped the replay directory.");
        }

        return directory;
    }
}
