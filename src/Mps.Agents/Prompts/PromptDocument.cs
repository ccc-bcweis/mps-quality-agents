// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Agents.Prompts;

/// <summary>
/// A versioned prompt. Traces record the id and version that produced them.
/// </summary>
public sealed class PromptDocument
{
    public PromptDocument(string id, string version, string body)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Prompt id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Prompt version is required.", nameof(version));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("Prompt body is required.", nameof(body));
        }

        Id = id.Trim();
        Version = version.Trim();
        Body = body.Trim();
    }

    public string Id { get; }

    public string Version { get; }

    public string Body { get; }
}
