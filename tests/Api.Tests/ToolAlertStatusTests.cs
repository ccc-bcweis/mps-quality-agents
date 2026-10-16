// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using FluentAssertions;

using Mps.Api.Http;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;

using Xunit;

namespace Mps.Api.Tests;

public sealed class ToolAlertStatusTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 15, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToolStatusIsTheWorstActiveAlertAndACleardAlertIsNominal()
    {
        var watch = new Alert(AlertId.New(), new ToolId("E-01"), "E-01-A", "etchRate", Severity.Medium, "WE4", At, "c1");
        var alarm = new Alert(AlertId.New(), new ToolId("E-02"), "E-02-A", "etchRate", Severity.High, "WE1", At, "c2");
        var both = new Alert(AlertId.New(), new ToolId("E-02"), "E-02-A", "rfBiasPower", Severity.Medium, "WE4", At, "c3");
        var cleared = new Alert(AlertId.New(), new ToolId("E-03"), "E-03-A", "etchRate", Severity.High, "WE1", At, "c4");
        cleared.Clear(At.AddMinutes(5));

        var status = ToolAlertStatus.ByTool([watch, alarm, both, cleared]);

        ToolAlertStatus.Of(status, "E-01").Should().Be("watch");
        ToolAlertStatus.Of(status, "E-02").Should().Be("alarm");
        ToolAlertStatus.Of(status, "E-03").Should().Be("nominal");
        ToolAlertStatus.Of(status, "D-01").Should().Be("nominal");
    }
}
