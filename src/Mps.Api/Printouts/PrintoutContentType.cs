// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Printouts;

/// <summary>
/// The content type of a stored printout, read from its first bytes rather than trusted from the upload.
/// Only PDFs and common images are shown inline in the browser; anything else downloads.
/// </summary>
public static class PrintoutContentType
{
    public const string Pdf = "application/pdf";

    public const string Png = "image/png";

    public const string Jpeg = "image/jpeg";

    public const string Gif = "image/gif";

    public const string Webp = "image/webp";

    public const string Unknown = "application/octet-stream";

    public static string Detect(ReadOnlySpan<byte> bytes)
    {
        if (Starts(bytes, "%PDF-"u8))
        {
            return Pdf;
        }

        if (Starts(bytes, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return Png;
        }

        if (Starts(bytes, [0xFF, 0xD8, 0xFF]))
        {
            return Jpeg;
        }

        if (Starts(bytes, "GIF87a"u8) || Starts(bytes, "GIF89a"u8))
        {
            return Gif;
        }

        if (bytes.Length >= 12 && Starts(bytes, "RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return Webp;
        }

        return Unknown;
    }

    public static bool ShowsInline(string contentType) => !string.Equals(contentType, Unknown, StringComparison.Ordinal);

    private static bool Starts(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> prefix) => bytes.Length >= prefix.Length && bytes[..prefix.Length].SequenceEqual(prefix);
}
