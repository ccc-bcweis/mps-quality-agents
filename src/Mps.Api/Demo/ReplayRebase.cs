// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mps.Api.Demo;

/// <summary>
/// A recording keeps its own times. Replay moves every time by one offset so the last recorded event lands on the plant's
/// current time. Order and spacing stay the same: reading, then alert, then ticket, then investigation.
/// </summary>
public static class ReplayRebase
{
    public static IReadOnlyList<ReplayMessage> OntoNow(IReadOnlyList<ReplayMessage> messages, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (messages.Count == 0)
        {
            return messages;
        }

        var shift = now - messages.Max(message => message.OccurredAt);
        return messages.Select(message => message with
        {
            OccurredAt = message.OccurredAt + shift,
            RecordedAt = message.RecordedAt + shift,
            Payload = ShiftPayload(message.Payload, shift),
        }).ToArray();
    }

    private static JsonElement ShiftPayload(JsonElement payload, TimeSpan shift)
    {
        if (payload.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
        {
            return payload;
        }

        var node = JsonNode.Parse(payload.GetRawText());
        Walk(node, shift);
        return JsonSerializer.SerializeToElement(node);
    }

    private static void Walk(JsonNode? node, TimeSpan shift)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(pair => pair.Key).ToArray())
                {
                    if (obj[key] is JsonValue value && TryTime(value, out var at))
                    {
                        obj[key] = (at + shift).ToString("O", CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        Walk(obj[key], shift);
                    }
                }

                break;
            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    if (array[index] is JsonValue value && TryTime(value, out var at))
                    {
                        array[index] = (at + shift).ToString("O", CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        Walk(array[index], shift);
                    }
                }

                break;
        }
    }

    private static bool TryTime(JsonValue value, out DateTimeOffset at)
    {
        at = default;
        return value.TryGetValue<string>(out var text)
            && text.Length >= 19
            && text[4] == '-'
            && text[10] == 'T'
            && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out at);
    }
}
