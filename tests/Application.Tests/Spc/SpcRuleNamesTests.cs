// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Spc;

namespace Mps.Application.Tests.Spc;

public sealed class SpcRuleNamesTests
{
    [Theory]
    [InlineData("western-electric-1", "Western Electric rule 1 (one reading more than 3 sigma from the average)")]
    [InlineData("nelson-6-rising", "Nelson rule 6 (6 readings in a row, each higher than the last)")]
    [InlineData("western-electric-1+western-electric-2+western-electric-3+western-electric-4+nelson-6-rising", "Western Electric rules 1-4 and Nelson rule 6 (rising trend)")]
    public void PlainNamesNeverShowRawIds(string rule, string expected) => SpcRuleNames.Plain(rule).Should().Be(expected);
}
