// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net.Http.Json;
using System.Text.Json;

namespace Mps.Api.Tests;

/// <summary>Waits for an evidence round that the API runs after it answers.</summary>
internal static class EvidenceRounds
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<JsonElement> WaitForRoundAsync(HttpClient client, Guid incidentId, int round)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (true)
        {
            var rounds = await client.GetFromJsonAsync<JsonElement>("/api/incidents/" + incidentId.ToString("D") + "/investigations", Json);
            var match = rounds.EnumerateArray().FirstOrDefault(row => row.GetProperty("round").GetInt32() == round);
            if (match.ValueKind == JsonValueKind.Object
                && match.GetProperty("stage").GetString() is not ("started" or "evidenceGathered"))
            {
                return match;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("Round " + round + " did not finish: " + rounds);
            }

            await Task.Delay(100);
        }
    }
}
