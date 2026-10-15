// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.EntityFrameworkCore;

using Mps.Api.Fleet;
using Mps.Api.Persistence;
using Mps.Simulator.Demo;
using Mps.Simulator.Telemetry;

namespace Mps.Api.Demo;

/// <summary>
/// Puts tickets, investigations, approvals, CAPAs, and the simulated fleet back on the seeded baseline.
/// </summary>
public sealed class DemoResetService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly QualityDbContext _db;
    private readonly DemoRuntime _runtime;

    public DemoResetService(QualityDbContext db, DemoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(runtime);
        _db = db;
        _runtime = runtime;
    }

    public async Task<DemoBaseline> ResetAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _db.Approvals.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await _db.Capas.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await _db.Investigations.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await _db.Tickets.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await _db.Incidents.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await _db.Alerts.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await _db.Faults.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await _db.MetricSamples.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await _db.Chambers.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await _db.Tools.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await _db.TicketSequences.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            _db.ChangeTracker.Clear();

            foreach (var tool in DemoFleet.Create())
            {
                _db.Tools.Add(new ToolRow
                {
                    Id = tool.Id.ToString(),
                    Name = tool.Name,
                    Family = tool.Family.ToString(),
                    Model = tool.Model,
                    Bay = tool.Bay,
                    Chambers = tool.Chambers.Select(chamber => new ChamberRow
                    {
                        ToolId = tool.Id.ToString(),
                        ChamberId = chamber.Id,
                        Name = chamber.Name,
                    }).ToList(),
                });
            }

            _db.TicketSequences.Add(new TicketSequenceRow { Id = 1, NextNumber = 1000 });
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _runtime.Reset();

            return new DemoBaseline(
                _runtime.Seed,
                DemoClock.BaselineOrigin,
                await _db.Tools.CountAsync(cancellationToken).ConfigureAwait(false),
                await _db.Tickets.CountAsync(cancellationToken).ConfigureAwait(false),
                await _db.Investigations.CountAsync(cancellationToken).ConfigureAwait(false),
                await _db.Approvals.CountAsync(cancellationToken).ConfigureAwait(false),
                await _db.Capas.CountAsync(cancellationToken).ConfigureAwait(false));
        }
        finally
        {
            Gate.Release();
        }
    }
}

public sealed record DemoBaseline(
    int Seed,
    DateTimeOffset SimulatedOrigin,
    int FleetToolCount,
    int TicketCount,
    int InvestigationCount,
    int ApprovalCount,
    int CapaCount);
