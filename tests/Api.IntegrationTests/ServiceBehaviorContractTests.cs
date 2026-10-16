// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;

namespace Mps.Api.IntegrationTests;

/// <summary>
/// Fleet, metrics, and fault-injector behavior from spec sections 2.1, 2.7, and 2.8.
/// </summary>
public sealed class ServiceBehaviorContractTests : IClassFixture<MpsApiFactory>
{
    private readonly MpsApiFactory _factory;

    public ServiceBehaviorContractTests(MpsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FleetListsEveryConfiguredToolInFourFamiliesWithChambers()
    {
        using var client = _factory.CreateClient();
        var tools = (await ApiClient.SendAsync(client, HttpMethod.Get, "/api/tools")).Require(HttpStatusCode.OK);

        // data/fleet/sites.yaml: three sites, 32 tools.
        tools.GetArrayLength().Should().Be(32);
        tools.EnumerateArray().Select(tool => tool.RequiredString("family")).Distinct()
            .Should().BeEquivalentTo("deposition", "etch", "strip", "clean");
        tools.EnumerateArray().Select(tool => tool.RequiredString("bay")).Distinct().Count().Should().BeGreaterThanOrEqualTo(2);
        tools.EnumerateArray().Should().OnlyContain(tool => tool.GetProperty("chambers").GetArrayLength() > 0);
    }

    [Fact]
    public async Task MetricsExposeModeAndSpend()
    {
        using var client = _factory.CreateClient();
        var metrics = (await ApiClient.SendAsync(client, HttpMethod.Get, "/api/metrics")).Require(HttpStatusCode.OK);
        metrics.RequiredString("mode").Should().BeOneOf("replay", "live");
        metrics.GetProperty("spendTodayUsd").GetDouble().Should().BeGreaterThanOrEqualTo(0);
        metrics.GetProperty("spendCapUsd").GetDouble().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task InjectedFaultIsAccepted()
    {
        using var client = _factory.CreateClient();
        var accepted = (await ApiClient.SendAsync(
            client,
            HttpMethod.Post,
            "/api/faults",
            new
            {
                toolId = "E-02",
                parameter = "escTemperature",
                shape = "oscillation",
                magnitude = 3.0,
                rootCauseCategory = "supplyChainMaterial",
                confounders = true,
            })).Require(HttpStatusCode.Accepted);
        Guid.TryParse(accepted.RequiredString("faultId"), out _).Should().BeTrue();
        accepted.RequiredString("correlationId").Should().NotBeNullOrWhiteSpace();
    }
}
