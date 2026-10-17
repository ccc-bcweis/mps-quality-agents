// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Agents.Reasoning;
using Mps.Api.Demo;
using Mps.ModelGateway.Cost;
using Mps.ModelGateway.Replay;
using Mps.ModelGateway.Routing;

using Gateway = Mps.ModelGateway.Routing.ModelGateway;

namespace Mps.Api.Hosting;

/// <summary>
/// Builds the pinned gateway from <c>Mps:Model</c>. Live calls use temperature 0 and the configured seed.
/// </summary>
public static class ModelGatewayRegistration
{
    public static Gateway Create(IConfiguration configuration, TimeProvider time, IScenarioHint scenarioHint)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(scenarioHint);
        var demoSeed = Mps.Simulator.Demo.DemoDefaults.PlantSeedFrom(configuration);
        var options = new ModelGatewayOptions
        {
            Mode = Route(configuration["LLM_MODE"]),
            DailySpendCapUsd = (decimal)configuration.GetValue("Metrics:SpendCapUsd", 2d),
            ScriptedReplay = configuration.GetValue("Mps:Model:ScriptedReplay", false),
            Temperature = configuration.GetValue("Mps:Model:Temperature", 0f),
            Seed = configuration.GetValue("Mps:Model:Seed", (long)demoSeed),
        };
        var scripted = ScriptedReplayFiles.Load(DemoRuntimeLoader.FindReplayDirectory());
        return new Gateway(
            options,
            new InMemoryCostLedger(),
            new InMemoryReplayCatalog(),
            new QualityMockReasoner(),
            time,
            scriptedReplay: scripted,
            scenarioHint: scenarioHint);
    }

    private static ModelRoute Route(string? mode)
    {
        return mode?.Trim().ToLowerInvariant() switch
        {
            "live" or "azure" or "azureopenai" => ModelRoute.AzureOpenAI,
            "openai" => ModelRoute.OpenAI,
            "mock" or "mockreasoner" => ModelRoute.MockReasoner,
            _ => ModelRoute.Replay,
        };
    }
}
