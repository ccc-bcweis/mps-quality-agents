// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mps.Bus;

public static class EventJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    public static JsonElement Payload(object? payload)
    {
        if (payload is JsonElement element)
        {
            return element.ValueKind == JsonValueKind.Undefined ? Empty() : element.Clone();
        }

        if (payload is null)
        {
            return Empty();
        }

        return JsonSerializer.SerializeToElement(payload, payload.GetType(), Options);
    }

    public static JsonElement Empty() => JsonSerializer.SerializeToElement(new { }, Options);
}
