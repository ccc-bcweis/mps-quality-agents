// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Identity;
using Mps.Domain.Incidents;

namespace Mps.Domain.Tests;

public sealed class AlertTests
{
    private static readonly DateTimeOffset Raised = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AnAlertStartsActiveAtOneOccurrence()
    {
        var alert = Open("  C1  ", "  escTemperature  ", "  WE1  ", "  corr-1  ");

        alert.ToolId.Value.Should().Be("E-02");
        alert.ChamberId.Should().Be("C1");
        alert.Parameter.Should().Be("escTemperature");
        alert.Rule.Should().Be("WE1");
        alert.Severity.Should().Be(Severity.Medium);
        alert.RaisedAt.Should().Be(Raised);
        alert.LastSeenAt.Should().Be(Raised);
        alert.Occurrences.Should().Be(1);
        alert.CorrelationId.Should().Be("corr-1");
        alert.ClearedAt.Should().BeNull();
        alert.IsActive.Should().BeTrue();
        alert.Status.Should().Be("active");

        Open(null, "escTemperature", "WE1", "corr-1").ChamberId.Should().BeNull();
        Open("   ", "escTemperature", "WE1", "corr-1").ChamberId.Should().BeNull();
    }

    [Fact]
    public void AnAlertRequiresAnIdAToolAParameterARuleAndACorrelation()
    {
        var tool = new ToolId("E-02");
        Rejects(() => new Alert(default, tool, "C1", "p", Severity.Low, "WE1", Raised, "c"), "Alert id is required.");
        Rejects(() => new Alert(AlertId.New(), default, "C1", "p", Severity.Low, "WE1", Raised, "c"), "Tool id is required.");
        Rejects(() => new Alert(AlertId.New(), tool, "C1", "  ", Severity.Low, "WE1", Raised, "c"), "Parameter is required.");
        Rejects(() => new Alert(AlertId.New(), tool, "C1", null!, Severity.Low, "WE1", Raised, "c"), "Parameter is required.");
        Rejects(() => new Alert(AlertId.New(), tool, "C1", "p", Severity.Low, " ", Raised, "c"), "SPC rule is required.");
        Rejects(() => new Alert(AlertId.New(), tool, "C1", "p", Severity.Low, null!, Raised, "c"), "SPC rule is required.");
        Rejects(() => new Alert(AlertId.New(), tool, "C1", "p", Severity.Low, "WE1", Raised, "  "), "Correlation id is required.");
        Rejects(() => new Alert(AlertId.New(), tool, "C1", "p", Severity.Low, "WE1", Raised, null!), "Correlation id is required.");
    }

    [Fact]
    public void ALaterViolationAppendsRulesKeepsThePeakSeverityAndMovesLastSeenForward()
    {
        var alert = Open("C1", "escTemperature", "WE1", "corr-1");

        alert.Update(" WE1 ", Severity.Medium, Raised).Should().BeFalse();
        alert.Rule.Should().Be("WE1");
        alert.Severity.Should().Be(Severity.Medium);
        alert.LastSeenAt.Should().Be(Raised);
        alert.Occurrences.Should().Be(2);

        alert.Update("we1", Severity.Low, Raised.AddMinutes(-5), 0).Should().BeFalse();
        alert.Rule.Should().Be("WE1+we1");
        alert.Severity.Should().Be(Severity.Medium);
        alert.LastSeenAt.Should().Be(Raised);
        alert.Occurrences.Should().Be(2);

        alert.Update("WE2+WE3", Severity.High, Raised.AddMinutes(10), 3).Should().BeTrue();
        alert.Rule.Should().Be("WE1+we1+WE2+WE3");
        alert.Severity.Should().Be(Severity.High);
        alert.LastSeenAt.Should().Be(Raised.AddMinutes(10));
        alert.Occurrences.Should().Be(5);

        alert.Update("WE2++ +WE4", Severity.Critical, Raised.AddMinutes(10)).Should().BeTrue();
        alert.Rule.Should().Be("WE1+we1+WE2+WE3+WE4");
        alert.Severity.Should().Be(Severity.Critical);
        alert.LastSeenAt.Should().Be(Raised.AddMinutes(10));

        alert.Update("+", Severity.Low, Raised.AddMinutes(11), -4).Should().BeFalse();
        alert.Rule.Should().Be("WE1+we1+WE2+WE3+WE4");
        alert.Occurrences.Should().Be(6);
        alert.LastSeenAt.Should().Be(Raised.AddMinutes(11));
    }

    [Fact]
    public void UpdateRejectsABlankRuleAndAClearedAlert()
    {
        var alert = Open("C1", "escTemperature", "WE1", "corr-1");

        var blank = () => alert.Update("  ", Severity.High, Raised.AddMinutes(1));
        blank.Should().Throw<DomainRuleException>().WithMessage("SPC rule is required.");
        alert.Severity.Should().Be(Severity.Medium);
        alert.Occurrences.Should().Be(1);

        alert.Clear(Raised.AddHours(1));
        var cleared = () => alert.Update("WE2", Severity.Critical, Raised.AddHours(2));
        cleared.Should().Throw<DomainRuleException>().WithMessage("A cleared alert cannot be updated. Raise a new alert.");
        alert.Rule.Should().Be("WE1");
        alert.Severity.Should().Be(Severity.Medium);
        alert.Status.Should().Be("cleared");
    }

    [Fact]
    public void ClearRecordsTheFirstTimeAndDoesNotMoveEarlierThanTheRaise()
    {
        var alert = Open("C1", "escTemperature", "WE1", "corr-1");

        alert.Clear(Raised.AddHours(-2));
        alert.ClearedAt.Should().Be(Raised);
        alert.IsActive.Should().BeFalse();
        alert.Status.Should().Be("cleared");

        alert.Clear(Raised.AddHours(4));
        alert.ClearedAt.Should().Be(Raised);

        var later = Open("C1", "escTemperature", "WE1", "corr-1");
        later.Clear(Raised);
        later.ClearedAt.Should().Be(Raised);

        var after = Open("C1", "escTemperature", "WE1", "corr-1");
        after.Clear(Raised.AddMinutes(30));
        after.ClearedAt.Should().Be(Raised.AddMinutes(30));
        after.Clear(Raised.AddMinutes(90));
        after.ClearedAt.Should().Be(Raised.AddMinutes(30));
    }

    [Fact]
    public void RestoreKeepsALaterLastSeenAndAtLeastOneOccurrence()
    {
        var alert = Open("C1", "escTemperature", "WE1", "corr-1");

        alert.Restore(null, 0);
        alert.LastSeenAt.Should().Be(Raised);
        alert.Occurrences.Should().Be(1);

        alert.Restore(Raised.AddMinutes(-1), 4);
        alert.LastSeenAt.Should().Be(Raised);
        alert.Occurrences.Should().Be(4);

        alert.Restore(Raised, 1);
        alert.LastSeenAt.Should().Be(Raised);
        alert.Occurrences.Should().Be(1);

        alert.Restore(Raised.AddMinutes(8), 6);
        alert.LastSeenAt.Should().Be(Raised.AddMinutes(8));
        alert.Occurrences.Should().Be(6);
    }

    private static Alert Open(string? chamber, string parameter, string rule, string correlation)
    {
        return new Alert(AlertId.New(), new ToolId("E-02"), chamber, parameter, Severity.Medium, rule, Raised, correlation);
    }

    private static void Rejects<T>(Func<T> act, string message)
    {
        act.Should().Throw<DomainRuleException>().WithMessage(message);
    }
}
