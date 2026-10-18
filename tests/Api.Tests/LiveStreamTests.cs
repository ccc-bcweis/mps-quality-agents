// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

using Mps.Api.Bus;
using Mps.Api.Demo;
using Mps.Api.Hubs;
using Mps.Api.Live;
using Mps.Bus;
using Mps.Domain.Events;

namespace Mps.Api.Tests;

/// <summary>
/// The console reads live data from the fleet hub instead of polling: toolLive to the pages watching a tool,
/// fleetLive to the pages watching a plant, and record changes as the changed record.
/// </summary>
public sealed class LiveStreamTests
{
    private const string Tool = "E-02";

    [Fact]
    public async Task AWatchedToolGetsNewPointsAndReadingsAndAnUnwatchedPageGetsNoTelemetry()
    {
        await using var factory = Factory();
        await using var watcher = await ConnectAsync(factory);
        await using var bystander = await ConnectAsync(factory);
        var toolLive = Capture(watcher, "toolLive");
        var strayTool = Capture(bystander, "toolLive");
        var strayFleet = Capture(bystander, "fleetLive");
        var strayTelemetry = Capture(bystander, "telemetry");
        await watcher.InvokeAsync("SubscribeTool", Tool);

        var message = await PumpUntilAsync(factory, toolLive);

        message.GetProperty("toolId").GetString().Should().Be(Tool);
        message.GetProperty("resync").GetBoolean().Should().BeFalse();
        message.GetProperty("readings").GetArrayLength().Should().BeGreaterThan(0);
        var series = message.GetProperty("series").EnumerateArray().ToArray();
        series.Should().NotBeEmpty();
        series.Should().OnlyContain(row => row.GetProperty("points").GetArrayLength() > 0);
        series.Should().Contain(row => row.GetProperty("sigma").ValueKind == JsonValueKind.Number);
        message.GetProperty("status").GetString().Should().NotBeNullOrWhiteSpace();
        strayTool.Task.IsCompleted.Should().BeFalse();
        strayFleet.Task.IsCompleted.Should().BeFalse();
        strayTelemetry.Task.IsCompleted.Should().BeFalse("telemetry no longer goes to every connected page");
    }

    [Fact]
    public async Task APlantWatcherGetsOnlyTheChangedToolsOfThatPlant()
    {
        await using var factory = Factory();
        await using var watcher = await ConnectAsync(factory);
        var fleetLive = Capture(watcher, "fleetLive");
        await watcher.InvokeAsync("SubscribeSite", "ironwood");

        var message = await PumpUntilAsync(factory, fleetLive);

        var tools = message.GetProperty("tools").EnumerateArray().ToArray();
        tools.Should().NotBeEmpty();
        tools.Should().OnlyContain(tool => tool.GetProperty("siteId").GetString() == "ironwood");
        tools.Should().OnlyContain(tool => tool.GetProperty("readings").GetArrayLength() > 0);
    }

