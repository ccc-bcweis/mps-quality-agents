// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Spc;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Telemetry;

namespace Mps.Application.Tests.Spc;

public sealed class SpcSummaryTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 2, 23, 53, 0, TimeSpan.Zero);

    [Fact]
    public void TitleSaysTheSignalTheValueAndTheThreeSigmaLimitInPlainWords()
    {
        var sample = new SpcSample(new ToolId("E-01"), "E-01-A", "cf4Flow", new ControlBaseline(0d, 1d, null, null), [new TelemetryPoint(At, -4.75)])
        {
            Display = new SpcDisplay(50d, 2d, "sccm", "Etch"),
        };
        string[] rules = ["western-electric-1", "western-electric-2", "western-electric-3", "western-electric-4"];
        var violations = rules.Select(rule => new SpcViolation(rule, new ToolId("E-01"), "E-01-A", "cf4Flow", At, -4.75, -4.75, Severity.High)).ToArray();

        var text = SpcSummary.Describe(sample, violations);

        Assert.Equal("CF4 gas flow on Etch E-01, Chamber A fell to 40.5 sccm, below the 3-sigma lower limit of 44.0 sccm (Western Electric rules 1-4 tripped).", text);
        Assert.DoesNotContain("western-electric", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ShiftWithoutAThreeSigmaPointIsDescribedAsASustainedShift()
    {
        var sample = new SpcSample(new ToolId("E-03"), "E-03-A", "rfBiasPower", new ControlBaseline(0d, 1d, null, null), [new TelemetryPoint(At, 1.5)])
        {
            Display = new SpcDisplay(300d, 20d, "W", "Etch"),
        };
        var violations = new[] { new SpcViolation("western-electric-4", new ToolId("E-03"), "E-03-A", "rfBiasPower", At, 1.5, 1.5, Severity.Medium) };

        var text = SpcSummary.Describe(sample, violations);

        Assert.Equal("RF bias power on Etch E-03, Chamber A rose to 330 W, a sustained shift above the average of 300 W (Western Electric rule 4 tripped).", text);
    }
}
