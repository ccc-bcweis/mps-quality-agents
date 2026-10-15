// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Bus.Tests;

public sealed class InProcessEventBusTests
{
    [Fact]
    public async Task PublishStampsEnvelopeFields()
    {
        await using var bus = new InProcessEventBus();
        EventEnvelope? seen = null;
        using var _ = bus.Subscribe(EventTypes.AlertRaised, (envelope, _) =>
        {
            seen = envelope;
            return ValueTask.CompletedTask;
        });
        var occurred = new DateTimeOffset(2026, 9, 2, 3, 4, 5, TimeSpan.Zero);

        var stamped = await bus.PublishAsync(EventEnvelope.Draft(
            EventTypes.AlertRaised,
            "fault-1",
            "spc",
            occurred,
            new { rule = "western-electric-1", value = 4.2, limitLow = 2.0, limitHigh = 6.0 },
            causationId: "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            parentEventId: "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            siteId: "northwater",
            toolId: "E-01",
            chamberId: "E-01-A"));

        stamped.EventId.Should().HaveLength(26);
        EventIds.IsWellFormed(stamped.EventId).Should().BeTrue();
        stamped.Type.Should().Be(EventTypes.AlertRaised);
        stamped.CorrelationId.Should().Be("fault-1");
        stamped.CausationId.Should().Be("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        stamped.ParentEventId.Should().Be(stamped.CausationId);
        stamped.Sequence.Should().Be(1);
        stamped.Source.Should().Be("spc");
        stamped.SiteId.Should().Be("northwater");
        stamped.ToolId.Should().Be("E-01");
        stamped.ChamberId.Should().Be("E-01-A");
        stamped.OccurredAt.Should().Be(occurred);
        stamped.RecordedAt.Should().NotBe(default);
        stamped.SchemaVersion.Should().Be(EventEnvelope.CurrentSchemaVersion);
        stamped.Payload.GetProperty("rule").GetString().Should().Be("western-electric-1");
        seen.Should().NotBeNull();
        seen!.EventId.Should().Be(stamped.EventId);
    }

    [Fact]
    public async Task SequenceIsMonotonicPerCorrelation()
    {
        await using var bus = new InProcessEventBus();
        var sequences = new List<long>();
        using var _ = bus.Subscribe("command.*", (envelope, _) =>
        {
            sequences.Add(envelope.Sequence);
            return ValueTask.CompletedTask;
        });

        await bus.PublishAsync(Draft(EventTypes.FaultInject, "same", "E-01"));
        await bus.PublishAsync(Draft(EventTypes.AlertRaised, "other", "E-01"));
        await bus.PublishAsync(Draft(EventTypes.ResetSoft, "same", "E-02"));

        sequences.Should().Equal(1, 2);
    }

    [Fact]
    public async Task OneToolStaysOrderedWhileAnotherToolRuns()
    {
        await using var bus = new InProcessEventBus();
        var seen = new List<string>();
        using var _ = bus.Subscribe(EventTypes.TelemetryReading, (envelope, _) =>
        {
            seen.Add(envelope.ToolId + ":" + envelope.Payload.GetProperty("n").GetInt32());
            return ValueTask.CompletedTask;
        });

        for (var index = 0; index < 20; index++)
        {
            await bus.PublishAsync(Reading("E-01", index));
            await bus.PublishAsync(Reading("D-01", index));
        }

        seen.Where(item => item.StartsWith("E-01:", StringComparison.Ordinal)).Should().Equal(
            Enumerable.Range(0, 20).Select(index => "E-01:" + index));
        seen.Where(item => item.StartsWith("D-01:", StringComparison.Ordinal)).Should().Equal(
            Enumerable.Range(0, 20).Select(index => "D-01:" + index));
    }

    [Fact]
    public async Task WildcardAndTypedSubscribeBothFire()
    {
        await using var bus = new InProcessEventBus();
        var types = new List<string>();
        var rules = new List<string>();
        using var all = bus.Subscribe("*", (envelope, _) =>
        {
            types.Add(envelope.Type);
            return ValueTask.CompletedTask;
        });
        using var typed = bus.Subscribe<RulePayload>(EventTypes.AlertRaised, (_, payload, _) =>
        {
            rules.Add(payload.Rule);
            return ValueTask.CompletedTask;
        });

        await bus.PublishAsync(EventEnvelope.Draft(
            EventTypes.AlertRaised,
            "c-1",
            "spc",
            DateTimeOffset.UnixEpoch,
            new RulePayload("western-electric-1")));

        types.Should().Equal(EventTypes.AlertRaised);
        rules.Should().Equal("western-electric-1");
    }

    [Fact]
    public async Task BackPressureWaitsAndDoesNotDropAnAlert()
    {
        await using var bus = new InProcessEventBus(capacity: 1);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var seen = new List<string>();
        using var _ = bus.Subscribe("*", async (envelope, _) =>
        {
            seen.Add(envelope.Type);
            if (envelope.Type == EventTypes.TelemetryReading && seen.Count == 1)
            {
                entered.TrySetResult();
                await release.Task.ConfigureAwait(false);
            }
        });

        var first = bus.PublishAsync(Reading("E-01", 1));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var queued = bus.PublishAsync(Reading("E-01", 2));
        await Task.Delay(50);
        var alert = bus.PublishAsync(Draft(EventTypes.AlertRaised, "fault-1", "E-01"));
        await Task.Delay(50);

        alert.IsCompleted.Should().BeFalse();
        seen.Should().Equal(EventTypes.TelemetryReading);
        release.TrySetResult();
        await Task.WhenAll(first.AsTask(), queued.AsTask(), alert.AsTask()).WaitAsync(TimeSpan.FromSeconds(5));

        seen.Should().Equal(EventTypes.TelemetryReading, EventTypes.TelemetryReading, EventTypes.AlertRaised);
    }

    [Fact]
    public async Task TelemetryBatchReachesTheBatchSubscriberInOrder()
    {
        await using var bus = new InProcessEventBus();
        IReadOnlyList<EventEnvelope>? batch = null;
        using var _ = bus.SubscribeBatch(EventTypes.TelemetryReading, (items, _) =>
        {
            batch = items;
            return ValueTask.CompletedTask;
        });
        var drafts = Enumerable.Range(0, 5).Select(index => Reading("E-01", index)).ToArray();

        var stamped = await bus.PublishManyAsync(drafts);

        stamped.Select(item => item.Sequence).Should().Equal(1, 2, 3, 4, 5);
        batch.Should().NotBeNull();
        batch!.Select(item => item.Payload.GetProperty("n").GetInt32()).Should().Equal(0, 1, 2, 3, 4);
    }

    private static EventEnvelope Draft(string type, string correlationId, string toolId) =>
        EventEnvelope.Draft(type, correlationId, "test", DateTimeOffset.UnixEpoch, new { ok = true }, toolId: toolId);

    private static EventEnvelope Reading(string toolId, int n) =>
        EventEnvelope.Draft(
            EventTypes.TelemetryReading,
            "telemetry-" + toolId,
            "simulator",
            DateTimeOffset.UnixEpoch,
            new { n },
            toolId: toolId);

    private sealed record RulePayload(string Rule);
}
