// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Application;

/// <summary>
/// The clock business records are stamped with: alerts, tickets, investigations, decisions, and CAPA.
/// In the demo host it reads the plant simulation's current time, so records sort after the readings that caused them.
/// Timeouts, approval-token lifetimes, and spend caps stay on wall time.
/// </summary>
public sealed class RecordClock : TimeProvider
{
    private readonly Func<DateTimeOffset> _now;

    public RecordClock(Func<DateTimeOffset> now)
    {
        ArgumentNullException.ThrowIfNull(now);
        _now = now;
    }

    public override DateTimeOffset GetUtcNow() => _now();
}
