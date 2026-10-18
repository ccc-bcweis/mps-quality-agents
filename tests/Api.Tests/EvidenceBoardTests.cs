// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using Mps.Api.Demo;
using Mps.Api.Live;
using Mps.Bus;
using Mps.Domain.Events;
using Mps.Domain.Identity;

namespace Mps.Api.Tests;

/// <summary>Evidence is keyed by fault correlation. Two faults keep their records apart.</summary>
public sealed class EvidenceBoardTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly DateTimeOffset When = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task InjectedFaultsAndScenariosKeepRecordsOnTheirOwnCorrelations()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var board = factory.Services.GetRequiredService<ScenarioEvidenceBoard>();
        var tools = new SeedEnterpriseTools(
            Path.Combine(Root(), "data", "seed", "enterprise.json"),
            Path.Combine(Root(), "data", "scenarios", "evidence.json"),
            board);

        var first = await InjectAsync(client, InlineFault("E-01", "ECO-BOARD-A"));
        var second = await InjectAsync(client, CatalogFault("E-02", "ECO-BOARD-B"));
        first.Should().NotBe(second);

        using (board.Use(first))
        {
            var ecos = await tools.GetEcosAsync(new ToolId("E-01"), When.AddDays(-1), When.AddDays(40), CancellationToken.None);
            ecos.Should().Contain(eco => eco.EcoId == "ECO-BOARD-A");
            ecos.Should().NotContain(eco => eco.EcoId == "ECO-BOARD-B");
        }

        var catalog = board.Find(second);
        catalog.Should().NotBeNull();
        catalog!.Records!.Ecos.Select(eco => eco.EcoId).Should().BeEquivalentTo("ECO-4471", "ECO-BOARD-B");
        catalog.Records.WorkOrders.Should().Contain(order => order.WorkOrderId == "WO-PM-8821");
        board.Find(first)!.Records!.Ecos.Should().ContainSingle(eco => eco.EcoId == "ECO-BOARD-A");

        using (board.Use(second))
        {
            var ecos = await tools.GetEcosAsync(new ToolId("E-02"), When.AddDays(-1), When.AddDays(40), CancellationToken.None);
            ecos.Should().Contain(eco => eco.EcoId == "ECO-BOARD-B");
            ecos.Should().NotContain(eco => eco.EcoId == "ECO-BOARD-A");
        }

        (await tools.GetEcosAsync(new ToolId("E-01"), When.AddDays(-1), When.AddDays(40), CancellationToken.None))
            .Should().NotContain(eco => eco.EcoId == "ECO-BOARD-A");

        var bus = factory.Services.GetRequiredService<IEventBus>();
        await bus.PublishAsync(EventEnvelope.Draft(EventTypes.FaultCleared, first, "test", DateTimeOffset.UtcNow, new { signal = "faultCleared" }));
        board.Find(first).Should().BeNull();
        board.Find(second).Should().NotBeNull();

        var etch = await StartAsync(client, "SC-2");
        var scope = await StartAsync(client, "SC-6");
        etch.Should().NotBe(scope);
        etch.Should().NotBe(second);

        using (board.Use(etch))
        {
            (await tools.GetCalibrationsAsync(new ToolId("E-01"), CancellationToken.None))
                .Should().Contain(record => record.CalibrationId == "CAL-E01-ESC-TC")
                .And.NotContain(record => record.CalibrationId == "CAL-E01-RF-MATCH");
        }

        using (board.Use(scope))
        {
            (await tools.GetCalibrationsAsync(new ToolId("E-01"), CancellationToken.None))
                .Should().Contain(record => record.CalibrationId == "CAL-E01-RF-MATCH")
                .And.NotContain(record => record.CalibrationId == "CAL-E01-ESC-TC");
            (await tools.GetWorkOrdersAsync(new ToolId("E-01"), When.AddDays(-1), When.AddDays(80), CancellationToken.None))
                .Should().Contain(order => order.WorkOrderId == "WO-RF-E01-MATCH");
        }

        board.Find(second).Should().NotBeNull();

        var stopped = await client.PostAsync("/api/injector/stop", null);
        stopped.StatusCode.Should().Be(HttpStatusCode.OK);
        board.Find(etch).Should().BeNull();
        board.Find(scope).Should().BeNull();
        board.Find(second).Should().BeNull();
    }

    [Fact]
    public async Task UnknownFaultEvidenceIsRejected()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/faults", new
        {
            toolId = "E-01",
            parameter = "gasFlow",
            shape = "step",
            magnitude = 3d,
            rootCauseCategory = "supplyChainMaterial",
            evidenceFaultId = "FLT-DOES-NOT-EXIST",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<string> InjectAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/api/faults", body);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var accepted = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return accepted.GetProperty("correlationId").GetString()!;
    }

    private static async Task<string> StartAsync(HttpClient client, string scenarioId)
    {
        var response = await client.PostAsync("/api/injector/scenarios/" + scenarioId, null);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var accepted = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return accepted.GetProperty("correlationId").GetString()!;
    }

    private static object InlineFault(string toolId, string ecoId) => new
    {
        toolId,
        parameter = "gasFlow",
        shape = "step",
        magnitude = 3d,
        rootCauseCategory = "supplyChainMaterial",
        confounders = false,
        evidence = new
        {
            anchorAt = When,
            ecos = new[]
            {
                new
                {
                    ecoId,
                    title = "Change " + ecoId,
                    toolId,
                    effectiveAt = When,
                    changeSummary = "Registered with the fault.",
                },
            },
        },
    };

    private static object CatalogFault(string toolId, string ecoId) => new
    {
        toolId,
        parameter = "escTemperature",
        shape = "oscillation",
        magnitude = 3.5d,
        rootCauseCategory = "engineeringChange",
        confounders = false,
        evidenceFaultId = "FLT-ECO-4471",
        evidence = new
        {
            anchorAt = When,
            ecos = new[]
            {
                new
                {
                    ecoId,
                    title = "Change " + ecoId,
                    toolId,
                    effectiveAt = When,
                    changeSummary = "Registered with the fault.",
                },
            },
        },
    };

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "data", "seed", "enterprise.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
