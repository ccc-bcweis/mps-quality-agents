// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Connectors.Docs;

public sealed class CorpusDocument
{
    public CorpusDocument(string id, string title, string? toolFamily, string revision, string kind, bool untrusted, string body)
    {
        Id = Required(id, "Document id");
        Title = Required(title, "Document title");
        ToolFamily = string.IsNullOrWhiteSpace(toolFamily) ? null : toolFamily.Trim();
        Revision = Required(revision, "Document revision");
        Kind = Required(kind, "Document kind");
        Untrusted = untrusted;
        Body = body ?? string.Empty;
    }

    public string Id { get; }

    public string Title { get; }

    public string? ToolFamily { get; }

    public string Revision { get; }

    public string Kind { get; }

    public bool Untrusted { get; }

    public string Body { get; }

    public static CorpusDocument Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var text = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
        {
            throw new InvalidDataException("A corpus document is missing front matter.");
        }

        var end = text.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new InvalidDataException("A corpus document does not close its front matter.");
        }

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in text[4..end].Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var split = line.IndexOf(':');
            if (split <= 0)
            {
                throw new InvalidDataException($"Front matter line '{line}' needs a key.");
            }

            fields[line[..split].Trim()] = line[(split + 1)..].Trim();
        }

        var untrusted = fields.TryGetValue("untrusted", out var flag) && bool.Parse(flag);
        return new CorpusDocument(
            Field(fields, "id"),
            Field(fields, "title"),
            fields.TryGetValue("toolFamily", out var family) ? family : null,
            Field(fields, "revision"),
            Field(fields, "kind"),
            untrusted,
            text[(end + 5)..].Trim());
    }

    public static CorpusDocument ParseFile(string path)
    {
        return Parse(File.ReadAllText(path));
    }

    private static string Field(Dictionary<string, string> fields, string name)
    {
        return fields.TryGetValue(name, out var value) ? value : string.Empty;
    }

    private static string Required(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"{name} is required.");
        }

        return value.Trim();
    }
}
