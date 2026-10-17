// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Spc;

namespace Mps.Application.Tests.Spc;

/// <summary>Rule sentences follow the parameters: change spc-rules.yaml and every sentence changes with it.</summary>
public sealed class SpcRuleTextTests
{
    private static SpcRuleSettings Custom()
    {
        var settings = new SpcRuleSettings { ControlLimitSigma = 2.5 };
        settings.WesternElectric.Rule1 = new SpcRuleSettings.ZoneSetting { Sigma = 2.5, Count = 1, Window = 1 };
        settings.WesternElectric.Rule2 = new SpcRuleSettings.ZoneSetting { Sigma = 1.5, Count = 3, Window = 4 };
        settings.WesternElectric.Rule3 = new SpcRuleSettings.ZoneSetting { Sigma = 0.5, Count = 5, Window = 7 };
        settings.WesternElectric.Rule4 = new SpcRuleSettings.RunSetting { Count = 9 };
        settings.NelsonRising = new SpcRuleSettings.RunSetting { Count = 7 };
        return settings;
    }

    [Theory]
    [InlineData("western-electric-1", "Western Electric rules, rule 1: one point is more than 2.5σ from the average")]
    [InlineData("western-electric-2", "Western Electric rules, rule 2: 3 of the last 4 points are more than 1.5σ out on the same side")]
    [InlineData("western-electric-3", "Western Electric rules, rule 3: 5 of the last 7 points are more than 0.5σ out on the same side")]
    [InlineData("western-electric-4", "Western Electric rules, rule 4: 9 points in a row on one side of the average")]
    [InlineData("nelson-6-rising", "Nelson rule 6: 7 readings in a row, each higher than the last")]
    public void LabelsUseTheConfiguredNumbers(string rule, string expected) => SpcRuleText.Label(rule, Custom()).Should().Be(expected);

    [Fact]
    public void PlainAndBriefUseTheConfiguredNumbers()
    {
        SpcRuleText.Plain("western-electric-2", Custom()).Should().Be("Western Electric rule 2 (3 of 4 readings more than 1.5 sigma out on one side)");
        SpcRuleText.Brief("western-electric-4", Custom()).Should().Be("9 readings in a row on one side");
        SpcRuleText.Brief("western-electric-1", Custom()).Should().Be("one reading beyond 2.5 sigma");
    }

    [Fact]
    public void TheShippedFileGivesTheSentencesTheConsoleAlwaysShowed()
    {
        var shared = SpcRuleSettings.Shared;
        SpcRuleText.Label("western-electric-1", shared).Should().Be("Western Electric rules, rule 1: one point is more than 3σ from the average");
        SpcRuleText.Label("western-electric-2", shared).Should().Be("Western Electric rules, rule 2: 2 of the last 3 points are more than 2σ out on the same side");
        SpcRuleText.Label("western-electric-4", shared).Should().Be("Western Electric rules, rule 4: 8 points in a row on one side of the average");
        SpcRuleText.Label("unknown-rule", shared).Should().Be("unknown-rule");
        shared.Clearing.Readings.Should().Be(8);
        shared.Clearing.Sigma.Should().Be(2);
        shared.LimitCheck.RecentReadings.Should().Be(20);
    }
}
