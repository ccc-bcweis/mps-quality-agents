// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

namespace Mps.ModelGateway.Replay;

/// <summary>
/// Loads <c>data/replay/scripted/{scenarioId}/{promptHash}.json</c>. A missing directory is an empty catalog.
/// </summary>
public static class ScriptedReplayFiles
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static InMemoryScriptedReplayCatalog Load(string? directory)
    {
        var catalog = new InMemoryScriptedReplayCatalog();
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return catalog;
        }

        foreach (var scenarioDir in Directory.GetDirectories(directory).OrderBy(path => path, StringComparer.Ordinal))
        {
            var scenarioId = Path.GetFileName(scenarioDir);
            foreach (var file in Directory.GetFiles(scenarioDir, "*.json").OrderBy(path => path, StringComparer.Ordinal))
            {
                catalog.Store(scenarioId, Path.GetFileNameWithoutExtension(file), Read(file));
            }
        }

        return catalog;
    }

    private static ReplayTurn Read(string file)
    {
        ReplayFileDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ReplayFileDto>(File.ReadAllText(file), Json);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Scripted replay file is not valid JSON: " + file, exception);
        }

        if (dto is null)
        {
            throw new InvalidOperationException("Scripted replay file is empty: " + file);
        }

        var calls = (dto.FunctionCalls ?? []).Select(call => new ReplayFunctionCall(
            call.CallId ?? string.Empty,
            call.Name ?? string.Empty,
            call.Arguments)).ToArray();
        return new ReplayTurn(dto.ModelId ?? "replay", dto.Text, calls, dto.InputTokens, dto.OutputTokens);
    }

    private sealed class ReplayFileDto
    {
        public string? ModelId { get; set; }

        public string? Text { get; set; }

        public int InputTokens { get; set; }

        public int OutputTokens { get; set; }

        public List<CallDto>? FunctionCalls { get; set; }
    }

    private sealed class CallDto
    {
        public string? CallId { get; set; }

        public string? Name { get; set; }

        public Dictionary<string, object?>? Arguments { get; set; }
    }
}
