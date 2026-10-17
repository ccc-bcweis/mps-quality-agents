// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Evidence;
using Mps.Agents.Grounding;
using Mps.Agents.Impact;
using Mps.Domain.Enterprise;

namespace Mps.Agents.Tests;

public sealed class CauseGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 21, 0, 0, TimeSpan.Zero);

    private static EvidencePack Pack(SignalShape shape, params SupplierNotice[] notices) =>
        new([], [], [], [], [], [], notices, true, shape);

    [Fact]
    public void AnOscillationCalledAGradualRiseEscalates()
    {
        CauseGuard.Check("A gradual chamber pressure rise on D-01-A.", Pack(SignalShape.Oscillation))
            .Should().Contain("gradual").And.Contain("oscillation");
    }

    [Fact]
    public void AShapeTheReadingsDoNotShowYetEscalatesWithAWayForward()
    {
        CauseGuard.Check("The chamber pressure shows a late step down.", Pack(SignalShape.Unknown))
            .Should().Contain("Request more evidence");
    }

    [Fact]
    public void ARuledOutRecordCannotBeTheCause()
    {
        var notice = new SupplierNotice("SN-SIH4-088", "Cinderwell", "GAS-SIH4", "SIH4-088", "Silane lot SIH4-088 matches the purchase specification.", Now);
        CauseGuard.Check("Silane lot SIH4-088 is the cause.", Pack(SignalShape.Oscillation, notice)).Should().Contain("SIH4-088");
    }

    [Fact]
    public void AMatchingShapeAndARealRecordPass()
    {
        CauseGuard.Check("The oscillation started after ECO-4471 changed the edge ring.", Pack(SignalShape.Oscillation)).Should().BeNull();
        CauseGuard.Check("ECO-4471 changed the edge ring before onset.", Pack(SignalShape.Unknown)).Should().BeNull();
    }

    [Fact]
    public void NamingTheProcessDriftClassIsNotAShapeClaim()
    {
        CauseGuard.Check("The oscillation started after ECO-4471; calibration is in date, so process drift is ruled out.", Pack(SignalShape.Oscillation)).Should().BeNull();
        CauseGuard.Check("The oscillation is not process-drift.", Pack(SignalShape.Oscillation)).Should().BeNull();
        CauseGuard.Check("The temperature shows a slow drift upward.", Pack(SignalShape.Oscillation)).Should().NotBeNull();
    }
}
