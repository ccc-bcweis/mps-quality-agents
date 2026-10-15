// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Mps.Api.Demo;

namespace Mps.Api.Tests;

public sealed class ReplayRebaseTests
{
    [Fact]
    public void ReplayMovesARecordingOntoThePlantClockAndKeepsCausalOrder()
    {
        var origin = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        ReplayMessage Message(string type, int seconds, object payload) => new(
            Guid.NewGuid().ToString("N"), type, "c", null, null, seconds, "test", "northwater", "E-01", "E-01-A",
            origin.AddSeconds(seconds), origin.AddSeconds(seconds), 1, JsonSerializer.SerializeToElement(payload));

        var recorded = new[]
        {
            Message("telemetry.reading", 0, new { parameter = "escTemperature", value = 61.0 }),
            Message("alert.raised", 30, new { body = new { raisedAt = origin.AddSeconds(30).ToString("O") } }),
            Message("ticket.created", 45, new { body = new { openedAt = origin.AddSeconds(45).ToString("O") } }),
            Message("command.investigation.start", 60, new { }),
        };
        var now = new DateTimeOffset(2026, 10, 15, 13, 0, 0, TimeSpan.Zero);

        var rebased = ReplayRebase.OntoNow(recorded, now);

        rebased[^1].OccurredAt.Should().Be(now);
        rebased.Select(message => message.OccurredAt).Should().BeInAscendingOrder();
        rebased.Should().OnlyContain(message => message.OccurredAt <= now && message.OccurredAt >= now.AddMinutes(-5));
        var raised = DateTimeOffset.Parse(rebased[1].Payload.GetProperty("body").GetProperty("raisedAt").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        var opened = DateTimeOffset.Parse(rebased[2].Payload.GetProperty("body").GetProperty("openedAt").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        raised.Should().Be(rebased[1].OccurredAt);
        rebased[0].OccurredAt.Should().BeOnOrBefore(raised);
        raised.Should().BeOnOrBefore(opened);
        opened.Should().BeOnOrBefore(rebased[3].OccurredAt);
    }
}
