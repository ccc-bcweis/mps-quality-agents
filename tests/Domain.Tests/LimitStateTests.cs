// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Evidence;

namespace Mps.Domain.Tests;

public sealed class LimitStateTests
{
    [Fact]
    public void TheStateNamesAreStable()
    {
        LimitState.OutOfSpec.Should().Be("outOfSpec");
        LimitState.OutOfControl.Should().Be("outOfControl");
        LimitState.InControl.Should().Be("inControl");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void AReadingIsOutOfSpecBeforeItIsOutOfControl(
        double? value,
        double? specLow,
        double? specHigh,
        double? controlLow,
        double? controlHigh,
        bool ruleBroke,
        string? expected)
    {
        LimitState.Of(value, specLow, specHigh, controlLow, controlHigh, ruleBroke).Should().Be(expected);
    }

    public static TheoryData<double?, double?, double?, double?, double?, bool, string?> Cases() => new()
    {
        { null, 0d, 10d, 2d, 8d, false, null },
        { double.NaN, 0d, 10d, 2d, 8d, false, null },
        { double.PositiveInfinity, 0d, 10d, 2d, 8d, true, null },
        { double.NegativeInfinity, 0d, 10d, null, null, false, null },
        { 5d, null, null, null, null, false, null },
        { 5d, null, null, null, null, true, LimitState.OutOfControl },
        { 5d, 0d, 10d, null, null, false, LimitState.InControl },
        { 0d, 0d, 10d, null, null, false, LimitState.InControl },
        { 10d, 0d, 10d, null, null, false, LimitState.InControl },
        { -0.1d, 0d, 10d, null, null, false, LimitState.OutOfSpec },
        { 10.1d, 0d, 10d, null, null, false, LimitState.OutOfSpec },
        { 5d, 0d, null, null, null, false, LimitState.InControl },
        { -0.1d, 0d, null, null, null, false, LimitState.OutOfSpec },
        { 5d, null, 10d, null, null, false, LimitState.InControl },
        { 10.1d, null, 10d, null, null, false, LimitState.OutOfSpec },
        { 5d, 0d, 10d, 2d, 8d, false, LimitState.InControl },
        { 2d, 0d, 10d, 2d, 8d, false, LimitState.InControl },
        { 8d, 0d, 10d, 2d, 8d, false, LimitState.InControl },
        { 1.9d, 0d, 10d, 2d, 8d, false, LimitState.OutOfControl },
        { 8.1d, 0d, 10d, 2d, 8d, false, LimitState.OutOfControl },
        { 5d, 0d, 10d, 2d, 8d, true, LimitState.OutOfControl },
        { 0d, 0d, 10d, 2d, 8d, true, LimitState.OutOfControl },
        { -0.1d, 0d, 10d, 2d, 8d, true, LimitState.OutOfSpec },
        { 5d, null, null, 2d, 8d, false, LimitState.InControl },
        { 1d, null, null, 2d, null, false, LimitState.OutOfControl },
        { 3d, null, null, 2d, null, false, LimitState.InControl },
        { 2d, null, null, 2d, null, false, LimitState.InControl },
        { 9d, null, null, null, 8d, false, LimitState.OutOfControl },
        { 8d, null, null, null, 8d, false, LimitState.InControl },
        { 11d, null, 10d, null, 8d, false, LimitState.OutOfSpec },
    };
}
