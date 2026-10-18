// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Azure.Identity;

using Microsoft.Extensions.AI;

using Mps.Agents.Explain;
using Mps.Agents.Investigation;
using Mps.Agents.Prompts;
using Mps.Agents.Reasoning;
using Mps.Agents.Resolution;
using Mps.Agents.Tools;
using Mps.Application.Workflow;
using Mps.Connectors.Docs;
using Mps.ModelGateway.Cost;
using Mps.ModelGateway.Replay;
using Mps.ModelGateway.Routing;
using Mps.Orchestration;

namespace Mps.Api.Live;

/// <summary>
/// Composes the real agent workflow for the configured model mode (<see cref="ModelModeSettings"/>): offline (the
/// deterministic reasoner the replay eval scores, no network), OpenAI (API key) or Azure OpenAI (Azure identity or key).
/// Every model call goes through the gateway, which enforces the daily dollar cap, times out live calls, and falls back
/// to saved answers when the live model fails. The guards run in every mode.
/// </summary>
public static class LiveModelHost
{
    /// <summary>
    /// Adds the investigation workflow. Returns false only for the test-only placeholder mode, which the caller wires.
    /// </summary>
    public static bool TryAddLiveInvestigation(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var config = builder.Configuration;
        var settings = ModelModeSettings.Read(config);
        builder.Services.AddSingleton(settings);
        if (settings.Mode == ModelMode.Placeholder)
        {
            return false;
        }

        var dataDirectory = DataDirectory(builder.Environment.ContentRootPath, config["Mps:DataDirectory"]);
        var services = builder.Services;
        services.AddSingleton<ICostLedger, InMemoryCostLedger>();
        services.AddSingleton(services1 =>
        {
            var options = new ModelGatewayOptions
            {
                Mode = settings.Mode switch
                {
                    ModelMode.Azure => ModelRoute.AzureOpenAI,
                    ModelMode.OpenAI => ModelRoute.OpenAI,
                    _ => ModelRoute.MockReasoner,
                },
                DailySpendCapUsd = Required<decimal>(config, "Mps:Model:DailyCapUsd"),
                InputUsdPerMillionTokens = Required<decimal>(config, "Mps:Model:InputUsdPerMillionTokens"),
                OutputUsdPerMillionTokens = Required<decimal>(config, "Mps:Model:OutputUsdPerMillionTokens"),
                LiveRetryCount = 0,
                CircuitBreakerFailures = Required<int>(config, "Mps:Model:CircuitBreakerFailures"),
                CircuitCooldown = TimeSpan.FromSeconds(Required<double>(config, "Mps:Model:CircuitCooldownSeconds")),
                ScriptedReplay = config.GetValue("Mps:Model:ScriptedReplay", false),
                Seed = config.GetValue("Mps:Model:Seed", (long)Mps.Simulator.Demo.DemoDefaults.PlantSeedFrom(config)),

                // gpt-5.x mini rejects a temperature; set Mps:Model:Temperature only for models that accept it.
                Temperature = config.GetValue<float?>("Mps:Model:Temperature"),
            };
            var live = ModelClientFactory.Create(settings, services1.GetRequiredService<ILoggerFactory>().CreateLogger("Mps.Live.Model"));
            return new ModelGateway.Routing.ModelGateway(
                options,
                services1.GetRequiredService<ICostLedger>(),
                new InMemoryReplayCatalog(),
                new QualityMockReasoner(),
                services1.GetRequiredService<TimeProvider>(),
                azureClient: settings.Mode == ModelMode.Azure ? live : null,
                openAiClient: settings.Mode == ModelMode.OpenAI ? live : null,
                scriptedReplay: Mps.ModelGateway.Replay.ScriptedReplayFiles.Load(Mps.Api.Demo.DemoRuntimeLoader.FindReplayDirectory()),
                scenarioHint: services1.GetRequiredService<Mps.Api.Demo.ScriptedScenarioContext>());
        });
        services.AddSingleton(_ => WhyItMattersCatalog.Load(dataDirectory));
        services.AddSingleton(provider => new SeedEnterpriseTools(
            Path.Combine(dataDirectory, "seed", "enterprise.json"),
            Path.Combine(dataDirectory, "scenarios", "evidence.json"),
            provider.GetService<Mps.Api.Demo.ScenarioEvidenceBoard>()).WithConfounders(provider.GetService<Mps.Application.Faults.ConfounderBoard>()));
        services.AddSingleton<DemoSignalTools>();
        services.AddSingleton(_ => new HybridDocumentSearch(Path.Combine(dataDirectory, "index", "embeddings.json")));
        services.AddSingleton<InvestigationSessionStore>();
        services.AddSingleton(provider =>
        {
            var enterprise = provider.GetRequiredService<SeedEnterpriseTools>();
            var signals = provider.GetRequiredService<DemoSignalTools>();
            var gateway = provider.GetRequiredService<ModelGateway.Routing.ModelGateway>();
            var prompts = PromptCatalog.Load();
            var tools = new ReadOnlyToolSession(
                signals,
                signals,
                enterprise,
                enterprise,
                enterprise,
                enterprise,
                enterprise,
                provider.GetRequiredService<HybridDocumentSearch>(),
                signals);
            // The same key HumanDecisionService signs with (ApprovalPolicyLoader), so tokens minted there are accepted here.
            var key = provider.GetRequiredService<Mps.Application.Decisions.ApprovalPolicy>().SigningKey;
            return new MafInvestigationWorkflow(
                new StoreIncidentSource(provider.GetRequiredService<IServiceScopeFactory>()),
                new Mps.Api.Demo.CorrelationBoundInvestigationAgent(
                    new InvestigationAgent(
                        tools,
                        gateway,
                        prompts,
                        provider.GetRequiredService<TimeProvider>(),
                        measurements: provider.GetRequiredService<Mps.Application.Printouts.IManualMeasurementLog>(),
                        // Waiting for readings only makes sense when the plant streams in real time; a stepped plant never sends more on its own.
                        shapeWait: SimulationOptions.From(config) is { RealTime: true, Enabled: true }
                            ? Mps.Agents.Investigation.ShapeWait.Load()
                            : Mps.Agents.Investigation.ShapeWait.None),
                    provider.GetRequiredService<Mps.Api.Demo.ScenarioEvidenceBoard>()),
                new ResolutionAgent(gateway, prompts),
                key,
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<InvestigationSessionStore>());
        });
        services.AddScoped<IInvestigationWorkflow, LiveInvestigationWorkflow>();
        return true;
    }

