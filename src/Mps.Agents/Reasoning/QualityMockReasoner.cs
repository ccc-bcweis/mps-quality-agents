// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Extensions.AI;

using Mps.Agents.Ranking;
using Mps.Agents.Resolution;
using Mps.Agents.Tools;
using Mps.ModelGateway.Adapters;

namespace Mps.Agents.Reasoning;

/// <summary>
/// Deterministic investigation tool plan and resolution JSON. The resolution object includes whyItMatters for every cited evidence id, signalId on sensor signals, and an evidence matrix for all four root-cause classes. The gateway stays free of root-cause rules.
/// </summary>
public sealed class QualityMockReasoner : IMockReasoner
{
    private const string UnrecognizedPrintout = "{\"error\":\"unrecognized printout\"}";

    private static readonly Regex Sha256 = new("sha256:\\s*([0-9a-fA-F]{64})", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly JsonSerializerOptions Camel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly Lazy<Dictionary<string, string>> SamplePrintouts = new(LoadSamplePrintouts);

    public ChatResponse Complete(IReadOnlyList<ChatMessage> messages, ChatOptions? options)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (IsPrintout(messages))
        {
            return Text(PrintoutAnswer(messages));
        }

        if (IsResolution(messages))
        {
            return Text(Resolve(messages));
        }

        var completed = messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Count();
        if (completed >= AgentToolNames.DefaultPlan.Count)
        {
            return Text("Evidence gathered.");
        }

        var name = AgentToolNames.DefaultPlan[completed];
        var call = new FunctionCallContent("call-" + completed.ToString(System.Globalization.CultureInfo.InvariantCulture), name, new Dictionary<string, object?>());
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, [call]))
        {
            ModelId = "mock-reasoner",
            Usage = Usage(),
        };
    }

    private static string Resolve(IReadOnlyList<ChatMessage> messages)
    {
        var json = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text;
        if (json is null || !ResolutionJson.TryReadRequest(json, out var request) || request is null)
        {
            return "{\"outcome\":\"escalate\",\"reason\":\"resolution request is missing\"}";
        }

        return ResolutionJson.SerializeDecision(RootCauseRanker.Rank(request));
    }

    private static bool IsResolution(IReadOnlyList<ChatMessage> messages)
    {
        return messages.Any(message => message.Role == ChatRole.System && (message.Text?.Contains("resolution agent", StringComparison.Ordinal) ?? false));
    }

    private static bool IsPrintout(IReadOnlyList<ChatMessage> messages)
    {
        return messages.Any(message => message.Role == ChatRole.System && (message.Text?.Contains("printout reader", StringComparison.Ordinal) ?? false));
    }

    private static string PrintoutAnswer(IReadOnlyList<ChatMessage> messages)
    {
        var text = string.Join('\n', messages.Select(message => message.Text));
        var match = Sha256.Match(text);
        if (!match.Success)
        {
            return UnrecognizedPrintout;
        }

        return SamplePrintouts.Value.TryGetValue(match.Groups[1].Value.ToLowerInvariant(), out var json)
            ? json
            : UnrecognizedPrintout;
    }

    private static Dictionary<string, string> LoadSamplePrintouts()
    {
        var path = FindManifest();
        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        if (path is null)
        {
            return table;
        }

        var document = JsonSerializer.Deserialize<SampleManifest>(File.ReadAllText(path));
        if (document?.Files is null)
        {
            return table;
        }

        foreach (var file in document.Files)
        {
            if (string.IsNullOrWhiteSpace(file.Sha256) || file.Reading is null)
            {
                continue;
            }

            table[file.Sha256.Trim().ToLowerInvariant()] = JsonSerializer.Serialize(file.Reading, Camel);
        }

        return table;
    }

    private static string? FindManifest()
    {
        var seeds = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var seed in seeds)
        {
            var dir = new DirectoryInfo(seed);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "data", "samples", "manifest.json");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                dir = dir.Parent;
            }
        }

        return null;
    }

    private sealed class SampleManifest
    {
        public List<SampleFile>? Files { get; set; }
    }

    private sealed class SampleFile
    {
        public string? Sha256 { get; set; }

        public SampleReading? Reading { get; set; }
    }

    private sealed class SampleReading
    {
        public string? ToolId { get; set; }

        public string? ChamberId { get; set; }

        public DateTimeOffset ObservedAt { get; set; }

        public string? Instrument { get; set; }

        public string? Parameter { get; set; }

        public double Value { get; set; }

        public string? Unit { get; set; }

        public double SpecMinimum { get; set; }

        public double SpecMaximum { get; set; }

        public string? Shape { get; set; }

        public string? OperatorInitials { get; set; }
    }

    private static ChatResponse Text(string text)
    {
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
        {
            ModelId = "mock-reasoner",
            Usage = Usage(),
        };
    }

    private static UsageDetails Usage()
    {
        return new UsageDetails { InputTokenCount = 32, OutputTokenCount = 32, TotalTokenCount = 64 };
    }
}
