// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using Mps.Domain.Identity;

namespace Mps.Simulator.Seed;

public static class SeedJson
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static void Write(EnterpriseSeed seed, string path)
    {
        ArgumentNullException.ThrowIfNull(seed);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(seed, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        File.WriteAllText(path, json);
    }

    public static EnterpriseSeed Read(string path)
    {
        return JsonSerializer.Deserialize<EnterpriseSeed>(Mps.Simulator.Telemetry.RecordDates.Rebase(File.ReadAllText(path)), Options)
            ?? throw new InvalidDataException($"Empty enterprise seed {path}.");
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };
        options.Converters.Add(new ToolIdJsonConverter());
        options.Converters.Add(new IsoTimestampJsonConverter());
        return options;
    }

    private sealed class ToolIdJsonConverter : JsonConverter<ToolId>
    {
        public override ToolId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new JsonException("Tool id is required.");
            }

            return new ToolId(value);
        }

        public override void Write(Utf8JsonWriter writer, ToolId value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Value);
        }
    }

    private sealed class IsoTimestampJsonConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new JsonException("Timestamp is required.");
            }

            return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        }
    }
}
