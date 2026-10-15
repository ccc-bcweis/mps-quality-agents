// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Mps.Printouts;

/// <summary>
/// Writes the synthetic bench printouts in data/samples. One PDF per instrument, plus a PNG scan of the thermocouple sheet.
/// </summary>
public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static int Main()
    {
        var root = FindRepoRoot();
        var directory = Path.Combine(root, "data", "samples");
        Directory.CreateDirectory(directory);

        var sheets = Sheets();
        var manifest = new List<ManifestFile>();
        foreach (var sheet in sheets)
        {
            var pdfName = sheet.FileStem + ".pdf";
            var pdfPath = Path.Combine(directory, pdfName);
            File.WriteAllBytes(pdfPath, Pdf(sheet.Lines()));
            manifest.Add(Entry(pdfName, pdfPath, sheet));

            if (!sheet.Scan)
            {
                continue;
            }

            var pngName = sheet.FileStem + ".png";
            var pngPath = Path.Combine(directory, pngName);
            WriteScan(pngPath, sheet.Lines());
            manifest.Add(Entry(pngName, pngPath, sheet));
        }

        var json = JsonSerializer.Serialize(new Manifest(manifest), JsonOptions);
        File.WriteAllText(Path.Combine(directory, "manifest.json"), json + "\n");
        Console.WriteLine("Wrote " + manifest.Count.ToString(CultureInfo.InvariantCulture) + " printouts in " + directory);
        return 0;
    }

    private static ManifestFile Entry(string fileName, string path, Sheet sheet)
    {
        var bytes = File.ReadAllBytes(path);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new ManifestFile(fileName, hash, sheet.Reading());
    }

    private static Sheet[] Sheets()
    {
        return
        [
            new Sheet(
                "oscilloscope-e01",
                "Northline Bench Instruments",
                "NB-410",
                "oscilloscope",
                "Oscilloscope",
                "E-01",
                "E-01-A",
                "2026-10-12T14:22:00Z",
                "rfPower",
                "RF power",
                "2140",
                "W",
                "1000",
                "2000",
                "oscillation",
                "KL",
                "Peak-to-peak swing leaves the printed window.",
                false),
            new Sheet(
                "rf-power-meter-e02",
                "Calder Ridge Metrology",
                "CR-220",
                "rfPowerMeter",
                "RF power meter",
                "E-02",
                "E-02-A",
                "2026-10-12T15:05:00Z",
                "rfBiasPower",
                "Bias RF power",
                "510",
                "W",
                "200",
                "500",
                "step",
                "MR",
                "Forward power sits above the printed maximum.",
                false),
            new Sheet(
                "particle-counter-d01",
                "Hearthline Particle Works",
                "HP-12",
                "particleCounter",
                "Particle counter",
                "D-01",
                "D-01-A",
                "2026-10-11T18:10:00Z",
                "particlesAdders",
                "Particle adders",
                "3",
                "count",
                "0",
                "10",
                "",
                "BN",
                "Adder count is inside the printed window.",
                false),
            new Sheet(
                "thermocouple-logger-e01",
                "Sable Fork Instruments",
                "SF-80",
                "thermocoupleLogger",
                "Thermocouple logger",
                "E-01",
                "E-01-A",
                "2026-10-12T09:40:00Z",
                "escTemperature",
                "ESC temperature",
                "68",
                "C",
                "40",
                "60",
                "ramp",
                "JL",
                "The trace rises past the printed maximum.",
                true),
        ];
    }

    private static byte[] Pdf(IReadOnlyList<string> lines)
    {
        var content = new StringBuilder();
        content.Append("BT\n/F1 11 Tf\n48 760 Td\n14 TL\n");
        for (var index = 0; index < lines.Count; index++)
        {
            if (index > 0)
            {
                content.Append("T*\n");
            }

            content.Append('(');
            content.Append(Escape(lines[index]));
            content.Append(") Tj\n");
        }

        content.Append("ET\n");
        var stream = content.ToString();
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            "<< /Length " + stream.Length.ToString(CultureInfo.InvariantCulture) + " >>\nstream\n" + stream + "endstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };

        var builder = new StringBuilder();
        builder.Append("%PDF-1.4\n");
        var offsets = new int[objects.Length];
        for (var index = 0; index < objects.Length; index++)
        {
            offsets[index] = builder.Length;
            builder.Append((index + 1).ToString(CultureInfo.InvariantCulture));
            builder.Append(" 0 obj\n");
            builder.Append(objects[index]);
            builder.Append("\nendobj\n");
        }

        var xref = builder.Length;
        builder.Append("xref\n0 ");
        builder.Append((objects.Length + 1).ToString(CultureInfo.InvariantCulture));
        builder.Append("\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            builder.Append(offset.ToString("D10", CultureInfo.InvariantCulture));
            builder.Append(" 00000 n \n");
        }

        builder.Append("trailer\n<< /Size ");
        builder.Append((objects.Length + 1).ToString(CultureInfo.InvariantCulture));
        builder.Append(" /Root 1 0 R >>\nstartxref\n");
        builder.Append(xref.ToString(CultureInfo.InvariantCulture));
        builder.Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static string Escape(string line)
    {
        return line.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static void WriteScan(string path, IReadOnlyList<string> lines)
    {
        const int width = 900;
        const int height = 1100;
        var pixels = new byte[width * height * 3];
        for (var index = 0; index < pixels.Length; index += 3)
        {
            pixels[index] = 246;
            pixels[index + 1] = 244;
            pixels[index + 2] = 238;
        }

        Fill(pixels, width, height, 0, 0, width, 72, 30, 41, 59);
        DrawText(pixels, width, height, 36, 24, "SCAN", 255, 255, 255, 2);
        var top = 100;
        foreach (var line in lines)
        {
            DrawText(pixels, width, height, 40, top, line, 23, 23, 23, 2);
            top += 32;
        }

        var random = new Random(4242);
        for (var speck = 0; speck < 90; speck++)
        {
            Set(pixels, width, height, random.Next(16, width - 16), random.Next(80, height - 16), 180, 176, 168);
        }

        File.WriteAllBytes(path, Png(width, height, pixels));
    }

    private static void DrawText(byte[] pixels, int width, int height, int x, int y, string text, byte red, byte green, byte blue, int scale)
    {
        var cursor = x;
        foreach (var character in text)
        {
            var glyph = Glyph(character);
            for (var row = 0; row < 7; row++)
            {
                for (var column = 0; column < 5; column++)
                {
                    if ((glyph[row] & (1 << (4 - column))) == 0)
                    {
                        continue;
                    }

                    for (var dy = 0; dy < scale; dy++)
                    {
                        for (var dx = 0; dx < scale; dx++)
                        {
                            Set(pixels, width, height, cursor + (column * scale) + dx, y + (row * scale) + dy, red, green, blue);
                        }
                    }
                }
            }

            cursor += (6 * scale);
        }
    }

    private static void Fill(byte[] pixels, int width, int height, int x, int y, int w, int h, byte red, byte green, byte blue)
    {
        for (var row = 0; row < h; row++)
        {
            for (var column = 0; column < w; column++)
            {
                Set(pixels, width, height, x + column, y + row, red, green, blue);
            }
        }
    }

    private static void Set(byte[] pixels, int width, int height, int x, int y, byte red, byte green, byte blue)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return;
        }

        var index = ((y * width) + x) * 3;
        pixels[index] = red;
        pixels[index + 1] = green;
        pixels[index + 2] = blue;
    }

    private static byte[] Png(int width, int height, byte[] rgb)
    {
        using var output = new MemoryStream();
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        WriteChunk(output, "IHDR", Ihdr(width, height));
        using (var raw = new MemoryStream())
        {
            for (var row = 0; row < height; row++)
            {
                raw.WriteByte(0);
                raw.Write(rgb, row * width * 3, width * 3);
            }

            using var compressed = new MemoryStream();
            using (var deflate = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                raw.Position = 0;
                raw.CopyTo(deflate);
            }

            WriteChunk(output, "IDAT", compressed.ToArray());
        }

        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static byte[] Ihdr(int width, int height)
    {
        var header = new byte[13];
        WriteInt(header, 0, width);
        WriteInt(header, 4, height);
        header[8] = 8;
        header[9] = 2;
        return header;
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        var length = new byte[4];
        WriteInt(length, 0, data.Length);
        output.Write(length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        var crc = Crc(typeBytes, data);
        var crcBytes = new byte[4];
        WriteInt(crcBytes, 0, crc);
        output.Write(crcBytes);
    }

    private static int Crc(byte[] type, byte[] data)
    {
        var crc = 0xFFFFFFFF;
        foreach (var part in new[] { type, data })
        {
            foreach (var value in part)
            {
                crc ^= value;
                for (var bit = 0; bit < 8; bit++)
                {
                    var mask = (crc & 1) == 1 ? 0xEDB88320 : 0;
                    crc = (crc >> 1) ^ mask;
                }
            }
        }

        return (int)(crc ^ 0xFFFFFFFF);
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static byte[] Glyph(char character)
    {
        if (Glyphs.TryGetValue(character, out var glyph))
        {
            return glyph;
        }

        return Glyphs.TryGetValue(char.ToUpperInvariant(character), out glyph) ? glyph : Glyphs[' '];
    }

    private static byte[] GlyphRow(int r0, int r1, int r2, int r3, int r4, int r5, int r6)
    {
        return [(byte)r0, (byte)r1, (byte)r2, (byte)r3, (byte)r4, (byte)r5, (byte)r6];
    }

    private static readonly Dictionary<char, byte[]> Glyphs = new()
    {
        [' '] = GlyphRow(0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00),
        ['-'] = GlyphRow(0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00),
        ['.'] = GlyphRow(0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x0C),
        [':'] = GlyphRow(0x00, 0x04, 0x04, 0x00, 0x04, 0x04, 0x00),
        ['A'] = GlyphRow(0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11),
        ['B'] = GlyphRow(0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E),
        ['C'] = GlyphRow(0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E),
        ['D'] = GlyphRow(0x1E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x1E),
        ['E'] = GlyphRow(0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F),
        ['F'] = GlyphRow(0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10),
        ['G'] = GlyphRow(0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F),
        ['H'] = GlyphRow(0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11),
        ['I'] = GlyphRow(0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E),
        ['J'] = GlyphRow(0x07, 0x02, 0x02, 0x02, 0x02, 0x12, 0x0C),
        ['K'] = GlyphRow(0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11),
        ['L'] = GlyphRow(0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F),
        ['M'] = GlyphRow(0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11),
        ['N'] = GlyphRow(0x11, 0x19, 0x15, 0x13, 0x11, 0x11, 0x11),
        ['O'] = GlyphRow(0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E),
        ['P'] = GlyphRow(0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10),
        ['Q'] = GlyphRow(0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D),
        ['R'] = GlyphRow(0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11),
        ['S'] = GlyphRow(0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E),
        ['T'] = GlyphRow(0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04),
        ['U'] = GlyphRow(0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E),
        ['V'] = GlyphRow(0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04),
        ['W'] = GlyphRow(0x11, 0x11, 0x11, 0x15, 0x15, 0x15, 0x0A),
        ['X'] = GlyphRow(0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11),
        ['Y'] = GlyphRow(0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04),
        ['Z'] = GlyphRow(0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F),
        ['0'] = GlyphRow(0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E),
        ['1'] = GlyphRow(0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E),
        ['2'] = GlyphRow(0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F),
        ['3'] = GlyphRow(0x1E, 0x01, 0x01, 0x0E, 0x01, 0x01, 0x1E),
        ['4'] = GlyphRow(0x12, 0x12, 0x12, 0x1F, 0x02, 0x02, 0x02),
        ['5'] = GlyphRow(0x1F, 0x10, 0x10, 0x1E, 0x01, 0x01, 0x1E),
        ['6'] = GlyphRow(0x0E, 0x10, 0x10, 0x1E, 0x11, 0x11, 0x0E),
        ['7'] = GlyphRow(0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08),
        ['8'] = GlyphRow(0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E),
        ['9'] = GlyphRow(0x0E, 0x11, 0x11, 0x0F, 0x01, 0x01, 0x0E),
    };

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MPS.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("MPS.sln was not found above the tool output.");
    }

    private sealed record Sheet(
        string FileStem,
        string Maker,
        string Model,
        string Instrument,
        string InstrumentPlain,
        string ToolId,
        string ChamberId,
        string ObservedAt,
        string Parameter,
        string PlainName,
        string Value,
        string Unit,
        string SpecMinimum,
        string SpecMaximum,
        string Shape,
        string Operator,
        string Trace,
        bool Scan)
    {
        public List<string> Lines()
        {
            var lines = new List<string>
            {
                Maker.ToUpperInvariant(),
                "Model: " + Model + " " + InstrumentPlain,
                "Printout",
                "Tool: " + ToolId,
                "Chamber: " + ChamberId,
                "Observed at: " + ObservedAt,
                "Instrument: " + Instrument,
                "Parameter: " + Parameter,
                "Plain name: " + PlainName,
                "Value: " + Value,
                "Unit: " + Unit,
                "Spec minimum: " + SpecMinimum,
                "Spec maximum: " + SpecMaximum,
            };
            if (Shape.Length > 0)
            {
                lines.Add("Shape: " + Shape);
            }

            lines.Add("Operator: " + Operator);
            lines.Add("Reading table");
            lines.Add("Channel " + PlainName + "  value " + Value + " " + Unit + "  spec " + SpecMinimum + "-" + SpecMaximum);
            lines.Add("Trace: " + Trace);
            return lines;
        }

        public ReadingJson Reading()
        {
            return new ReadingJson(
                ToolId,
                ChamberId,
                ObservedAt,
                Instrument,
                Parameter,
                double.Parse(Value, CultureInfo.InvariantCulture),
                Unit,
                double.Parse(SpecMinimum, CultureInfo.InvariantCulture),
                double.Parse(SpecMaximum, CultureInfo.InvariantCulture),
                Shape.Length == 0 ? null : Shape,
                Operator);
        }
    }

    private sealed record Manifest(IReadOnlyList<ManifestFile> Files);

    private sealed record ManifestFile(string FileName, string Sha256, ReadingJson Reading);

    private sealed record ReadingJson(
        string ToolId,
        string ChamberId,
        string ObservedAt,
        string Instrument,
        string Parameter,
        double Value,
        string Unit,
        double SpecMinimum,
        double SpecMaximum,
        string? Shape,
        string OperatorInitials);
}
