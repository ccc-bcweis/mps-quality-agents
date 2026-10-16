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
/// Composes the real agent workflow when Mps:Model:Mode is Live and an Azure OpenAI endpoint is set.
/// Every model call goes through the gateway, which enforces the daily dollar cap and falls back to the labeled mock.
/// </summary>
public static class LiveModelHost
{
    public static bool TryAddLiveInvestigation(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var config = builder.Configuration;
        var mode = config["Mps:Model:Mode"];
        var endpoint = config["Mps:Model:Endpoint"] ?? config["AZURE_OPENAI_ENDPOINT"];
        var deployment = config["Mps:Model:Deployment"] ?? config["AZURE_OPENAI_DEPLOYMENT"];
        if (!string.Equals(mode, "Live", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(endpoint)
            || string.IsNullOrWhiteSpace(deployment))
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
                Mode = ModelRoute.AzureOpenAI,
                DailySpendCapUsd = config.GetValue("Mps:Model:DailyCapUsd", 2m),
                InputUsdPerMillionTokens = config.GetValue("Mps:Model:InputUsdPerMillionTokens", 0.75m),
                OutputUsdPerMillionTokens = config.GetValue("Mps:Model:OutputUsdPerMillionTokens", 4.50m),
                LiveRetryCount = 0,
                ScriptedReplay = config.GetValue("Mps:Model:ScriptedReplay", false),
                Seed = config.GetValue("Mps:Model:Seed", (long)config.GetValue("Mps:Demo:Seed", 4242)),

                // gpt-5.x mini rejects a temperature; set Mps:Model:Temperature only for models that accept it.
                Temperature = config.GetValue<float?>("Mps:Model:Temperature"),
            };
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ManagedIdentityClientId = config["AZURE_CLIENT_ID"],
            });
            var azure = new LiveFailureTranslatingChatClient(
                ManagedIdentityChatClient.Create(new Uri(endpoint), deployment, credential),
                services1.GetRequiredService<ILoggerFactory>().CreateLogger("Mps.Live.Model"));
            return new ModelGateway.Routing.ModelGateway(
                options,
                services1.GetRequiredService<ICostLedger>(),
                new InMemoryReplayCatalog(),
                new QualityMockReasoner(),
                services1.GetRequiredService<TimeProvider>(),
                azure,
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
            var key = System.Text.Encoding.UTF8.GetBytes(config["Mps:Approval:SigningKey"] ?? Guid.NewGuid().ToString("N"));
            return new MafInvestigationWorkflow(
                new StoreIncidentSource(provider.GetRequiredService<IServiceScopeFactory>()),
                new InvestigationAgent(
                    tools,
                    gateway,
                    prompts,
                    provider.GetRequiredService<TimeProvider>(),
                    measurements: provider.GetRequiredService<Mps.Application.Printouts.IManualMeasurementLog>(),
                    shapeWait: Mps.Agents.Investigation.ShapeWait.Load()),
                new ResolutionAgent(gateway, prompts),
                key,
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<InvestigationSessionStore>());
        });
        services.AddScoped<IInvestigationWorkflow, LiveInvestigationWorkflow>();
        return true;
    }

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
    /// Today's model calls by route, from the gateway ledger. Route "azure-openai" means the live model answered.
    /// </summary>
    public static void MapModelUsage(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/api/model/usage", async (IServiceProvider services, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var ledger = services.GetService<ICostLedger>();
            if (ledger is null)
            {
                return Results.Ok(new { mode = "stand-in", spendTodayUsd = 0m, calls = Array.Empty<object>() });
            }

            var day = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
            var entries = await ledger.GetEntriesAsync(day, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new
            {
                mode = "live",
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