    /// <summary>A model setting from configuration. appsettings.json ships every one, so a missing value is a broken install.</summary>
    private static T Required<T>(IConfiguration config, string key) where T : struct =>
        config.GetValue<T?>(key) ?? throw new InvalidOperationException(key + " is not set. appsettings.json ships the default.");

    private static string DataDirectory(string contentRoot, string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
        {
            return configured;
        }

        for (var dir = new DirectoryInfo(contentRoot); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, "seed", "enterprise.json")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("The data directory with seed/enterprise.json was not found.");
    }

    /// <summary>
    /// GET /api/model/status: the model mode actually in use (what the console banner shows) and whether live calls are
    /// failing over to saved answers. GET /api/model/usage: today's model calls by route from the gateway ledger.
    /// </summary>
    public static void MapModelUsage(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/api/model/status", (ModelModeSettings settings, IServiceProvider services) =>
            Results.Ok(ModelStatusResponse.From(settings, services.GetService<ModelGateway.Routing.ModelGateway>()?.Status)));
        app.MapGet("/api/model/usage", async (ModelModeSettings settings, IServiceProvider services, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var ledger = services.GetService<ICostLedger>();
            if (ledger is null)
            {
                return Results.Ok(new { mode = settings.Name, spendTodayUsd = 0m, calls = Array.Empty<object>() });
            }

            var day = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
            var entries = await ledger.GetEntriesAsync(day, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new
            {
                mode = settings.Name,
                spendTodayUsd = await ledger.GetSpendAsync(day, cancellationToken).ConfigureAwait(false),
                byRoute = entries.GroupBy(entry => entry.Route).Select(group => new
                {
                    route = group.Key,
                    calls = group.Count(),
                    inputTokens = group.Sum(entry => entry.InputTokens),
                    outputTokens = group.Sum(entry => entry.OutputTokens),
                    costUsd = group.Sum(entry => entry.CostUsd),
                    models = group.Select(entry => entry.Model).Distinct().ToArray(),
                }),
                lastCalls = entries.OrderByDescending(entry => entry.OccurredAt).Take(10).Select(entry => new
                {
                    entry.Route,
                    entry.Model,
                    entry.InputTokens,
                    entry.OutputTokens,
                    entry.CostUsd,
                    entry.OccurredAt,
                    entry.LiveCallRefused,
                }),
            });
        });
    }
}
