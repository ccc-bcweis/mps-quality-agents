// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.EntityFrameworkCore;

using Mps.Api.Fleet;

namespace Mps.Api.Persistence;

public sealed class QualityDatabaseInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopes;

    public QualityDatabaseInitializer(IServiceScopeFactory scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        _scopes = scopes;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QualityDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        if (await db.Tools.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        foreach (var tool in DemoFleet.Create())
        {
            db.Tools.Add(new ToolRow
            {
                Id = tool.Id.ToString(),
                Name = tool.Name,
                Family = tool.Family.ToString(),
                Model = tool.Model,
                Bay = tool.Bay,
                SiteId = tool.SiteId ?? "northwater",
                Chambers = tool.Chambers.Select(chamber => new ChamberRow
                {
                    ToolId = tool.Id.ToString(),
                    ChamberId = chamber.Id,
                    Name = chamber.Name,
                }).ToList(),
            });
        }

        db.TicketSequences.Add(new TicketSequenceRow { Id = 1, NextNumber = 1000 });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
