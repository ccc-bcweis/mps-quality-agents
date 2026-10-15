// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text;

using Mps.Application.Printouts;

using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Mps.Connectors.Printouts;

/// <summary>
/// Pulls a text layer out of a PDF. A scan or a picture has no text layer, so the model path runs next.
/// </summary>
public sealed class PdfTextExtractor : IPrintoutTextExtractor
{
    public string Extract(ReadOnlyMemory<byte> bytes, string? mediaType, string? fileName)
    {
        if (bytes.IsEmpty || !LooksLikePdf(bytes, mediaType, fileName))
        {
            return string.Empty;
        }

        try
        {
            using var document = PdfDocument.Open(bytes.ToArray());
            var builder = new StringBuilder();
            foreach (var page in document.GetPages())
            {
                foreach (var line in Lines(page))
                {
                    if (builder.Length > 0)
                    {
                        builder.Append('\n');
                    }

                    builder.Append(line);
                }
            }

            return builder.ToString();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException)
        {
            return string.Empty;
        }
    }

    private static bool LooksLikePdf(ReadOnlyMemory<byte> bytes, string? mediaType, string? fileName)
    {
        if (mediaType is not null && mediaType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (fileName is not null && fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return bytes.Length >= 4
            && bytes.Span[0] == (byte)'%'
            && bytes.Span[1] == (byte)'P'
            && bytes.Span[2] == (byte)'D'
            && bytes.Span[3] == (byte)'F';
    }

    private static IEnumerable<string> Lines(Page page)
    {
        var words = page.GetWords().ToArray();
        if (words.Length == 0)
        {
            var plain = page.Text?.Trim();
            if (!string.IsNullOrEmpty(plain))
            {
                yield return plain;
            }

            yield break;
        }

        var rows = words
            .GroupBy(word => Math.Round(word.BoundingBox.Bottom / 3d) * 3d)
            .OrderByDescending(group => group.Key);
        foreach (var row in rows)
        {
            var line = string.Join(" ", row.OrderBy(word => word.BoundingBox.Left).Select(word => word.Text));
            if (!string.IsNullOrWhiteSpace(line))
            {
                yield return line.Trim();
            }
        }
    }
}
