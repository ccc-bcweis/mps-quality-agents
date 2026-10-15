// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.EntityFrameworkCore;

using Mps.Api.Bus;
using Mps.Api.Fleet;
using Mps.Api.Http;
using Mps.Api.Live;
using Mps.Api.Persistence;
using Mps.Application.Bus;
using Mps.Application.Persistence;
using Mps.Application.Printouts;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;
using Mps.Domain.Lifecycle;
using Mps.Simulator.Sites;
using Mps.Simulator.Telemetry;

namespace Mps.Api.Demo;

/// <summary>
/// Hard reset deletes transactional rows and refills a week of healthy telemetry.
/// Soft reset closes open work, clears live faults, and keeps the history.
/// Plant metadata lives outside this database, so neither reset reads or deletes it.
/// </summary>
public sealed class DemoResetService
{
    private const int ChunkSize = 2000;

    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly QualityDbContext _db;
    private readonly PlantSimulation _plant;
    private readonly StreamSpcPublisher _traces;
    private readonly SqliteEventStore _events;
    private readonly ICascadeDirectory _cascade;
    private readonly ILifecycleEventSink _sink;
    private readonly TimeProvider _time;
    private readonly INominalHistorySeeder _seeder;
    private readonly IIncidentLog _incidents;
    private readonly IInvestigationLog _investigations;
    private readonly ScenarioEvidenceBoard _board;
    private readonly IManualMeasurementLog _measurements;
    private readonly ScriptedScenarioContext _context;
    private readonly ResetProgress _progress;

