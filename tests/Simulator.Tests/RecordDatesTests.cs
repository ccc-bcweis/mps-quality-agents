// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Simulator.Telemetry;

using Xunit;

namespace Mps.Simulator.Tests;

[Collection(RecordDatesSerial.Name)]
public sealed class RecordDatesTests
{
    [Fact]
    public void RebaseMovesEveryDateBySameWholeDays()
    {
        try
        {
            RecordDates.UseToday(new FixedTime(new DateTimeOffset(2026, 10, 15, 19, 30, 0, TimeSpan.Zero)));
            Assert.Equal(44, RecordDates.ShiftDays);
            var text = "{\"startedAt\":\"2026-08-29T14:00:00Z\",\"note\":\"ECO on 2026-08-29, lot LOT-7\"}";
            Assert.Equal("{\"startedAt\":\"2026-10-12T14:00:00Z\",\"note\":\"ECO on 2026-10-12, lot LOT-7\"}", RecordDates.Rebase(text));
            Assert.Equal(new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero), DemoClock.BaselineOrigin);
        }
        finally
        {
            RecordDates.UseReference();
        }

        Assert.Equal("2026-08-29", RecordDates.Rebase("2026-08-29"));
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RecordDatesSerial
{
    public const string Name = "RecordDates";
}

internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

public sealed class FaultPaceTests
{
    [Fact]
    public void PacedShortensPeriodAndRampButKeepsOnset()
    {
        var onset = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
        var fault = new FaultSignal("E-02", "A", "escTemperature", FaultShape.Ramp, MagnitudeKind.Sigma, 4d, onset, periodSeconds: 1200d, rampSeconds: 7200d);
        var paced = fault.Paced(60d);
        Assert.Equal(20d, paced.PeriodSeconds);
        Assert.Equal(120d, paced.RampSeconds);
        Assert.Equal(onset, paced.Onset);
        Assert.Same(fault, fault.Paced(1d));
    }
}
