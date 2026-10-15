// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Data;
using System.Globalization;

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
        if (!await TableExistsAsync(db, "Investigations", cancellationToken).ConfigureAwait(false))
        {
            // The file already had tables, so EnsureCreated skipped the quality schema.
            // Rebuild it. The event log is created later, on first use.
            await db.Database.EnsureDeletedAsync(cancellationToken).ConfigureAwait(false);
            await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        }

        await EnsureInvestigationSequenceAsync(db, cancellationToken).ConfigureAwait(false);
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

    private static async Task<bool> TableExistsAsync(QualityDbContext db, string name, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "$name";
            parameter.Value = name;
            command.Parameters.Add(parameter);
            var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt64(scalar, CultureInfo.InvariantCulture) > 0;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// EnsureCreated does not add columns to a database from an earlier run.
    /// Sequence records start order so the latest investigation is stable.
    /// </summary>
    private static async Task EnsureInvestigationSequenceAsync(QualityDbContext db, CancellationToken cancellationToken)
    {
        var table = db.Model.FindEntityType(typeof(InvestigationRow))?.GetTableName();
        if (!string.Equals(table, "Investigations", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected the Investigations table, found '" + table + "'.");
        }

        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await using var check = connection.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Investigations') WHERE name = 'Sequence';";
            var scalar = await check.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (Convert.ToInt64(scalar, CultureInfo.InvariantCulture) > 0)
            {
                return;
            }

            await using var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE \"Investigations\" ADD COLUMN \"Sequence\" INTEGER NOT NULL DEFAULT 0;";
            await alter.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            await using var backfill = connection.CreateCommand();
            backfill.CommandText = "UPDATE \"Investigations\" SET \"Sequence\" = rowid WHERE \"Sequence\" = 0;";
            await backfill.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }
}
