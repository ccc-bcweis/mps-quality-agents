// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Events;

namespace Mps.Domain.Tests;

public sealed class EventTypesTests
{
    [Fact]
    public void EventNamesAreSpelledOnce()
    {
        EventTypes.TelemetryReading.Should().Be("telemetry.reading");
        EventTypes.AlertRaised.Should().Be("alert.raised");
        EventTypes.AlertCleared.Should().Be("alert.cleared");
        EventTypes.FaultCleared.Should().Be("fault.cleared");
        EventTypes.ScenarioStopped.Should().Be("command.scenario.stop");
        EventTypes.RuleMatched.Should().Be("rule.matched");
        EventTypes.TicketCreated.Should().Be("ticket.created");
        EventTypes.TicketCapped.Should().Be("ticket.capped");
        EventTypes.IncidentChanged.Should().Be("incident.changed");
        EventTypes.InvestigationStep.Should().Be("investigation.step");
        EventTypes.InvestigationChanged.Should().Be("investigation.changed");
        EventTypes.ApprovalGranted.Should().Be("approval.granted");
        EventTypes.CapaCreated.Should().Be("capa.created");
        EventTypes.CapaAdvanced.Should().Be("capa.advanced");
        EventTypes.DocumentUploaded.Should().Be("document.uploaded");
        EventTypes.FaultInject.Should().Be("command.fault.inject");
        EventTypes.ScenarioStart.Should().Be("command.scenario.start");
        EventTypes.ResetHard.Should().Be("command.reset.hard");
        EventTypes.ResetSoft.Should().Be("command.reset.soft");
        EventTypes.OperatorReport.Should().Be("command.operator.report");
        EventTypes.DocumentUpload.Should().Be("command.document.upload");
        EventTypes.ApprovalGrant.Should().Be("command.approval.grant");
        EventTypes.CapaAdvance.Should().Be("command.capa.advance");
        EventTypes.InvestigationStart.Should().Be("command.investigation.start");
    }

    [Theory]
    [InlineData("telemetry.reading", true)]
    [InlineData("Telemetry.Reading", false)]
    [InlineData("telemetry.reading ", false)]
    [InlineData("alert.raised", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OnlyTheTelemetryReadingNameIsTelemetry(string? type, bool telemetry)
    {
        EventTypes.IsTelemetry(type!).Should().Be(telemetry);
    }
}

public sealed class DomainRuleExceptionTests
{
    [Fact]
    public void TheExceptionKeepsTheMessageAndTheInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var exception = new DomainRuleException("outer", inner);

        exception.Message.Should().Be("outer");
        exception.InnerException.Should().BeSameAs(inner);
        new DomainRuleException("plain").Message.Should().Be("plain");
    }
}
