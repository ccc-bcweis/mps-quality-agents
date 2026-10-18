// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Bus;
using Mps.Api.Persistence;
using Mps.Application.Errors;
using Mps.Application.Lifecycle;
using Mps.Application.Persistence;
using Mps.Domain.Events;
using Mps.Domain.Identity;

namespace Mps.Api.Http;

public static partial class QualityEndpoints
{
    /// <summary>CAPA list, detail and stage advance.</summary>
    private static void MapCapas(WebApplication app)
    {
        app.MapGet("/api/capas", async (string? siteId, ICapaLifecycleService capas, IIncidentLog incidents, QualityDbContext db, CancellationToken cancellationToken) =>
        {
            var rows = await capas.ListAsync(cancellationToken).ConfigureAwait(false);
            var mapped = new List<CapaResponse>();
            foreach (var row in rows)
            {
                var site = await SiteForIncidentAsync(incidents, row.Capa.IncidentId, cancellationToken).ConfigureAwait(false);
                if (MatchesSite(siteId, site))
                {
                    mapped.Add(await WithApprovalAsync(db, ContractMapper.ToResponse(row.Capa, site, await ToolForIncidentAsync(incidents, row.Capa.IncidentId, cancellationToken).ConfigureAwait(false)), cancellationToken).ConfigureAwait(false));
                }
            }

            return Results.Ok(mapped);
        });
        app.MapGet("/api/capas/{capaId:guid}", async (Guid capaId, ICapaLifecycleService capas, IIncidentLog incidents, QualityDbContext db, CancellationToken cancellationToken) =>
        {
            var stored = await capas.GetAsync(new CapaId(capaId), cancellationToken).ConfigureAwait(false);
            var site = await SiteForIncidentAsync(incidents, stored.Capa.IncidentId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(await WithApprovalAsync(db, ContractMapper.ToResponse(stored.Capa, site, await ToolForIncidentAsync(incidents, stored.Capa.IncidentId, cancellationToken).ConfigureAwait(false)), cancellationToken).ConfigureAwait(false));
        });
        app.MapPost("/api/capas/{capaId:guid}/advance", async (
            Guid capaId,
            AdvanceCapaBody? body,
            ICapaLifecycleService capas,
            IIncidentLog incidents,
            QualityDbContext db,
            CommandGateway commands,
            HttpContext http,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            if (body?.Stage is null)
            {
                throw new IntakeValidationException("CAPA stage is required.");
            }

            commands.Ensure(EventTypes.CapaAdvance, Caller(http, configuration));
            var current = await capas.GetAsync(new CapaId(capaId), cancellationToken).ConfigureAwait(false);
            var incident = await incidents.GetAsync(current.Capa.IncidentId, cancellationToken).ConfigureAwait(false);
            var correlation = incident?.Incident.CorrelationId ?? "capa-" + capaId.ToString("N");
            var toolId = incident?.Incident.ToolId.ToString();
            var parent = commands.Cascade.FindStep(correlation, "capa") ?? commands.Cascade.FindStep(correlation, "approval");
            var stored = await commands.RunAsync(
                EventTypes.CapaAdvance,
                correlation,
                "quality",
                toolId,
                incident?.Incident.ChamberId,
                parent,
                new { capaId, stage = body.Stage.Value.ToString() },
                token => capas.AdvanceAsync(new CapaId(capaId), body.Stage.Value, token),
                cancellationToken).ConfigureAwait(false);
            var site = await SiteForIncidentAsync(incidents, stored.Capa.IncidentId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(await WithApprovalAsync(db, ContractMapper.ToResponse(stored.Capa, site, await ToolForIncidentAsync(incidents, stored.Capa.IncidentId, cancellationToken).ConfigureAwait(false)), cancellationToken).ConfigureAwait(false));
        });
    }
}
