// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.EntityFrameworkCore;

using Mps.Api.Fakes;
using Mps.Api.Http;
using Mps.Api.Hubs;
using Mps.Api.Persistence;
using Mps.Application.Decisions;
using Mps.Application.Faults;
using Mps.Application.Intake;
using Mps.Application.Lifecycle;
using Mps.Application.Metrics;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Application.Spc;
using Mps.Application.Workflow;
using Mps.Application.Writes;

namespace Mps.Api.Hosting;

public static class QualityHost
{
    public static void AddQualityApi(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
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
        builder.Services.AddScoped<EfQualityStore>();
        builder.Services.AddScoped<IFleetCatalog>(services => services.GetRequiredService<EfQualityStore>());
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
        builder.Services.AddScoped<IQualityNotifier, SignalRQualityNotifier>();
        builder.Services.AddScoped<IIntakeService, IntakeService>();
        builder.Services.AddScoped(services =>
        {
            var minutes = services.GetRequiredService<IConfiguration>().GetValue("Spc:DebounceMinutes", 10);
            var debounce = TimeSpan.FromMinutes(minutes < 0 ? 0 : minutes);
            return new SpcAlertService(
                services.GetRequiredService<IAlertLog>(),
                services.GetRequiredService<IIntakeService>(),
                services.GetRequiredService<IQualityNotifier>(),
                debounce);
        });
        builder.Services.AddScoped<IInvestigationWorkflow, FakeInvestigationWorkflow>();
        builder.Services.AddScoped<IInvestigationService, InvestigationService>();
        builder.Services.AddScoped<ICapaLifecycleService, CapaLifecycleService>();
        builder.Services.AddScoped<IHumanDecisionService, HumanDecisionService>();
        builder.Services.AddScoped<IFaultInjectionService, FaultInjectionService>();
        builder.Services.AddHostedService<QualityDatabaseInitializer>();
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

        QualityEndpoints.Map(app);
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

    private static void ApplyJson(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }
}
