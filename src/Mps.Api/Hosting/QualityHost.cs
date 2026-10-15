// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

using Mps.Api.Bus;
using Mps.Api.Demo;
using Mps.Api.Fakes;
using Mps.Api.Http;
using Mps.Api.Hubs;
using Mps.Api.Live;
using Mps.Api.Persistence;
using Mps.Api.Printouts;
using Mps.Application.Bus;
using Mps.Application.Decisions;
using Mps.Application.Faults;
using Mps.Application.Intake;
using Mps.Application.Lifecycle;
using Mps.Application.Metadata;
using Mps.Application.Metrics;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Application.Printouts;
using Mps.Application.Spc;
using Mps.Application.Workflow;
using Mps.Application.Writes;
using Mps.Bus;
using Mps.Connectors.Printouts;
using Mps.Simulator.Scenarios;
using Mps.Simulator.Sites;

using Gateway = Mps.ModelGateway.Routing.ModelGateway;

namespace Mps.Api.Hosting;

public static class QualityHost
{
    public static void AddQualityApi(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var dataDirectory = DemoDataDirectory(builder.Environment.ContentRootPath, builder.Configuration["Mps:DataDirectory"]);
        var replayDirectory = builder.Configuration["Mps:Replay:Directory"];
        if (string.IsNullOrWhiteSpace(replayDirectory))
        {
            replayDirectory = Path.Combine(dataDirectory, "replay");
        }

        builder.Services.AddSingleton(new ReplayStore(replayDirectory));
        builder.Services.AddSingleton<ConsoleDataMode>();
        builder.Services.AddSingleton<ScenarioReplayRecorder>();
        var plantSeed = builder.Configuration.GetValue("Mps:Demo:Seed", SiteIds.DefaultSeed);
        builder.Services.AddSingleton(SitePlantGenerator.Load(dataDirectory, plantSeed));
        builder.Services.AddSingleton(new CrossSiteMaterialCatalog(Path.Combine(dataDirectory, "seed", "enterprise.json")));
        var connectionString = builder.Configuration.GetConnectionString("Mps") ?? "Data Source=mps.db";
        builder.Services.AddProblemDetails();
        builder.AddQualityTelemetry();
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddSignalR();
        builder.Services.ConfigureHttpJsonOptions(options => ApplyJson(options.SerializerOptions));
        builder.Services.Configure<Microsoft.AspNetCore.SignalR.JsonHubProtocolOptions>(options => ApplyJson(options.PayloadSerializerOptions));
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("console", policy =>
            {
                policy.WithOrigins("http://localhost:5173", "http://localhost:8080")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
        builder.Services.AddDbContext<QualityDbContext>(options => options.UseSqlite(connectionString));
        builder.Services.AddSingleton(new Mps.Api.Access.JoinLinkStore(connectionString));
        builder.Services.AddScoped<EfQualityStore>();
        builder.Services.AddScoped<IFleetCatalog, PlantFleetCatalog>();
        builder.Services.AddScoped<IAlertLog>(services => services.GetRequiredService<EfQualityStore>());
        builder.Services.AddScoped<IIncidentLog>(services => services.GetRequiredService<EfQualityStore>());
        builder.Services.AddScoped<ITicketLog>(services => services.GetRequiredService<EfQualityStore>());
        builder.Services.AddScoped<IInvestigationLog>(services => services.GetRequiredService<EfQualityStore>());
        builder.Services.AddScoped<ICapaLog>(services => services.GetRequiredService<EfQualityStore>());
        builder.Services.AddScoped<IFaultLog>(services => services.GetRequiredService<EfQualityStore>());
        builder.Services.AddScoped<IApprovalLog>(services => services.GetRequiredService<EfQualityStore>());
        builder.Services.AddScoped<IInvestigationNarrativeLog>(services => services.GetRequiredService<EfQualityStore>());
        builder.Services.AddScoped<ITicketingPort>(services => services.GetRequiredService<EfQualityStore>());
        builder.Services.AddSingleton(ApprovalPolicyLoader.Load(builder.Configuration));
        builder.Services.AddSingleton(new InjectorPolicy(builder.Configuration.GetValue("Mps:Injector:DirectTicket", false)));
        builder.Services.AddSingleton(new MetricsPolicy(
            builder.Configuration.GetValue("Metrics:CostPerInvestigationUsd", 0d),
            builder.Configuration.GetValue("Metrics:SpendCapUsd", 2d)));
        builder.Services.AddScoped<IMetricsLog, EfMetricsLog>();
        builder.Services.AddSingleton<IQmsCapaWriter, FakeQmsCapaWriter>();
        builder.Services.AddSingleton(TimeProvider.System);
        var simulation = SimulationOptions.From(builder.Configuration);
        if (simulation.RealTime)
        {
            Mps.Simulator.Telemetry.RecordDates.UseToday();
        }
        else
        {
            Mps.Simulator.Telemetry.RecordDates.UseReference();
        }

        builder.Services.AddSingleton(simulation);
        builder.Services.AddSingleton<PlantSimulation>();
        builder.Services.AddSingleton(services =>
        {
            var plant = services.GetRequiredService<PlantSimulation>();
            return new Mps.Application.RecordClock(() => plant.SimulatedNow);
        });
        builder.Services.AddSingleton(services => services.GetRequiredService<PlantSimulation>().Runtime);
        builder.Services.AddSingleton<StreamSpcPublisher>();
        builder.Services.AddSingleton<ScriptedScenarioContext>();
        builder.Services.AddSingleton(services => ModelGatewayRegistration.Create(
            services.GetRequiredService<IConfiguration>(),
            services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<ScriptedScenarioContext>()));
        builder.Services.AddSingleton<IChatClient>(services => services.GetRequiredService<Gateway>());
        builder.Services.AddSingleton<ResetProgress>();
        builder.Services.AddScoped<DemoResetService>();
        builder.Services.AddSingleton(ScenarioCardCatalog.Load(Path.Combine(dataDirectory, "scenarios", "catalog.yaml")));
        builder.Services.AddSingleton<ScenarioEvidenceBoard>();
        builder.Services.AddScoped<ScenarioTrigger>(services => ActivatorUtilities.CreateInstance<ScenarioTrigger>(services, (TimeProvider)services.GetRequiredService<Mps.Application.RecordClock>()));
        builder.Services.AddSingleton<IMetadataStore>(new YamlMetadataStore(Path.Combine(dataDirectory, "metadata")));
        builder.Services.AddSingleton<IBusinessRuleEngine>(services => new BusinessRuleEngine(services.GetRequiredService<IMetadataStore>()));
        builder.Services.AddScoped<IQualityNotifier, SignalRQualityNotifier>();
        builder.Services.AddScoped<IIntakeService>(services => ActivatorUtilities.CreateInstance<IntakeService>(services, (TimeProvider)services.GetRequiredService<Mps.Application.RecordClock>()));
        builder.Services.AddScoped(services =>
        {
            var minutes = services.GetRequiredService<IConfiguration>().GetValue("Spc:DebounceMinutes", 10);
            var debounce = TimeSpan.FromMinutes(minutes < 0 ? 0 : minutes);
            return new SpcAlertService(
                services.GetRequiredService<IAlertLog>(),
                services.GetRequiredService<IIntakeService>(),
                services.GetRequiredService<IQualityNotifier>(),
                debounce,
                services.GetRequiredService<IBusinessRuleEngine>(),
                services.GetRequiredService<ICascadeDirectory>(),
                services.GetRequiredService<ILifecycleEventSink>());
        });
        if (!builder.TryAddLiveInvestigation())
        {
            builder.Services.AddScoped<IInvestigationWorkflow>(services => ActivatorUtilities.CreateInstance<FakeInvestigationWorkflow>(services, (TimeProvider)services.GetRequiredService<Mps.Application.RecordClock>()));
        }

        builder.Services.AddScoped<IInvestigationService>(services => ActivatorUtilities.CreateInstance<InvestigationService>(services, (TimeProvider)services.GetRequiredService<Mps.Application.RecordClock>()));
        builder.Services.AddScoped<ICapaLifecycleService, CapaLifecycleService>();
        builder.Services.AddScoped<IHumanDecisionService, HumanDecisionService>();
        builder.Services.AddSingleton<IManualMeasurementLog, InMemoryManualMeasurementLog>();
        builder.Services.AddSingleton<IPrintoutTextExtractor, PdfTextExtractor>();
        builder.Services.AddSingleton<IPrintoutFieldParser, LabeledPrintoutParser>();
        builder.Services.AddSingleton<IPrintoutModelReader, PrintoutModelReader>();
        builder.Services.AddSingleton<ISimulationFaultPort, DemoSimulationFaultPort>();
        builder.Services.AddScoped<IDocumentIntake>(services => ActivatorUtilities.CreateInstance<DocumentIntakeService>(services, (TimeProvider)services.GetRequiredService<Mps.Application.RecordClock>()));
        builder.Services.AddScoped<IFaultInjectionService>(services => ActivatorUtilities.CreateInstance<FaultInjectionService>(services, (TimeProvider)services.GetRequiredService<Mps.Application.RecordClock>()));
        builder.Services.AddSingleton<IEventBus>(services =>
            new InProcessEventBus(time: services.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton(_ => new SqliteEventStore(connectionString));
        builder.Services.AddSingleton<CascadeRegistry>();
        builder.Services.AddSingleton<ICascadeDirectory>(services => services.GetRequiredService<CascadeRegistry>());
        builder.Services.AddSingleton<ILifecycleEventSink>(services => ActivatorUtilities.CreateInstance<BusEventSink>(services, (TimeProvider)services.GetRequiredService<Mps.Application.RecordClock>()));
        builder.Services.AddSingleton(CommandCatalog.Load(dataDirectory));
        builder.Services.AddScoped<CommandGateway>(services => ActivatorUtilities.CreateInstance<CommandGateway>(services, (TimeProvider)services.GetRequiredService<Mps.Application.RecordClock>()));
        builder.Services.AddSingleton<TelemetryPublisher>();
        builder.Services.AddSingleton<INominalHistorySeeder, NominalHistorySeeder>();
        builder.Services.AddSingleton<CausalTraceService>();
        builder.Services.AddHostedService<QualityDatabaseInitializer>();
        builder.Services.AddHostedService<EventLogSubscriber>();
        builder.Services.AddHostedService<PlantCommandSubscriber>();
        builder.Services.AddHostedService<BusSignalRBridge>();
        builder.Services.AddHostedService<SimulationTickService>();
    }

    public static void UseQualityApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseExceptionHandler();
        app.UseCors("console");

        // The Azure image copies web/dist here. Local `dotnet run` has no wwwroot, so the API stays API-only.
        if (HasWebRoot(app.Environment))
        {
            app.UseDefaultFiles();
            app.UseStaticFiles();
        }

        app.UseMiddleware<Mps.Api.Access.AccessGate>();
        QualityEndpoints.Map(app);
        Mps.Api.Access.AccessEndpoints.MapAccess(app);
        app.MapModelUsage();
        app.MapHub<FleetHub>("/hubs/fleet");
        if (HasWebRoot(app.Environment))
        {
            MapSpaFallback(app);
        }
    }

    private static bool HasWebRoot(IWebHostEnvironment environment)
    {
        var path = environment.WebRootPath;
        return !string.IsNullOrEmpty(path) && Directory.Exists(path);
    }

    private static void MapSpaFallback(WebApplication app)
    {
        app.MapFallback((HttpContext context) =>
        {
            var path = context.Request.Path;
            if (path.StartsWithSegments("/api")
                || path.StartsWithSegments("/hubs")
                || path.StartsWithSegments("/health")
                || Path.HasExtension(path.Value))
            {
                return Results.NotFound();
            }

            var root = app.Environment.WebRootPath;
            if (string.IsNullOrEmpty(root))
            {
                return Results.NotFound();
            }

            var index = Path.Combine(root, "index.html");
            return File.Exists(index)
                ? Results.File(index, "text/html; charset=utf-8")
                : Results.NotFound();
        });
    }

    private static string DemoDataDirectory(string contentRoot, string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
        {
            return configured;
        }

        foreach (var start in new[] { contentRoot, AppContext.BaseDirectory })
        {
            if (string.IsNullOrWhiteSpace(start))
            {
                continue;
            }

            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "data");
                if (File.Exists(Path.Combine(candidate, "seed", "enterprise.json")))
                {
                    return candidate;
                }
            }
        }

        throw new DirectoryNotFoundException("The data directory with seed/enterprise.json was not found.");
    }

    private static void ApplyJson(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }
}
