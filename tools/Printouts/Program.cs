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
            File.WriteAllBytes(pdfPath, Pdf(PageContent(sheet)));
            manifest.Add(Entry(pdfName, pdfPath, sheet));

            if (!sheet.Scan)
            {
                continue;
            }

            var pngName = sheet.FileStem + ".png";
            var pngPath = Path.Combine(directory, pngName);
            WriteScan(pngPath, sheet);
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
                "18",
                "count",
                "0",
                "10",
                "",
                "BN",
                "Adder count sits above the printed maximum.",
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

    private static byte[] Pdf(string stream)
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R /F2 6 0 R /F3 7 0 R >> >> >>",
            "<< /Length " + stream.Length.ToString(CultureInfo.InvariantCulture) + " >>\nstream\n" + stream + "endstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>",
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

    private static string PageContent(Sheet sheet)
    {
        var page = new StringBuilder();
        var min = double.Parse(sheet.SpecMinimum, CultureInfo.InvariantCulture);
        var max = double.Parse(sheet.SpecMaximum, CultureInfo.InvariantCulture);
        var value = double.Parse(sheet.Value, CultureInfo.InvariantCulture);

        // Header band
        page.Append("0.12 0.16 0.23 rg 0 722 612 70 re f\n");
        Text(page, "F2", 18, 40, 760, sheet.Maker.ToUpperInvariant(), "1 1 1");
        Text(page, "F1", 10, 40, 742, sheet.Model + " " + sheet.InstrumentPlain + "  |  Bench Measurement Report", "0.85 0.88 0.92");
        Text(page, "F2", 13, 40, 700, "MEASUREMENT REPORT", "0 0 0");
        Text(page, "F2", 13, 400, 700, "RESULT  OUT OF SPEC", "0.75 0.1 0.1");

        // Labeled fields, one per row so the reader keeps each key
        var fields = sheet.Fields(min, max);
        var y = 680;
        foreach (var field in fields)
        {
            Text(page, "F3", 9.5, 40, y, field, "0 0 0");
            y -= 13;
        }

        // Trace graphic
        const double left = 80, right = 560, bottom = 200;
        var top = y - 30d;
        var span = Math.Max(max - min, 1d);
        var lo = Math.Min(min, value) - (0.35 * span);
        var hi = Math.Max(max, value) + (0.35 * span);
        if (min <= 0 && value >= 0)
        {
            lo = 0;
        }

        double Y(double v) => bottom + ((v - lo) / (hi - lo) * (top - bottom));
        Text(page, "F2", 10, 40, top + 12, TraceTitle(sheet), "0 0 0");
        page.Append("0.97 0.97 0.95 rg ").Append(F(left)).Append(' ').Append(F(bottom)).Append(' ').Append(F(right - left)).Append(' ').Append(F(top - bottom)).Append(" re f\n");
        page.Append("0.85 0.85 0.85 RG 0.4 w\n");
        for (var i = 1; i < 10; i++)
        {
            var gx = left + ((right - left) * i / 10d);
            page.Append(F(gx)).Append(' ').Append(F(bottom)).Append(" m ").Append(F(gx)).Append(' ').Append(F(top)).Append(" l S\n");
        }

        for (var i = 1; i < 6; i++)
        {
            var gy = bottom + ((top - bottom) * i / 6d);
            page.Append(F(left)).Append(' ').Append(F(gy)).Append(" m ").Append(F(right)).Append(' ').Append(F(gy)).Append(" l S\n");
        }

        page.Append("0 0 0 RG 0.8 w ").Append(F(left)).Append(' ').Append(F(bottom)).Append(' ').Append(F(right - left)).Append(' ').Append(F(top - bottom)).Append(" re S\n");
        for (var i = 0; i <= 6; i += 2)
        {
            var tick = lo + ((hi - lo) * i / 6d);
            Text(page, "F1", 7, 44, bottom + ((top - bottom) * i / 6d) - 2, Number(tick), "0.3 0.3 0.3");
        }

        // Spec limits
        page.Append("[4 3] 0 d 0.8 0.1 0.1 RG 0.9 w\n");
        foreach (var limit in new[] { min, max })
        {
            page.Append(F(left)).Append(' ').Append(F(Y(limit))).Append(" m ").Append(F(right)).Append(' ').Append(F(Y(limit))).Append(" l S\n");
        }

        page.Append("[] 0 d\n");
        Text(page, "F1", 7, right + 4, Y(max) - 2, "USL " + sheet.SpecMaximum, "0.75 0.1 0.1");
        Text(page, "F1", 7, right + 4, Y(min) - 2, "LSL " + sheet.SpecMinimum, "0.75 0.1 0.1");

        if (sheet.Shape.Length == 0)
        {
            // Particle counts by size bin
            var bins = new[] { ("0.1um", 0.45), ("0.2um", 0.25), ("0.3um", 0.15), ("0.5um", 0.1), ("1.0um", 0.05) };
            var x = left + 30;
            var cumulative = 0d;
            foreach (var (label, share) in bins)
            {
                cumulative += share * value;
                page.Append("0.2 0.4 0.7 rg ").Append(F(x)).Append(' ').Append(F(bottom)).Append(" 60 ").Append(F(Y(cumulative) - bottom)).Append(" re f\n");
                Text(page, "F1", 7, x + 14, bottom - 10, label, "0.3 0.3 0.3");
                x += 90;
            }

            Text(page, "F1", 7, left, bottom - 22, "Cumulative adders per wafer, by particle size bin", "0.3 0.3 0.3");
        }
        else
        {
            page.Append("0.1 0.45 0.2 RG 1.2 w\n");
            const int points = 240;
            var mid = (min + max) / 2d;
            for (var i = 0; i <= points; i++)
            {
                var t = i / (double)points;
                var v = sheet.Shape switch
                {
                    "oscillation" => mid + ((value - mid) * Math.Sin(t * Math.PI * 12) * (0.55 + (0.45 * t))),
                    "step" => t < 0.45 ? mid + (0.08 * span * Math.Sin(t * 90)) : value + (0.03 * span * Math.Sin(t * 70)),
                    _ => (mid - (0.2 * span)) + ((value - (mid - (0.2 * span))) * t) + (0.02 * span * Math.Sin(t * 60)),
                };
                var px = left + ((right - left) * t);
                page.Append(F(px)).Append(' ').Append(F(Y(v))).Append(i == 0 ? " m\n" : " l\n");
            }

            page.Append("S\n");
            Text(page, "F1", 7, left, bottom - 12, "0 s", "0.3 0.3 0.3");
            Text(page, "F1", 7, right - 20, bottom - 12, "60 s", "0.3 0.3 0.3");
        }

        // Reading table
        var tableTop = bottom - 45d;
        page.Append("0.9 0.92 0.95 rg 40 ").Append(F(tableTop - 4)).Append(" 532 16 re f\n");
        var columns = new[] { 44d, 200d, 300d, 380d, 460d };
        var header = new[] { "Channel", "Measured", "Min", "Max", "Result" };
        var row = new[] { sheet.PlainName, sheet.Value + " " + sheet.Unit, sheet.SpecMinimum, sheet.SpecMaximum, "FAIL" };
        for (var i = 0; i < columns.Length; i++)
        {
            Text(page, "F2", 9, columns[i], tableTop, header[i], "0 0 0");
            Text(page, "F1", 9, columns[i], tableTop - 18, row[i], i == 4 ? "0.75 0.1 0.1" : "0 0 0");
        }

        page.Append("0.6 0.6 0.6 RG 0.5 w 40 ").Append(F(tableTop - 26)).Append(" m 572 ").Append(F(tableTop - 26)).Append(" l S\n");

        // Signature block
        Text(page, "F1", 9, 40, 70, "Technician signature: ______________________   Initials " + sheet.Operator, "0 0 0");
        Text(page, "F1", 9, 40, 52, "Reviewed by  ______________________   Date  ____________", "0 0 0");
        Text(page, "F1", 7, 40, 30, sheet.Maker + "  " + sheet.Model + "  Page 1 of 1  Fictional sample for demo use", "0.45 0.45 0.45");
        return page.ToString();
    }

    private static string TraceTitle(Sheet sheet)
    {
        return sheet.Shape.Length == 0 ? "PARTICLE SIZE DISTRIBUTION" : "TRACE  " + sheet.PlainName.ToUpperInvariant() + " (" + sheet.Unit + ")";
    }

    private static void Text(StringBuilder page, string font, double size, double x, double y, string text, string color)
    {
        page.Append("BT ").Append(color).Append(" rg /").Append(font).Append(' ').Append(F(size)).Append(" Tf ")
            .Append(F(x)).Append(' ').Append(F(y)).Append(" Td (").Append(Escape(text)).Append(") Tj ET\n");
    }

    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Number(double value) => Math.Abs(value) >= 100 ? value.ToString("0", CultureInfo.InvariantCulture) : value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string Escape(string line)
    {
        return line.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static void WriteScan(string path, Sheet sheet)
    {
        const int width = 900;
        const int height = 1160;
        var pixels = new byte[width * height * 3];
        for (var index = 0; index < pixels.Length; index += 3)
        {
            pixels[index] = 246;
            pixels[index + 1] = 244;
            pixels[index + 2] = 238;
        }

        var min = double.Parse(sheet.SpecMinimum, CultureInfo.InvariantCulture);
        var max = double.Parse(sheet.SpecMaximum, CultureInfo.InvariantCulture);
        var value = double.Parse(sheet.Value, CultureInfo.InvariantCulture);
        Fill(pixels, width, height, 0, 0, width, 72, 30, 41, 59);
        DrawText(pixels, width, height, 36, 16, sheet.Maker, 255, 255, 255, 3);
        DrawText(pixels, width, height, 36, 48, sheet.Model + " " + sheet.InstrumentPlain + " MEASUREMENT REPORT", 200, 210, 225, 2);
        var top = 96;
        foreach (var line in sheet.Fields(min, max))
        {
            DrawText(pixels, width, height, 40, top, line, 23, 23, 23, 2);
            top += 26;
        }

        // Trace box
        int boxTop = top + 20, boxBottom = boxTop + 260, boxLeft = 60, boxRight = 860;
        Fill(pixels, width, height, boxLeft, boxTop, boxRight - boxLeft, 2, 0, 0, 0);
        Fill(pixels, width, height, boxLeft, boxBottom, boxRight - boxLeft, 2, 0, 0, 0);
        Fill(pixels, width, height, boxLeft, boxTop, 2, boxBottom - boxTop, 0, 0, 0);
        Fill(pixels, width, height, boxRight, boxTop, 2, boxBottom - boxTop + 2, 0, 0, 0);
        var span = max - min;
        var lo = Math.Min(min, value) - (0.35 * span);
        var hi = Math.Max(max, value) + (0.35 * span);
        int Py(double v) => boxBottom - (int)((v - lo) / (hi - lo) * (boxBottom - boxTop));
        foreach (var limit in new[] { min, max })
        {
            for (var x = boxLeft; x < boxRight; x += 12)
            {
                Fill(pixels, width, height, x, Py(limit), 7, 2, 190, 30, 30);
            }
        }

        DrawText(pixels, width, height, boxRight - 120, Py(max) - 18, "USL " + sheet.SpecMaximum, 190, 30, 30, 2);
        DrawText(pixels, width, height, boxRight - 120, Py(min) + 6, "LSL " + sheet.SpecMinimum, 190, 30, 30, 2);
        var start = min + (0.3 * span);
        for (var x = 0; x < boxRight - boxLeft; x++)
        {
            var t = x / (double)(boxRight - boxLeft);
            var v = start + ((value - start) * t) + (0.02 * span * Math.Sin(t * 60));
            Fill(pixels, width, height, boxLeft + x, Py(v) - 1, 2, 3, 20, 110, 50);
        }

        // Table
        var tableTop = boxBottom + 30;
        Fill(pixels, width, height, 40, tableTop - 6, 820, 26, 225, 230, 238);
        DrawText(pixels, width, height, 48, tableTop, "CHANNEL      MEASURED   MIN   MAX   RESULT", 0, 0, 0, 2);
        DrawText(pixels, width, height, 48, tableTop + 32, sheet.PlainName + "   " + sheet.Value + " " + sheet.Unit + "   " + sheet.SpecMinimum + "   " + sheet.SpecMaximum + "   FAIL", 23, 23, 23, 2);

        // Signature
        var signatureTop = height - 90;
        DrawText(pixels, width, height, 40, signatureTop, "TECHNICIAN SIGNATURE", 23, 23, 23, 2);
        Fill(pixels, width, height, 300, signatureTop + 14, 300, 2, 40, 40, 40);
        for (var x = 0; x < 160; x++)
        {
            Fill(pixels, width, height, 320 + x, signatureTop + 2 + (int)(6 * Math.Sin(x / 9d)), 2, 2, 20, 40, 120);
        }

        DrawText(pixels, width, height, 640, signatureTop, "INITIALS " + sheet.Operator, 23, 23, 23, 2);

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
        public string Serial => Model.Replace("-", string.Empty, StringComparison.Ordinal) + "-" + ((FileStem.Sum(c => (int)c) * 7919 % 900000) + 100000).ToString(CultureInfo.InvariantCulture);

        public List<string> Fields(double min, double max)
        {
            var observed = DateTimeOffset.Parse(ObservedAt, CultureInfo.InvariantCulture);
            var lines = new List<string>
            {
                "Serial no: " + Serial,
                "Report date: " + observed.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
                "Calibration due: " + observed.AddMonths(7).ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
            };
            lines.AddRange(Lines().Skip(3).TakeWhile(line => line != "Reading table"));
            lines.Add("Specification: " + min.ToString(CultureInfo.InvariantCulture) + " to " + max.ToString(CultureInfo.InvariantCulture) + " " + Unit);
            lines.Add("Note: " + Trace);
            return lines;
        }

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
