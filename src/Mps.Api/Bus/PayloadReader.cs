// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

namespace Mps.Api.Bus;

internal static class PayloadReader
{
    public static string? String(JsonElement payload, string name)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        return value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : value.ToString();
    }

    public static bool Bool(JsonElement payload, string name)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty(name, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => string.Equals(value.GetString(), "true", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    public static double? Number(JsonElement payload, string name)
    {
        if (payload.ValueKind != JsonValueKind.Object
            || !payload.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetDouble(out var number)
            || !double.IsFinite(number))
        {
            return null;
        }

        return number;
    }

    public static string[] Strings(JsonElement payload, string name)
    {
        if (payload.ValueKind != JsonValueKind.Object
            || !payload.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            {
                list.Add(item.GetString()!);
            }
        }

        return [.. list];
    }
}