    [Fact]
    public async Task NothingIsCollectedWhileNoPageWatchesATool()
    {
        await using var factory = Factory();
        var publisher = factory.Services.GetRequiredService<LiveStreamPublisher>();
        var subscriptions = factory.Services.GetRequiredService<LiveSubscriptions>();
        await using var page = await ConnectAsync(factory);
        var toolLive = Capture(page, "toolLive");

        await PumpAsync(factory);
        await publisher.FlushAsync(CancellationToken.None);

        subscriptions.Any.Should().BeFalse();
        toolLive.Task.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ResetProgressIsPushedAndAFinishedResetAsksEveryPageToReload()
    {
        await using var factory = Factory();
        await using var page = await ConnectAsync(factory);
        var progress = new List<string>();
        var resync = Capture(page, "resync");
        page.On<JsonElement>("resetProgress", payload =>
        {
            lock (progress)
            {
                progress.Add(payload.GetProperty("phase").GetString()!);
            }
        });
        var reset = factory.Services.GetRequiredService<ResetProgress>();

        reset.Begin("hard");
        reset.Report("purging", "Clearing tickets.");
        reset.Complete("hard");

        var reload = await resync.Task.WaitAsync(TimeSpan.FromSeconds(5));
        reload.GetProperty("reason").GetString().Should().Be("reset");
        lock (progress)
        {
            progress.Should().Contain(["starting", "purging", "done"]);
        }
    }

    [Fact]
    public async Task AnOperatorReportPushesTheTicketWithItsStatusBesideTheIncident()
    {
        await using var factory = Factory();
        var client = factory.CreateClient();
        await using var page = await ConnectAsync(factory);
        var ticket = Capture(page, "ticketChanged");
        var opened = Capture(page, "ticketOpened");

        var posted = await client.PostAsJsonAsync("/api/incidents", new
        {
            toolId = "S-02",
            parameter = "vibration-" + Guid.NewGuid().ToString("N"),
            summary = "Pump vibration climbed during the ash step.",
        });

        posted.EnsureSuccessStatusCode();
        var changed = await ticket.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var first = await opened.Task.WaitAsync(TimeSpan.FromSeconds(5));
        changed.GetProperty("id").GetString().Should().Be(first.GetProperty("id").GetString());
        changed.GetProperty("status").GetString().Should().Be("open");
    }

    [Fact]
    public async Task APrintoutReadingTellsOnlyThePageWatchingThatToolToReloadItsManualReadings()
    {
        await using var factory = Factory();
        await using var watcher = await ConnectAsync(factory);
        await using var bystander = await ConnectAsync(factory);
        var toolLive = Capture(watcher, "toolLive");
        var stray = Capture(bystander, "toolLive");
        await watcher.InvokeAsync("SubscribeTool", Tool);
        await bystander.InvokeAsync("SubscribeTool", "D-01");
        var publisher = factory.Services.GetRequiredService<LiveStreamPublisher>();
        var at = DateTimeOffset.UtcNow;

        await publisher.OnManualReading(
            new EventEnvelope("e1", EventTypes.DocumentUploaded, "printout-1", null, null, 1, "printout", null, Tool, null, at, at, EventEnvelope.CurrentSchemaVersion, JsonSerializer.SerializeToElement(new { parameter = "esc-voltage", value = 1.0 })),
            CancellationToken.None);

        var message = await toolLive.Task.WaitAsync(TimeSpan.FromSeconds(5));
        message.GetProperty("manual").GetBoolean().Should().BeTrue();
        message.GetProperty("series").GetArrayLength().Should().Be(0);
        stray.Task.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void SubscriptionsCountEachConnectionOnceAndForgetADroppedConnection()
    {
        var subscriptions = new LiveSubscriptions();
        var key = LiveSubscriptions.ToolKey(Tool);

        subscriptions.Add("a", key).Should().BeTrue();
        subscriptions.Add("a", key).Should().BeFalse();
        subscriptions.Add("b", key).Should().BeTrue();
        subscriptions.Remove("a", key).Should().BeTrue();
        subscriptions.Watched(key).Should().BeTrue();
        subscriptions.Drop("b");

        subscriptions.Watched(key).Should().BeFalse();
        subscriptions.Any.Should().BeFalse();
        LiveSubscriptions.SiteKey(" Ironwood ").Should().Be("site:ironwood");
    }

    private static WebApplicationFactory<Program> Factory() => new ApiFactory().WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Mps:Simulation:PrimeSeconds", "0");
        builder.UseSetting("Mps:Simulation:PushIntervalMilliseconds", "1");
    });

    private static async Task<HubConnection> ConnectAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/fleet"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();
        await connection.StartAsync();
        return connection;
    }

    private static TaskCompletionSource<JsonElement> Capture(HubConnection connection, string name)
    {
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>(name, payload => received.TrySetResult(payload));
        return received;
    }

    private static async Task PumpAsync(WebApplicationFactory<Program> factory)
    {
        var plant = factory.Services.GetRequiredService<PlantSimulation>();
        var telemetry = factory.Services.GetRequiredService<TelemetryPublisher>();
        for (var tick = 0; tick < 3; tick++)
        {
            plant.AdvanceTick();
        }

        await telemetry.PublishPendingAsync(CancellationToken.None);
    }

    private static async Task<JsonElement> PumpUntilAsync(WebApplicationFactory<Program> factory, TaskCompletionSource<JsonElement> received)
    {
        var publisher = factory.Services.GetRequiredService<LiveStreamPublisher>();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!received.Task.IsCompleted && DateTime.UtcNow < deadline)
        {
            await PumpAsync(factory);
            await publisher.FlushAsync(CancellationToken.None);
            await Task.WhenAny(received.Task, Task.Delay(200));
        }

        return await received.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }
}
