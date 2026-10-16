// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Explain;
using Mps.Domain.Evidence;
using Mps.Domain.Identity;

namespace Mps.Agents.Tests;

public sealed class LimitSentenceTests
{
    [Fact]
    public void InSpecRuleBreakSaysOutOfControlNotOutsideTheWindow()
    {
        var sentence = WhyItMattersCatalog.LimitSentence(Item(54.669, 48, 56));

        sentence.Should().Contain("still inside its recipe spec").And.Contain("out of control");
        sentence.Should().NotContain("outside");
        PlainSentence.IsValid(sentence).Should().BeTrue();
    }

    [Fact]
    public void OutOfSpecSaysOutOfSpec()
    {
        var sentence = WhyItMattersCatalog.LimitSentence(Item(54.669, 46, 54));

        sentence.Should().Contain("outside its recipe spec").And.Contain("out of spec");
        PlainSentence.IsValid(sentence).Should().BeTrue();
    }

    [Fact]
    public void NoStoredLimitsGivesNoLimitSentence()
    {
        var item = new EvidenceItem(EvidenceId.New(), EvidenceSource.Spc, "t", "t", DateTimeOffset.UnixEpoch, "E-02", "spc:E-02:wallTemperature:x:western-electric-4", EvidenceReading.Place("E-02", "A", 59.9));

        WhyItMattersCatalog.LimitSentence(item).Should().BeNull();
    }

    private static EvidenceItem Item(double value, double low, double high) =>
        new(
            EvidenceId.New(),
            EvidenceSource.Spc,
            "Rule on ESC temperature",
            "x",
            DateTimeOffset.UnixEpoch,
            "E-02",
            "spc:E-02:escTemperature:x:western-electric-2",
            EvidenceReading.Place("E-02", "A", value) with { SpecLow = low, SpecHigh = high, Center = (low + high) / 2d, Sigma = (high - low) / 6d });
}
