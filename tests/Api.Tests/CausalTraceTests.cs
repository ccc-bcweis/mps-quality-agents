// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

using Mps.Api.Bus;
using Mps.Application.Bus;
using Mps.Application.Spc;
using Mps.Domain.Events;
using Mps.Domain.Identity;
using Mps.Domain.Telemetry;

namespace Mps.Api.Tests;

public sealed class CausalTraceTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task UnknownEventReturnsNotFound()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/trace/not-an-event");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FaultCascadeReconstructsFromTheCommandToTheCorrectiveAction()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var parameter = "gasFlow-" + Guid.NewGuid().ToString("N");
        var injected = await client.PostAsJsonAsync("/api/faults", new
        {
            toolId = "E-02",
            chamberId = "A",
            parameter,
            shape = "step",
            magnitude = 4.5,
            rootCauseCategory = "supplyChainMaterial",
        });
        var accepted = await injected.Content.ReadFromJsonAsync<JsonElement>(Json);
        var correlationId = accepted.GetProperty("correlationId").GetString()!;

        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<SqliteEventStore>();
        var sink = scope.ServiceProvider.GetRequiredService<ILifecycleEventSink>();
        var spc = scope.ServiceProvider.GetRequiredService<SpcAlertService>();
        var fault = store.FindFirst(correlationId, EventTypes.FaultInject);
        fault.Should().NotBeNull();
        var now = DateTimeOffset.UtcNow;
        var readingId = await sink.PublishAsync(
            new LifecycleEvent(
                EventTypes.TelemetryReading,
                correlationId,
                fault!.EventId,
                fault.EventId,
                "simulator",
                null,
                "E-02",
                "A",
                now,
                new { parameter, value = 4d, low = -100d, high = 100d }),
            CancellationToken.None);
        var engine = new SpcRuleEngine(SpcRuleSettings.Shared);
        engine.Observe(new SpcReading("E-02", "A", parameter, "periodic", now.AddMinutes(-1), 0.2d, 0.2d, 0d, 1d, -100d, 100d, correlationId, null));
        var breaking = engine.Observe(new SpcReading("E-02", "A", parameter, "periodic", now, 4d, 4d, 0d, 1d, -100d, 100d, correlationId, null));
        breaking.Should().NotBeNull();
        breaking!.Action.Should().Be(SpcAction.Raise);
        breaking.CorrelationId.Should().Be(correlationId);
        var outcome = await spc.ApplyAsync(breaking, null, CancellationToken.None);
        outcome.Incident.Should().NotBeNull();
        outcome.Ticket.Should().NotBeNull();

        var started = await client.PostAsync(
            "/api/incidents/" + outcome.Incident!.Id.Value.ToString("D") + "/investigations",
            null);
        var investigation = await started.Content.ReadFromJsonAsync<JsonElement>(Json);
        var investigationId = investigation.GetProperty("id").GetGuid();
        var evidence = investigation.GetProperty("evidence")[0];
        evidence.GetProperty("sourceEventIds").EnumerateArray().Select(item => item.GetString()).Should().Contain(readingId);

        var approved = await client.PostAsJsonAsync(
            "/api/investigations/" + investigationId.ToString("D") + "/decisions",
            new { kind = "approve", approverId = "eng-17", comment = "Hold the lot." });
        var traceResponse = await client.GetAsync("/api/trace/" + fault.EventId);
        var trace = await traceResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var types = new List<string>();
        Collect(trace.GetProperty("tree"), types);

        injected.StatusCode.Should().Be(HttpStatusCode.Accepted);
        started.StatusCode.Should().Be(HttpStatusCode.Created);
        approved.StatusCode.Should().Be(HttpStatusCode.OK);
        traceResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        trace.GetProperty("focusEventId").GetString().Should().Be(fault.EventId);
        trace.GetProperty("rootEventId").GetString().Should().Be(fault.EventId);
        trace.GetProperty("ancestors").GetArrayLength().Should().Be(0);
        trace.GetProperty("tree").GetProperty("type").GetString().Should().Be(EventTypes.FaultInject);
        types.Should().Contain(
        [
            EventTypes.TelemetryReading,
            EventTypes.AlertRaised,
            EventTypes.TicketCreated,
            EventTypes.InvestigationStep,
            EventTypes.ApprovalGranted,
            EventTypes.CapaCreated,
        ]);
        var storedReading = store.Find(readingId);
        storedReading.Should().NotBeNull();
        storedReading!.ParentEventId.Should().Be(fault.EventId);
        storedReading.CausationId.Should().Be(fault.EventId);
        storedReading.FromTelemetry.Should().BeTrue();
    }

    [Fact]
    public async Task SignalRReceivesTheFaultFromTheBus()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/fleet"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>("faultInjected", payload => received.TrySetResult(payload));
        await connection.StartAsync();
        var parameter = "gasFlow-" + Guid.NewGuid().ToString("N");

        var posted = await client.PostAsJsonAsync("/api/faults", new
        {
            toolId = "E-02",
            chamberId = "A",
            parameter,
            shape = "step",
            magnitude = 4.5,
            rootCauseCategory = "processDrift",
        });
        var accepted = await posted.Content.ReadFromJsonAsync<JsonElement>(Json);
        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

        posted.StatusCode.Should().Be(HttpStatusCode.Accepted);
        message.GetProperty("faultId").GetGuid().Should().Be(accepted.GetProperty("faultId").GetGuid());
        message.GetProperty("correlationId").GetString().Should().Be(accepted.GetProperty("correlationId").GetString());
        await connection.StopAsync();
    }

    private static void Collect(JsonElement node, List<string> types)
    {
        types.Add(node.GetProperty("type").GetString() ?? string.Empty);
        foreach (var child in node.GetProperty("children").EnumerateArray())
        {
            Collect(child, types);
        }
    }
}
