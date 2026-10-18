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
        var plantSeed = Mps.Simulator.Demo.DemoDefaults.PlantSeedFrom(builder.Configuration);
        builder.Services.AddSingleton(SitePlantGenerator.Load(dataDirectory, plantSeed));
        builder.Services.AddSingleton(new CrossSiteMaterialCatalog(Path.Combine(dataDirectory, "seed", "enterprise.json")));
        // One home for the default: appsettings.json (ConnectionStrings:Mps); the image and Bicep override it.
        var connectionString = builder.Configuration.GetConnectionString("Mps")
            ?? throw new InvalidOperationException("ConnectionStrings:Mps is not set. appsettings.json ships the local default.");
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
        var configuration = builder.Configuration;
        var environment = builder.Environment;
        builder.Services.AddSingleton(services => ApprovalPolicyLoader.Load(
            configuration,
            environment,
            services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ApprovalPolicyLoader).FullName!)));
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
        builder.Services.AddSingleton<Mps.Application.Faults.ConfounderBoard>();
        builder.Services.AddScoped<ScenarioTrigger>(services => ActivatorUtilities.CreateInstance<ScenarioTrigger>(services, (TimeProvider)services.GetRequiredService<Mps.Application.RecordClock>()));
        builder.Services.AddSingleton<IMetadataStore>(new YamlMetadataStore(Path.Combine(dataDirectory, "metadata")));
        builder.Services.AddSingleton<IBusinessRuleEngine>(services => new BusinessRuleEngine(services.GetRequiredService<IMetadataStore>()));
        builder.Services.AddScoped<IQualityNotifier, SignalRQualityNotifier>();
        builder.Services.AddScoped<IIntakeService>(services => ActivatorUtilities.CreateInstance<IntakeService>(services, (TimeProvider)services.GetRequiredService<Mps.Application.RecordClock>()));
        builder.Services.AddScoped(services =>
        {
            var minutes = services.GetRequiredService<IConfiguration>().GetValue<double?>("Spc:DebounceMinutes") ?? SpcRuleSettings.Shared.AlertDebounceMinutes;
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

        builder.Services.AddSingleton<Mps.Application.Lifecycle.InvestigationsInFlight>();
        builder.Services.AddScoped<IInvestigationService>(services => ActivatorUtilities.CreateInstance<InvestigationService>(services, (TimeProvider)services.GetRequiredService<Mps.Application.RecordClock>()));
        builder.Services.AddSingleton<Mps.Application.Lifecycle.IPlantFaultControl>(services => services.GetRequiredService<PlantSimulation>());
        builder.Services.AddScoped<ICapaLifecycleService>(services => ActivatorUtilities.CreateInstance<CapaLifecycleService>(services, (TimeProvider)services.GetRequiredService<Mps.Application.RecordClock>()));
        builder.Services.AddScoped<IHumanDecisionService, HumanDecisionService>();
        builder.Services.AddSingleton<IManualMeasurementLog, InMemoryManualMeasurementLog>();
        builder.Services.AddSingleton<Mps.Api.Printouts.PrintoutFileStore>();
        builder.Services.AddSingleton<IManualSampleSink, Mps.Api.Printouts.PlantManualSampleSink>();
        builder.Services.AddSingleton<IPrintoutTextExtractor, PdfTextExtractor>();
        builder.Services.AddSingleton<IPrintoutFieldParser, LabeledPrintoutParser>();
        builder.Services.AddSingleton<IPrintoutModelReader, PrintoutModelReader>();
        builder.Services.AddSingleton<ISimulationFaultPort, DemoSimulationFaultPort>();
        builder.Services.AddScoped<IDocumentIntake>(services => ActivatorUtilities.CreateInstance<DocumentIntakeService>(services, (TimeProvider)services.GetRequiredService<Mps.Application.RecordClock>()));
        builder.Services.AddScoped<IFaultInjectionService>(services => ActivatorUtilities.CreateInstance<FaultInjectionService>(services, (TimeProvider)services.GetRequiredService<Mps.Application.RecordClock>()));
        builder.Services.AddSingleton<IEventBus>(services =>
            new InProcessEventBus(time: services.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton(_ => new SqliteEventStore(connectionString) { TelemetryRowsPerSeries = Mps.Simulator.SimulationSettings.Shared.TelemetryWindow.ReadingsPerSensor });
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
        builder.Services.AddSingleton<LiveSubscriptions>();
        builder.Services.AddSingleton<LiveStreamPublisher>();
        builder.Services.AddHostedService(services => services.GetRequiredService<LiveStreamPublisher>());
        builder.Services.AddHostedService<SimulationTickService>();
    }

    public static void UseQualityApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Resolve the approval key now: outside Development a missing key stops startup instead of the first approval.
        _ = app.Services.GetRequiredService<Mps.Application.Decisions.ApprovalPolicy>();

        // Sign-in setup: Entra mode with no admin stops startup; local mode warns when it is not private to this machine.
        var authLog = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(Mps.Api.Access.AuthStartup).FullName!);
        Mps.Api.Access.AuthStartup.Validate(app.Configuration, app.Environment, authLog);
        app.Lifetime.ApplicationStarted.Register(() => Mps.Api.Access.AuthStartup.WarnIfExposed(
            app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>(), app.Configuration, authLog));
        app.UseExceptionHandler();
        app.UseCors("console");

        // The Azure image copies web/dist here. Local `dotnet run` has no wwwroot, so the API stays API-only.
        if (HasWebRoot(app.Environment))
        {
            app.UseMiddleware<Mps.Api.Access.ConsoleEntryGate>();
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
