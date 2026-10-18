// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Reflection;

using Mps.Application.Wording;

namespace Mps.Agents.Prompts;

/// <summary>
/// Loads the versioned investigation and resolution prompts embedded from prompts/.
/// </summary>
public sealed class PromptCatalog
{
    public PromptCatalog(PromptDocument investigation, PromptDocument resolution)
    {
        ArgumentNullException.ThrowIfNull(investigation);
        ArgumentNullException.ThrowIfNull(resolution);
        Investigation = investigation;
        Resolution = resolution;
    }

    public PromptDocument Investigation { get; }

    public PromptDocument Resolution { get; }

    public static PromptCatalog Load()
    {
        return new PromptCatalog(
            Read("Mps.Agents.Prompts.investigation.md"),
            Read("Mps.Agents.Prompts.resolution.md"));
    }

    private static PromptDocument Read(string resourceName)
    {
        var assembly = typeof(PromptCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Missing embedded prompt " + resourceName);
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    public static PromptDocument Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != "---")
        {
            throw new InvalidOperationException("Prompt is missing YAML front matter.");
        }

        string? id = null;
        string? version = null;
        var bodyStart = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                bodyStart = i + 1;
                break;
            }

            var separator = lines[i].IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var key = lines[i][..separator].Trim();
            var value = lines[i][(separator + 1)..].Trim();
            if (key == "id")
            {
                id = value;
            }
            else if (key == "version")
            {
                version = value;
            }
        }

        if (bodyStart < 0 || id is null || version is null)
        {
            throw new InvalidOperationException("Prompt front matter requires id and version.");
        }

        var body = string.Join('\n', lines.Skip(bodyStart));
        if (body.Contains(PlainWording.PromptMarker, StringComparison.Ordinal))
        {
            body = body.Replace(PlainWording.PromptMarker, PlainWording.Shared.PromptClause, StringComparison.Ordinal);
        }

        return new PromptDocument(id, version, body);
    }
}
