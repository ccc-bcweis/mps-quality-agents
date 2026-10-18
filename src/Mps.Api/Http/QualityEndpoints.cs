// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Mps.Api.Auth;
using Mps.Api.Persistence;
using Mps.Application.Decisions;
using Mps.Application.Lifecycle;
using Mps.Application.Persistence;
using Mps.Domain.Identity;

namespace Mps.Api.Http;

public static partial class QualityEndpoints
{
    private const int ReplayCaptureTicks = 90;

    private static readonly JsonSerializerOptions ResetJson = new(JsonSerializerDefaults.Web);

    public static void Map(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/health", () => Results.Ok(new HealthResponse("ok", LayerAnchor.Name)));
        MetadataEndpoints.Map(app);
        MapSitesAndFleet(app);
        MapRecords(app);
        MapCapas(app);
        MapDemoControls(app);
        MapPrintoutsAndManualReadings(app);
        MapInjector(app);
        MapTraceAuditAndMetrics(app);
    }

    private static bool MatchesSite(string? requested, string actual) =>
        string.IsNullOrWhiteSpace(requested)
        || string.Equals(requested.Trim(), "all", StringComparison.OrdinalIgnoreCase)
        || string.Equals(requested.Trim(), actual, StringComparison.OrdinalIgnoreCase);

    private static async Task<string?> ToolForIncidentAsync(IIncidentLog incidents, IncidentId incidentId, CancellationToken cancellationToken)
    {
        var stored = await incidents.GetAsync(incidentId, cancellationToken).ConfigureAwait(false);
        return stored?.Incident.ToolId.ToString();
    }

    private static async Task<string> SiteForIncidentAsync(IIncidentLog incidents, IncidentId incidentId, CancellationToken cancellationToken)
    {
        var stored = await incidents.GetAsync(incidentId, cancellationToken).ConfigureAwait(false);
        return ContractMapper.SiteForTool(stored?.Incident.ToolId.ToString());
    }

    private static async Task<CapaResponse> WithApprovalAsync(QualityDbContext db, CapaResponse capa, CancellationToken cancellationToken)
    {
        var approval = await db.Approvals.AsNoTracking()
            .Where(row => row.TokenId == capa.ApprovalTokenId)
            .OrderByDescending(row => row.IssuedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return ContractMapper.WithApproval(capa, approval);
    }

    private static DemoCaller Caller(HttpContext http, IConfiguration configuration)
    {
        var header = http.Request.Headers[EasyAuthPrincipal.HeaderName].ToString();
        var caller = EasyAuthPrincipal.Read(string.IsNullOrWhiteSpace(header) ? null : header, configuration["Mps:Auth:Mode"]);
        return !caller.IsAdmin && Mps.Api.Access.AccessOptions.From(configuration).IsAdminEmail(Mps.Api.Access.CallerIdentity.Read(header).Email)
            ? caller with { IsAdmin = true }
            : caller;
    }

    /// <summary>Top-1 category accuracy from the CI replay eval (eval/results/latest.json), shipped with the build.</summary>
    private static readonly Lazy<double?> EvalAccuracy = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "eval", "latest.json");
        if (!File.Exists(path))
        {
            return null;
        }

        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.TryGetProperty("categoryAccuracy", out var value) && value.ValueKind == System.Text.Json.JsonValueKind.Number
            ? value.GetDouble()
            : null;
    });

    private static async Task<double> LiveSpendAsync(IServiceProvider services, TimeProvider time, double fallback, CancellationToken cancellationToken)
    {
        var ledger = services.GetService<Mps.ModelGateway.Cost.ICostLedger>();
        if (ledger is null)
        {
            return fallback;
        }

        var day = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        return (double)await ledger.GetSpendAsync(day, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>How long the decision call waits for the next round before answering with its placeholder.</summary>
    private static readonly TimeSpan RoundAnswerWait = TimeSpan.FromSeconds(2);

    private static readonly Action<ILogger, Guid, Exception?> RoundNotStarted = LoggerMessage.Define<Guid>(
        LogLevel.Warning,
        new EventId(4101, "EvidenceRoundNotStarted"),
        "The next round did not start for incident {IncidentId}.");

    /// <summary>
    /// Starts the next round in its own scope so it outlives the request, then answers with the saved round when it
    /// finishes quickly, or with the "started" placeholder the console polls until the round is saved.
    /// </summary>
    private static async Task<Mps.Domain.Investigations.InvestigationSnapshot> NextRoundAsync(
        IServiceScopeFactory scopes,
        Mps.Application.Lifecycle.InvestigationsInFlight inFlight,
        HumanDecisionOutcome outcome,
        CancellationToken cancellationToken)
    {
        var incidentId = outcome.Investigation.IncidentId;
        var userEvidence = outcome.UserEvidence;
        var run = Task.Run(
            async () =>
            {
                await using var scope = scopes.CreateAsyncScope();
                try
                {
                    var service = scope.ServiceProvider.GetRequiredService<IInvestigationService>();
                    return await service.StartRoundAsync(incidentId, userEvidence, CancellationToken.None).ConfigureAwait(false);
                }
#pragma warning disable CA1031 // The round runs after the response; a failure is logged and the ticket keeps its closed round.
                catch (Exception exception)
#pragma warning restore CA1031
                {
                    if (scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("Mps.Api.EvidenceLoop") is { } logger)
                    {
                        RoundNotStarted(logger, incidentId.Value, exception);
                    }

                    return null;
                }
            },
            CancellationToken.None);
        var deadline = DateTimeOffset.UtcNow + RoundAnswerWait;
        while (!run.IsCompleted && DateTimeOffset.UtcNow < deadline)
        {
            if (inFlight.ForIncident(incidentId) is { } running && running.Round > outcome.Investigation.Round)
            {
                return running;
            }

            await Task.WhenAny(run, Task.Delay(50, cancellationToken)).ConfigureAwait(false);
        }

        if (run.IsCompleted)
        {
            return await run.ConfigureAwait(false) ?? outcome.Investigation;
        }

        return inFlight.ForIncident(incidentId) ?? outcome.Investigation;
    }
}

public sealed record AdminSessionResponse(string Mode, bool IsAdmin, string DisplayName, bool Member = true);

public sealed record ResetBody(string? Kind);

public sealed record DataModeBody(string? Mode, string? ScenarioId);

public sealed record SpeedBody(int Speed, int StrideSeconds);
