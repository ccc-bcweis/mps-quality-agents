// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Mps.Api.Demo;

namespace Mps.Api.Tests;

public sealed class ReplayDataModeTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ReplaySwapsTheConsoleViewAndLiveRestoresIt()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var before = await client.GetFromJsonAsync<JsonElement>("/api/fleet/live");
        var idle = await client.GetFromJsonAsync<JsonElement>("/api/demo/data-mode");

        var recorded = await client.PostAsync("/api/admin/replay/SC-1", null);
        var recordedBody = await recorded.Content.ReadFromJsonAsync<JsonElement>(Json);
        var store = factory.Services.GetRequiredService<ReplayStore>();
        var switched = await client.PostAsJsonAsync("/api/demo/data-mode", new { mode = "replay", scenarioId = "sc-1" });
        var replayMode = await switched.Content.ReadFromJsonAsync<JsonElement>(Json);
        var replayFleet = await client.GetFromJsonAsync<JsonElement>("/api/fleet/live");
        var toolId = replayFleet.GetProperty("tools")[0].GetProperty("toolId").GetString();
        var replayTool = await client.GetFromJsonAsync<JsonElement>("/api/tools/" + toolId + "/live");
        var otherTool = await client.GetAsync("/api/tools/E-01/live");
        var missingTool = await client.GetAsync("/api/tools/Z-99/live");

        var restored = await client.PostAsJsonAsync("/api/demo/data-mode", new { mode = "live" });
        var liveFleet = await client.GetFromJsonAsync<JsonElement>("/api/fleet/live");
        store.TryLoad("SC-1", out var kept).Should().BeTrue();
        var again = await client.PostAsJsonAsync("/api/demo/data-mode", new { mode = "replay", scenarioId = "SC-1" });
        var againFleet = await client.GetFromJsonAsync<JsonElement>("/api/fleet/live");
        var unknown = await client.PostAsJsonAsync("/api/demo/data-mode", new { mode = "replay", scenarioId = "SC-9" });

        before.GetProperty("tools").GetArrayLength().Should().Be(0);
        idle.GetProperty("mode").GetString().Should().Be("live");
        idle.GetProperty("messageCount").GetInt32().Should().Be(0);
        recorded.StatusCode.Should().Be(HttpStatusCode.OK);
        recordedBody.GetProperty("scenarioId").GetString().Should().Be("SC-1");
        recordedBody.GetProperty("messageCount").GetInt32().Should().BeGreaterThan(0);
        File.Exists(Path.Combine(store.Root, "SC-1", "messages.jsonl")).Should().BeTrue();
        File.Exists(Path.Combine(store.Root, "scripted", "messages.jsonl")).Should().BeFalse();
        switched.StatusCode.Should().Be(HttpStatusCode.OK);
        replayMode.GetProperty("mode").GetString().Should().Be("replay");
        replayMode.GetProperty("scenarioId").GetString().Should().Be("SC-1");
        replayFleet.GetProperty("tools").GetArrayLength().Should().BeGreaterThan(0);
        replayFleet.GetProperty("tools")[0].GetProperty("readings").GetArrayLength().Should().BeGreaterThan(0);
        replayTool.GetProperty("series").GetArrayLength().Should().BeGreaterThan(0);
        replayTool.GetProperty("series")[0].GetProperty("points").GetArrayLength().Should().BeGreaterThan(0);
        otherTool.StatusCode.Should().Be(HttpStatusCode.OK);
        missingTool.StatusCode.Should().Be(HttpStatusCode.NotFound);
        restored.StatusCode.Should().Be(HttpStatusCode.OK);
        // Recording steps the live plant, so the live view now holds the live plant, not the recorded set.
        (await restored.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("mode").GetString().Should().Be("live");
        kept.Should().NotBeEmpty();
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        againFleet.GetProperty("tools").GetArrayLength().Should().Be(replayFleet.GetProperty("tools").GetArrayLength());
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReplayControlsRejectANonAdminPrincipal()
    {
        await using var azure = new ApiFactory().WithWebHostBuilder(builder => builder.UseSetting("Mps:Auth:Mode", "azure"));
        var client = azure.CreateClient();
        client.DefaultRequestHeaders.Add("X-MS-CLIENT-PRINCIPAL", Principal("reader"));

        var mode = await client.PostAsJsonAsync("/api/demo/data-mode", new { mode = "live" });
        var record = await client.PostAsync("/api/admin/replay/SC-1", null);
        var readable = await client.GetAsync("/api/demo/data-mode");

        mode.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        record.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        readable.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static string Principal(string role)
    {
        var json = "{\"auth_typ\":\"aad\",\"claims\":[{\"typ\":\"roles\",\"val\":\"" + role + "\"}],\"role_typ\":\"roles\"}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }
}