    public DemoResetService(
        QualityDbContext db,
        PlantSimulation plant,
        StreamSpcPublisher traces,
        SqliteEventStore events,
        ICascadeDirectory cascade,
        ILifecycleEventSink sink,
        TimeProvider time,
        INominalHistorySeeder seeder,
        IIncidentLog incidents,
        IInvestigationLog investigations,
        ScenarioEvidenceBoard board,
        IManualMeasurementLog measurements,
        ScriptedScenarioContext context,
        ResetProgress progress)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(plant);
        ArgumentNullException.ThrowIfNull(traces);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(cascade);
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(seeder);
        ArgumentNullException.ThrowIfNull(incidents);
        ArgumentNullException.ThrowIfNull(investigations);
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(measurements);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(progress);
        _db = db;
        _plant = plant;
        _traces = traces;
        _events = events;
        _cascade = cascade;
        _sink = sink;
        _time = time;
        _seeder = seeder;
        _incidents = incidents;
        _investigations = investigations;
        _board = board;
        _measurements = measurements;
        _context = context;
        _progress = progress;
    }

    public Task<DemoBaseline> ResetAsync(CancellationToken cancellationToken) =>
        ResetAsync(hard: true, cancellationToken);

    public async Task<DemoBaseline> ResetAsync(bool hard, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var kind = hard ? "hard" : "soft";
        var succeeded = false;
        try
        {
            _progress.Begin(kind);
            var baseline = hard
                ? await HardAsync(cancellationToken).ConfigureAwait(false)
                : await SoftAsync(cancellationToken).ConfigureAwait(false);
            _progress.Complete(kind);
            succeeded = true;
            return baseline;
        }
        finally
        {
            if (!succeeded)
            {
                _progress.Fail("Reset failed.");
            }

            Gate.Release();
        }
    }

    private async Task<DemoBaseline> HardAsync(CancellationToken cancellationToken)
    {
        _progress.Report("purging", "Clearing tickets, alerts, and recorded history.");
        ClearOverlays();
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
        _events.Clear();

        _progress.Report("rebuilding", "Rebuilding the fleet.");
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
        var published = await PublishResetAsync(LifecycleTypes.ResetHard, cancellationToken).ConfigureAwait(false);
        _plant.Reset();
        _progress.Report("seeding", "Filling a week of healthy history.");
        await _seeder.SeedAsync(NominalWindow.LastWeek, cancellationToken).ConfigureAwait(false);
        return await BaselineAsync(published.EventId, "hard", cancellationToken).ConfigureAwait(false);
    }

    private async Task<DemoBaseline> SoftAsync(CancellationToken cancellationToken)
    {
        _progress.Report("closing", "Closing open tickets and investigations.");
        var published = await PublishResetAsync(LifecycleTypes.ResetSoft, cancellationToken).ConfigureAwait(false);
        await CloseOpenWorkAsync(published.EventId, published.CorrelationId, cancellationToken).ConfigureAwait(false);
        ClearOverlays();
        _plant.Reset();
        _progress.Report("nominal", "Sending a fresh healthy reading for every sensor.");
        await PublishNominalPulseAsync(published.EventId, published.CorrelationId, cancellationToken).ConfigureAwait(false);
        return await BaselineAsync(published.EventId, "soft", cancellationToken).ConfigureAwait(false);
    }

    private void ClearOverlays()
    {
        _board.Clear();
        _measurements.Clear();
        _context.Set(null);
        _traces.Clear();
        _cascade.Clear();
    }

    private async Task CloseOpenWorkAsync(string resetEventId, string correlationId, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var incidents = await _incidents.ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var stored in incidents)
        {
            if (stored.Incident.Stage is IncidentStage.Closed or IncidentStage.ClosedByDemoReset)
            {
                continue;
            }

            stored.Incident.CloseByDemoReset();
            await _incidents.SaveAsync(stored, now, cancellationToken).ConfigureAwait(false);
            var toolId = stored.Incident.ToolId.ToString();
            await _sink.PublishAsync(
                new LifecycleEvent(
                    LifecycleTypes.IncidentChanged,
                    correlationId,
                    resetEventId,
                    resetEventId,
                    "presenter",
                    ContractMapper.SiteForTool(toolId),
                    toolId,
                    stored.Incident.ChamberId,
                    now,
                    new
                    {
                        status = DemoReset.StatusLabel,
                        stage = "closedByDemoReset",
                        incidentId = stored.Incident.Id.Value,
                    }),
                cancellationToken).ConfigureAwait(false);
        }

        var investigations = await _investigations.ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var snapshot in investigations)
        {
            if (!InvestigationLifecycle.CanCloseByDemoReset(snapshot.Stage))
            {
                continue;
            }

            var closed = InvestigationLifecycle.CloseByDemoReset(snapshot);
            await _investigations.UpdateAsync(closed, cancellationToken).ConfigureAwait(false);
            await _sink.PublishAsync(
                new LifecycleEvent(
                    LifecycleTypes.InvestigationChanged,
                    correlationId,
                    resetEventId,
                    resetEventId,
                    "presenter",
                    null,
                    null,
                    null,
                    now,
                    new
                    {
                        status = DemoReset.StatusLabel,
                        stage = "closedByDemoReset",
                        investigationId = closed.Id.Value,
                        incidentId = closed.IncidentId.Value,
                    }),
                cancellationToken).ConfigureAwait(false);
        }

        var pending = await _db.Approvals
            .Where(row => row.Outcome == ApprovalOutcomes.Writing
                || (row.Outcome != ApprovalOutcomes.Succeeded
                    && row.Outcome != ApprovalOutcomes.ClosedByDemoReset
                    && !row.Consumed))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var row in pending)
        {
            row.Outcome = ApprovalOutcomes.ClosedByDemoReset;
            row.Decision = DemoReset.StatusLabel;
            row.Consumed = true;
            if (string.IsNullOrWhiteSpace(row.Comment))
            {
                row.Comment = DemoReset.StatusLabel;
            }
        }

        if (pending.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PublishNominalPulseAsync(string resetEventId, string correlationId, CancellationToken cancellationToken)
    {
        var now = _plant.SimulatedNow;
        var chunk = new List<LifecycleEvent>(ChunkSize);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var site in _plant.World.Sites)
        {
            foreach (var tool in site.Tools)
            {
                foreach (var sensor in tool.Spec.Sensors)
                {
                    var parameter = string.IsNullOrWhiteSpace(sensor.Parameter) ? sensor.Id : sensor.Parameter;
                    if (!seen.Add(tool.Id + "|" + parameter) || sensor.Limits.Maximum <= sensor.Limits.Minimum)
                    {
                        continue;
                    }

                    chunk.Add(new LifecycleEvent(
                        LifecycleTypes.TelemetryReading,
                        correlationId,
                        resetEventId,
                        resetEventId,
                        "reset",
                        tool.SiteId,
                        tool.Id,
                        sensor.FrameChamberId(tool.Id),
                        now,
                        new
                        {
                            parameter,
                            value = sensor.Limits.Nominal,
                            unit = sensor.Unit,
                            low = sensor.Limits.Minimum,
                            high = sensor.Limits.Maximum,
                        }));
                    if (chunk.Count >= ChunkSize)
                    {
                        await _sink.PublishManyAsync(chunk, cancellationToken).ConfigureAwait(false);
                        chunk.Clear();
                    }
                }
            }
        }

        if (chunk.Count > 0)
        {
            await _sink.PublishManyAsync(chunk, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<(string EventId, string CorrelationId)> PublishResetAsync(string type, CancellationToken cancellationToken)
    {
        var correlationId = "reset-" + Guid.NewGuid().ToString("N");
        var eventId = await _sink.PublishAsync(
            new LifecycleEvent(
                type,
                correlationId,
                null,
                null,
                "presenter",
                null,
                null,
                null,
                _time.GetUtcNow(),
                new { kind = type == LifecycleTypes.ResetHard ? "hard" : "soft" }),
            cancellationToken).ConfigureAwait(false);
        return (eventId, correlationId);
    }

    private async Task<DemoBaseline> BaselineAsync(string resetEventId, string kind, CancellationToken cancellationToken) =>
        new(
            _plant.Runtime.Seed,
            DemoClock.BaselineOrigin,
            await _db.Tools.CountAsync(cancellationToken).ConfigureAwait(false),
            await _db.Tickets.CountAsync(cancellationToken).ConfigureAwait(false),
            await _db.Investigations.CountAsync(cancellationToken).ConfigureAwait(false),
            await _db.Approvals.CountAsync(cancellationToken).ConfigureAwait(false),
            await _db.Capas.CountAsync(cancellationToken).ConfigureAwait(false),
            _events.CountTelemetry(),
            _events.CountLog(),
            resetEventId,
            kind);
}

public sealed record DemoBaseline(
    int Seed,
    DateTimeOffset SimulatedOrigin,
    int FleetToolCount,
    int TicketCount,
    int InvestigationCount,
    int ApprovalCount,
    int CapaCount,
    long TelemetryCount,
    long EventCount,
    string ResetEventId,
    string Kind);
