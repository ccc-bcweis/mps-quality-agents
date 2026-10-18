// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Reflection;

using NetArchTest.Rules;

namespace Mps.Architecture.Tests;

/// <summary>
/// Locks the module graph in PLAN.md section 1. Inner layers cannot see the host, agents cannot see connectors,
/// and the model gateway cannot see domain types.
/// </summary>
public sealed class LayeringTests
{
    private static readonly Assembly Domain = typeof(Mps.Domain.LayerAnchor).Assembly;

    private static readonly Assembly Bus = typeof(Mps.Bus.LayerAnchor).Assembly;

    private static readonly Assembly Application = typeof(Mps.Application.LayerAnchor).Assembly;

    private static readonly Assembly Simulator = typeof(Mps.Simulator.LayerAnchor).Assembly;

    private static readonly Assembly ModelGateway = typeof(Mps.ModelGateway.LayerAnchor).Assembly;

    private static readonly Assembly Agents = typeof(Mps.Agents.LayerAnchor).Assembly;

    private static readonly Assembly Connectors = typeof(Mps.Connectors.LayerAnchor).Assembly;

    private static readonly Assembly Orchestration = typeof(Mps.Orchestration.LayerAnchor).Assembly;

    private static readonly Assembly Api = typeof(Mps.Api.LayerAnchor).Assembly;

    private static readonly Assembly MockEnterprise = typeof(Mps.MockEnterprise.LayerAnchor).Assembly;

    private static readonly string[] Layers =
    [
        "Mps.Domain",
        "Mps.Bus",
        "Mps.Application",
        "Mps.Simulator",
        "Mps.ModelGateway",
        "Mps.Agents",
        "Mps.Connectors",
        "Mps.Orchestration",
        "Mps.Api",
        "Mps.MockEnterprise",
    ];

    [Fact]
    public void DomainDependsOnNothingElse()
    {
        AssertDoesNotDependOn(Domain, AllExcept("Mps.Domain"));
        AssertDoesNotDependOn(Domain, "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Microsoft.Extensions.AI");
    }

    [Fact]
    public void BusDependsOnlyOnDomain()
    {
        // The bus reads the event names from Mps.Domain (one spelling for bus, application and API); nothing else.
        AssertDoesNotDependOn(Bus, AllExcept("Mps.Bus", "Mps.Domain"));
        AssertDoesNotDependOn(Bus, "Mps.Application", "Mps.Api");
    }

    [Fact]
    public void ApplicationDependsOnlyOnDomain()
    {
        AssertDependsOn(Application, "Mps.Domain");
        AssertDoesNotDependOn(Application, AllExcept("Mps.Domain", "Mps.Application"));
        AssertDoesNotDependOn(Application, "Microsoft.AspNetCore");
    }

    [Fact]
    public void SimulatorDependsOnlyOnDomain()
    {
        AssertDependsOn(Simulator, "Mps.Domain");
        AssertDoesNotDependOn(Simulator, AllExcept("Mps.Domain", "Mps.Simulator"));
    }

    [Fact]
    public void ModelGatewayUsesTheAiAbstractionAndNoBusinessModules()
    {
        AssertDependsOn(ModelGateway, "Microsoft.Extensions.AI");
        AssertDoesNotDependOn(ModelGateway, AllExcept("Mps.ModelGateway"));
    }

    [Fact]
    public void AgentsUseReadPortsAndTheGatewayOnly()
    {
        AssertDependsOn(Agents, "Mps.Application", "Mps.ModelGateway");
        AssertDoesNotDependOn(
            Agents,
            AllExcept("Mps.Agents", "Mps.Application", "Mps.ModelGateway", "Mps.Domain"));
        AssertDoesNotDependOn(Agents, "Mps.Application.Writes");
    }

    [Fact]
    public void ConnectorsImplementApplicationPorts()
    {
        AssertDependsOn(Connectors, "Mps.Application", "Mps.Application.Writes");
        AssertDoesNotDependOn(
            Connectors,
            AllExcept("Mps.Connectors", "Mps.Application", "Mps.Domain"));
    }

    [Fact]
    public void OrchestrationComposesAgentsWithoutCallingTheHost()
    {
        AssertDependsOn(Orchestration, "Mps.Domain", "Mps.Application", "Mps.Agents");
        AssertDoesNotDependOn(
            Orchestration,
            AllExcept("Mps.Orchestration", "Mps.Domain", "Mps.Application", "Mps.Agents"));
    }

    [Fact]
    public void ApiIsTheCompositionRoot()
    {
        AssertDependsOn(Api, "Mps.Application", "Mps.Orchestration");
    }

    [Fact]
    public void MockEnterpriseDependsOnlyOnDomain()
    {
        AssertDependsOn(MockEnterprise, "Mps.Domain");
        AssertDoesNotDependOn(MockEnterprise, AllExcept("Mps.MockEnterprise", "Mps.Domain"));
    }

    [Fact]
    public void InnerLayersDoNotReferenceTheHost()
    {
        AssertDoesNotDependOn(Domain, "Mps.Api");
        AssertDoesNotDependOn(Application, "Mps.Api");
        AssertDoesNotDependOn(Simulator, "Mps.Api");
        AssertDoesNotDependOn(ModelGateway, "Mps.Api");
        AssertDoesNotDependOn(Agents, "Mps.Api");
        AssertDoesNotDependOn(Connectors, "Mps.Api");
        AssertDoesNotDependOn(Orchestration, "Mps.Api");
        AssertDoesNotDependOn(MockEnterprise, "Mps.Api");
    }

    private static string[] AllExcept(params string[] allowed)
    {
        return Layers.Where(layer => Array.IndexOf(allowed, layer) < 0).ToArray();
    }

    private static void AssertDependsOn(Assembly assembly, params string[] dependencies)
    {
        foreach (var dependency in dependencies)
        {
            var result = Types.InAssembly(assembly)
                .That()
                .HaveName(nameof(Mps.Domain.LayerAnchor))
                .Should()
                .HaveDependencyOn(dependency)
                .GetResult();

            AssertPasses(result, dependency);
        }
    }

    private static void AssertDoesNotDependOn(Assembly assembly, params string[] dependencies)
    {
        if (dependencies.Length == 0)
        {
            return;
        }

        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(dependencies)
            .GetResult();

        AssertPasses(result, string.Join(", ", dependencies));
    }

    private static void AssertPasses(TestResult result, string expectation)
    {
        var failing = result.FailingTypeNames is null
            ? string.Empty
            : string.Join(", ", result.FailingTypeNames);

        result.IsSuccessful.Should().BeTrue("rule '{0}' failed for: {1}", expectation, failing);
    }
}
