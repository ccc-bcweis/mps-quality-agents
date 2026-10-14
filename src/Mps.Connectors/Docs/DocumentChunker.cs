// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Connectors.Docs;

public sealed record TextChunk(string Id, string Heading, string Text);

/// <summary>
/// Splits a markdown body on headings, then packs each section to about 500 whitespace tokens with overlap.
/// </summary>
public static class DocumentChunker
{
    public const int DefaultTokens = 500;

    public const int DefaultOverlap = 50;

    public static TextChunk[] Chunk(CorpusDocument document, int maxTokens = DefaultTokens, int overlap = DefaultOverlap)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (maxTokens < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTokens), "Chunk size must be positive.");
        }

        if (overlap < 0 || overlap >= maxTokens)
        {
            throw new ArgumentOutOfRangeException(nameof(overlap), "Overlap must be smaller than the chunk size.");
        }

        var chunks = new List<TextChunk>();
        foreach (var section in Sections(document.Body))
        {
            var words = section.Body.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (words.Length <= maxTokens)
            {
                Add(chunks, document.Id, section.Heading, Join(section.Heading, section.Body));
                continue;
            }

            var start = 0;
            while (start < words.Length)
            {
                var count = Math.Min(maxTokens, words.Length - start);
                var slice = string.Join(' ', words.Skip(start).Take(count));
                Add(chunks, document.Id, section.Heading, Join(section.Heading, slice));
                if (start + count >= words.Length)
                {
                    break;
                }

                start += maxTokens - overlap;
            }
        }

        if (chunks.Count == 0)
        {
            Add(chunks, document.Id, document.Title, document.Title);
        }

        return chunks.ToArray();
    }

    private static void Add(List<TextChunk> chunks, string documentId, string heading, string text)
    {
        chunks.Add(new TextChunk($"doc:{documentId}#c{chunks.Count:D3}", heading, text));
    }

    private static string Join(string heading, string body)
    {
        if (string.IsNullOrWhiteSpace(heading))
        {
            return body.Trim();
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return heading.Trim();
        }

        return heading.Trim() + "\n" + body.Trim();
    }

    private static List<Section> Sections(string body)
    {
        var sections = new List<Section>();
        var heading = string.Empty;
        var lines = new List<string>();
        foreach (var line in body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (IsHeading(line) && (lines.Count > 0 || heading.Length > 0))
            {
                sections.Add(new Section(heading, string.Join('\n', lines).Trim()));
                heading = line.Trim();
                lines.Clear();
                continue;
            }

            if (IsHeading(line))
            {
                heading = line.Trim();
                continue;
            }

            lines.Add(line);
        }

        if (heading.Length > 0 || lines.Exists(line => !string.IsNullOrWhiteSpace(line)))
        {
            sections.Add(new Section(heading, string.Join('\n', lines).Trim()));
        }

        return sections;
    }

    private static bool IsHeading(string line)
    {
        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith('#'))
        {
            return false;
        }

        var marks = 0;
        while (marks < trimmed.Length && trimmed[marks] == '#')
        {
            marks++;
        }

        return marks is >= 1 and <= 6 && marks < trimmed.Length && trimmed[marks] == ' ';
    }

    private sealed record Section(string Heading, string Body);
}
