// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using Mps.Api.Bus;
using Mps.Api.Live;
using Mps.Application.Bus;
using Mps.Simulator.Scenarios;
using Mps.Simulator.Telemetry;

namespace Mps.Api.Tests;

/// <summary>
/// Each scripted scenario and a manual fault enter as commands. Readings, the process-control alert,
/// the business rule, the ticket, and the investigation hang off that command.
/// </summary>
public sealed class ScenarioBusChainTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ManualFaultIsAppliedOnceFromTheCommand()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var plant = factory.Services.GetRequiredService<PlantSimulation>();

        var response = await client.PostAsJsonAsync("/api/faults", new
        {
            toolId = "E-02",
            chamberId = "E-02-A",
            parameter = "escTemperature",
            shape = "step",
            magnitude = 4.5,
            rootCauseCategory = "engineeringChange",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        plant.Runtime.Simulator.ActiveFaults.Should().ContainSingle();
        plant.Runtime.Simulator.ActiveFaults[0].ToolId.Should().Be("E-02");
        plant.Runtime.Simulator.ActiveFaults[0].Parameter.Should().Be("escTemperature");
        plant.Runtime.Simulator.ActiveFaults[0].Shape.Should().Be(FaultShape.Step);
    }

    [Fact]
    public async Task EachScenarioCommandLinksReadingsAlertRuleTicketAndInvestigation()
    {
        await using var factory = new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Mps:Simulation:SampleStrideSeconds", "120");
            builder.UseSetting("Mps:Simulation:PrimeSeconds", "0");
        });
        var client = factory.CreateClient();
        var plant = factory.Services.GetRequiredService<PlantSimulation>();
        var telemetry = factory.Services.GetRequiredService<TelemetryPublisher>();
        var spc = factory.Services.GetRequiredService<StreamSpcPublisher>();
        var store = factory.Services.GetRequiredService<SqliteEventStore>();

        foreach (var scenario in ScriptedScenarioCatalog.All)
        {
            plant.Reset();
            spc.Clear();
            var response = await client.PostAsync("/api/injector/scenarios/" + scenario.Id, null);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
            response.StatusCode.Should().Be(HttpStatusCode.Accepted, scenario.Id);
            body.GetProperty("scenarioId").GetString().Should().Be(scenario.Id);
            body.GetProperty("telemetry").GetArrayLength().Should().BeGreaterThan(0);

            var faults = plant.Runtime.Simulator.ActiveFaults.Where(fault => fault.Parameter == scenario.Parameter).ToArray();
            faults.Should().HaveCount(scenario.ToolIds.Count, scenario.Id + " should apply each tool once");

            var correlation = body.GetProperty("correlationId").GetString()!;
            var started = store.FindFirst(correlation, LifecycleTypes.ScenarioStart);
            var fault = store.FindFirst(correlation, LifecycleTypes.FaultInject);
            started.Should().NotBeNull(scenario.Id);
            fault.Should().NotBeNull(scenario.Id);
            fault!.ParentEventId.Should().Be(started!.EventId);
            fault.CausationId.Should().Be(started.EventId);

            plant.Advance(TimeSpan.FromSeconds(AdvanceSeconds(scenario)));
            await telemetry.PublishPendingAsync(CancellationToken.None);
            await spc.PublishAsync(CancellationToken.None);
            await spc.DrainAsync();

            var rule = RuleOnTheChain(store, fault.EventId, scenario.Id);

            var trace = await client.GetFromJsonAsync<JsonElement>("/api/trace/" + rule.EventId, Json);
            var summary = trace.GetProperty("tree").GetProperty("summary").GetString()!;
            summary.Should().Contain("business rule");
            summary.Should().NotContain("rule-");
            summary.Should().NotContain(rule.EventId);
            summary.Should().NotContain(scenario.Id);
        }
    }

    /// <summary>
    /// Follows reading, alert, business rule, ticket, investigation, and the first step.
    /// A scenario can raise more than one alert. The chain is the one whose rule opened the ticket.
    /// </summary>
    private static StoredEvent RuleOnTheChain(SqliteEventStore store, string faultEventId, string scenarioId)
    {
        var readings = store.Children(faultEventId, 800)
            .Where(row => row.Type == LifecycleTypes.TelemetryReading
                && row.ParentEventId == faultEventId
                && row.CausationId == faultEventId)
            .ToArray();
        readings.Should().NotBeEmpty(scenarioId + " should stream readings from the fault");
        foreach (var reading in readings)
        {
            var alert = LinkedChild(store, reading.EventId, LifecycleTypes.AlertRaised);
            if (alert is null)
            {
                continue;
            }

            var rule = LinkedChild(store, alert.EventId, LifecycleTypes.RuleMatched);
            if (rule is null)
            {
                continue;
            }

            var ticket = LinkedChild(store, rule.EventId, LifecycleTypes.TicketCreated);
            if (ticket is null)
            {
                continue;
            }

            var investigation = LinkedChild(store, ticket.EventId, LifecycleTypes.InvestigationStart);
            if (investigation is null)
            {
                continue;
            }

            if (LinkedChild(store, investigation.EventId, LifecycleTypes.InvestigationStep) is null)
            {
                continue;
            }

            return rule;
        }

        throw new InvalidOperationException(scenarioId + " did not link a reading, an alert, a business rule, a ticket, and an investigation.");
    }

    private static StoredEvent? LinkedChild(SqliteEventStore store, string parentId, string type)
    {
        return store.Children(parentId, 40)
            .FirstOrDefault(row => row.Type == type && row.ParentEventId == parentId && row.CausationId == parentId);
    }

    private static int AdvanceSeconds(ScriptedScenario scenario)
    {
        if (scenario.Shape == FaultShape.Ramp)
        {
            return 7200;
        }

        if (scenario.Shape == FaultShape.Oscillation && scenario.PeriodSeconds >= 1000d)
        {
            return 300;
        }

        return 120;
    }
}
