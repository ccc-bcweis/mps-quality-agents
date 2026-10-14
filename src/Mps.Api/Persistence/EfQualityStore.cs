// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Mps.Application.Lifecycle;
using Mps.Application.Persistence;
using Mps.Application.Writes;
using Mps.Domain.Capa;
using Mps.Domain.Equipment;
using Mps.Domain.Evidence;
using Mps.Domain.Hypotheses;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;
using Mps.Domain.Investigations;

namespace Mps.Api.Persistence;

public sealed class EfQualityStore :
    IFleetCatalog,
    IAlertLog,
    IIncidentLog,
    ITicketLog,
    IInvestigationLog,
    ICapaLog,
    IFaultLog,
    ITicketingPort
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly QualityDbContext _db;

    public EfQualityStore(QualityDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<IReadOnlyList<Tool>> ListToolsAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.Tools.AsNoTracking().Include(row => row.Chambers).OrderBy(row => row.Id).ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToTool).ToArray();
    }

    Task<IReadOnlyList<Tool>> IFleetCatalog.ListAsync(CancellationToken cancellationToken) =>
        ListToolsAsync(cancellationToken);

    public async Task<Tool?> FindAsync(ToolId toolId, CancellationToken cancellationToken)
    {
        var row = await _db.Tools.AsNoTracking().Include(item => item.Chambers).SingleOrDefaultAsync(item => item.Id == toolId.ToString(), cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ToTool(row);
    }

    public async Task AddAsync(Alert alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);
        _db.Alerts.Add(new AlertRow
        {
            Id = alert.Id.Value,
            ToolId = alert.ToolId.ToString(),
            ChamberId = alert.ChamberId,
            Parameter = alert.Parameter,
            Severity = alert.Severity.ToString(),
            Rule = alert.Rule,
            RaisedAt = ToUtc(alert.RaisedAt),
            CorrelationId = alert.CorrelationId,
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Alert>> ListAsync(ToolId? toolId, CancellationToken cancellationToken)
    {
        var query = _db.Alerts.AsNoTracking().AsQueryable();
        if (toolId is not null)
        {
            var id = toolId.Value.ToString();
            query = query.Where(row => row.ToolId == id);
        }

        var rows = await query.OrderByDescending(row => row.RaisedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(ToAlert).ToArray();
    }

    public async Task<IReadOnlyList<Alert>> ListBySignalAsync(
        ToolId toolId,
        string parameter,
        string? chamberId,
        CancellationToken cancellationToken)
    {
        var id = toolId.ToString();
        var normalizedChamber = string.IsNullOrWhiteSpace(chamberId) ? null : chamberId.Trim();
        var rows = await _db.Alerts.AsNoTracking()
            .Where(row => row.ToolId == id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows
            .Where(row => string.Equals(row.Parameter, parameter, StringComparison.OrdinalIgnoreCase)
                && string.Equals(row.ChamberId, normalizedChamber, StringComparison.OrdinalIgnoreCase))
            .Select(ToAlert)
            .ToArray();
    }

    public async Task AddAsync(StoredIncident incident, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incident);
        var current = incident.Incident;
        _db.Incidents.Add(new IncidentRow
        {
            Id = current.Id.Value,
            ToolId = current.ToolId.ToString(),
            ChamberId = current.ChamberId,
            Parameter = current.Parameter,
            ParameterKey = ParameterKey(current.Parameter),
            Channel = current.Channel.ToString(),
            Severity = current.Severity.ToString(),
            Summary = current.Summary,
            CorrelationId = current.CorrelationId,
            OpenedAt = ToUtc(current.OpenedAt),
            Stage = current.Stage.ToString(),
            EscalationRequired = incident.EscalationRequired,
            HasEscalated = incident.HasEscalated,
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<StoredIncident?> FindOpenAsync(ToolId toolId, string parameter, CancellationToken cancellationToken)
    {
        var key = ParameterKey(parameter);
        var id = toolId.ToString();
        var row = await _db.Incidents.AsNoTracking()
            .Where(item => item.ToolId == id && item.ParameterKey == key && item.Stage != nameof(IncidentStage.Closed))
            .OrderBy(item => item.OpenedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ToIncident(row);
    }

    public async Task<StoredIncident?> GetAsync(IncidentId id, CancellationToken cancellationToken)
    {
        var row = await _db.Incidents.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ToIncident(row);
    }

    public async Task<IReadOnlyList<StoredIncident>> ListIncidentsAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.Incidents.AsNoTracking().OrderByDescending(row => row.OpenedAt).ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToIncident).ToArray();
    }

    Task<IReadOnlyList<StoredIncident>> IIncidentLog.ListAsync(CancellationToken cancellationToken) =>
        ListIncidentsAsync(cancellationToken);

    public async Task SaveAsync(StoredIncident incident, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incident);
        _ = at;
        var row = await _db.Incidents.SingleAsync(item => item.Id == incident.Incident.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        row.Stage = incident.Incident.Stage.ToString();
        row.HasEscalated = incident.HasEscalated;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Ticket> OpenAsync(Incident incident, string title, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incident);
        var number = await NextTicketNumberAsync(cancellationToken).ConfigureAwait(false);
        var ticket = new Ticket(TicketId.New(), number, incident.Id, incident.ToolId, title, incident.OpenedAt);
        _db.Tickets.Add(new TicketRow
        {
            Id = ticket.Id.Value,
            Number = ticket.Number,
            IncidentId = ticket.IncidentId.Value,
            ToolId = ticket.ToolId.ToString(),
            Title = ticket.Title,
            OpenedAt = ToUtc(ticket.OpenedAt),
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ticket;
    }

    public async Task<IReadOnlyList<Ticket>> ListTicketsAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.Tickets.AsNoTracking().OrderByDescending(row => row.OpenedAt).ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToTicket).ToArray();
    }

    Task<IReadOnlyList<Ticket>> ITicketLog.ListAsync(CancellationToken cancellationToken) =>
        ListTicketsAsync(cancellationToken);

    public async Task AddAsync(InvestigationSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _db.Investigations.Add(ToInvestigationRow(snapshot));
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(InvestigationSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var row = await _db.Investigations.SingleAsync(item => item.Id == snapshot.Id.Value, cancellationToken).ConfigureAwait(false);
        row.Stage = snapshot.Stage.ToString();
        row.EscalationReason = snapshot.EscalationReason;
        row.CorrelationId = snapshot.CorrelationId;
        row.EvidenceJson = JsonSerializer.Serialize(snapshot.Evidence.Select(ToEvidenceDocument), JsonOptions);
        row.HypothesesJson = JsonSerializer.Serialize(snapshot.Hypotheses.Select(ToHypothesisDocument), JsonOptions);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<InvestigationSnapshot?> GetAsync(InvestigationId id, CancellationToken cancellationToken)
    {
        var row = await _db.Investigations.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ToSnapshot(row);
    }

    public async Task<InvestigationSnapshot?> FindActiveByIncidentAsync(IncidentId incidentId, CancellationToken cancellationToken)
    {
        var rejected = nameof(InvestigationStage.Rejected);
        var failed = nameof(InvestigationStage.Failed);
        var row = await _db.Investigations.AsNoTracking()
            .Where(item => item.IncidentId == incidentId.Value && item.Stage != rejected && item.Stage != failed)
            .OrderByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ToSnapshot(row);
    }

    public async Task AddAsync(CapaAction capa, string externalId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capa);
        _db.Capas.Add(new CapaRow
        {
            Id = capa.Id.Value,
            IncidentId = capa.IncidentId.Value,
            Summary = capa.Summary,
            ApprovalTokenId = capa.ApprovalTokenId.Value,
            ActionHash = capa.ActionHash,
            Stage = capa.Stage.ToString(),
            OpenedAt = ToUtc(capa.OpenedAt),
            ExternalId = externalId,
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(CapaAction capa, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capa);
        var row = await _db.Capas.SingleAsync(item => item.Id == capa.Id.Value, cancellationToken).ConfigureAwait(false);
        row.Stage = capa.Stage.ToString();
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<StoredCapa?> GetAsync(CapaId id, CancellationToken cancellationToken)
    {
        var row = await _db.Capas.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ToCapa(row);
    }

    public async Task<StoredCapa?> FindByIncidentAsync(IncidentId incidentId, CancellationToken cancellationToken)
    {
        var row = await _db.Capas.AsNoTracking()
            .Where(item => item.IncidentId == incidentId.Value)
            .OrderBy(item => item.OpenedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ToCapa(row);
    }

    public async Task<IReadOnlyList<StoredCapa>> ListCapasAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.Capas.AsNoTracking().OrderByDescending(row => row.OpenedAt).ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToCapa).ToArray();
    }

    Task<IReadOnlyList<StoredCapa>> ICapaLog.ListAsync(CancellationToken cancellationToken) =>
        ListCapasAsync(cancellationToken);

    public async Task AddAsync(FaultRecord fault, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fault);
        _db.Faults.Add(new FaultRow
        {
            Id = fault.Id,
            ToolId = fault.ToolId,
            ChamberId = fault.ChamberId,
            Parameter = fault.Parameter,
            Shape = fault.Shape,
            Magnitude = fault.Magnitude,
            RootCauseCategory = fault.RootCauseCategory,
            Confounders = fault.Confounders,
            CorrelationId = fault.CorrelationId,
            AcceptedAt = ToUtc(fault.AcceptedAt),
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> NextTicketNumberAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var sequence = await _db.TicketSequences.SingleAsync(cancellationToken).ConfigureAwait(false);
        sequence.NextNumber++;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return "TKT-" + sequence.NextNumber.ToString(CultureInfo.InvariantCulture);
    }

    private static string ParameterKey(string parameter) => parameter.Trim().ToUpperInvariant();

    private static DateTime ToUtc(DateTimeOffset value) => value.UtcDateTime;

    private static DateTimeOffset FromUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static Tool ToTool(ToolRow row) =>
        new(
            new ToolId(row.Id),
            row.Name,
            Enum.Parse<ToolFamily>(row.Family),
            row.Model,
            row.Bay,
            row.Chambers.OrderBy(chamber => chamber.ChamberId).Select(chamber => new Chamber(chamber.ChamberId, chamber.Name)).ToArray());

    private static Alert ToAlert(AlertRow row) =>
        new(
            new AlertId(row.Id),
            new ToolId(row.ToolId),
            row.ChamberId,
            row.Parameter,
            Enum.Parse<Severity>(row.Severity),
            row.Rule,
            FromUtc(row.RaisedAt),
            row.CorrelationId);

    private static StoredIncident ToIncident(IncidentRow row)
    {
        var incident = IncidentRestorer.Restore(
            new IncidentId(row.Id),
            new ToolId(row.ToolId),
            row.ChamberId,
            row.Parameter,
            Enum.Parse<IncidentChannel>(row.Channel),
            Enum.Parse<Severity>(row.Severity),
            row.Summary,
            row.CorrelationId,
            FromUtc(row.OpenedAt),
            Enum.Parse<IncidentStage>(row.Stage));
        var stored = new StoredIncident(incident, row.EscalationRequired, row.HasEscalated);
        if (row.HasEscalated)
        {
            stored.MarkEscalated();
        }

        return stored;
    }

    private static Ticket ToTicket(TicketRow row) =>
        new(new TicketId(row.Id), row.Number, new IncidentId(row.IncidentId), new ToolId(row.ToolId), row.Title, FromUtc(row.OpenedAt));

    private static InvestigationRow ToInvestigationRow(InvestigationSnapshot snapshot) =>
        new()
        {
            Id = snapshot.Id.Value,
            IncidentId = snapshot.IncidentId.Value,
            Stage = snapshot.Stage.ToString(),
            EscalationReason = snapshot.EscalationReason,
            CorrelationId = snapshot.CorrelationId,
            EvidenceJson = JsonSerializer.Serialize(snapshot.Evidence.Select(ToEvidenceDocument), JsonOptions),
            HypothesesJson = JsonSerializer.Serialize(snapshot.Hypotheses.Select(ToHypothesisDocument), JsonOptions),
        };

    private static InvestigationSnapshot ToSnapshot(InvestigationRow row)
    {
        var evidence = JsonSerializer.Deserialize<EvidenceDocument[]>(row.EvidenceJson, JsonOptions) ?? [];
        var hypotheses = JsonSerializer.Deserialize<HypothesisDocument[]>(row.HypothesesJson, JsonOptions) ?? [];
        return new InvestigationSnapshot(
            new InvestigationId(row.Id),
            new IncidentId(row.IncidentId),
            Enum.Parse<InvestigationStage>(row.Stage),
            evidence.Select(ToEvidence).ToArray(),
            hypotheses.Select(ToHypothesis).ToArray(),
            row.EscalationReason,
            row.CorrelationId);
    }

    private static EvidenceDocument ToEvidenceDocument(EvidenceItem item) =>
        new(item.Id.Value, item.Source.ToString(), item.Title, item.Excerpt, item.ObservedAt, item.ToolId, item.ExternalRef);

    private static EvidenceItem ToEvidence(EvidenceDocument document) =>
        new(
            new EvidenceId(document.Id),
            Enum.Parse<EvidenceSource>(document.Source),
            document.Title,
            document.Excerpt,
            document.ObservedAt,
            document.ToolId,
            document.ExternalRef);

    private static HypothesisDocument ToHypothesisDocument(Hypothesis hypothesis) =>
        new(
            hypothesis.Id.Value,
            hypothesis.Category.ToString(),
            hypothesis.Cause,
            hypothesis.Confidence,
            hypothesis.EvidenceIds.Select(id => id.Value).ToArray(),
            hypothesis.CounterEvidence.ToArray(),
            hypothesis.Actions.ToArray());

    private static Hypothesis ToHypothesis(HypothesisDocument document) =>
        new(
            new HypothesisId(document.Id),
            Enum.Parse<RootCauseCategory>(document.Category),
            document.Cause,
            document.Confidence,
            document.EvidenceIds.Select(id => new EvidenceId(id)).ToArray(),
            document.CounterEvidence,
            document.Actions);

    private static StoredCapa ToCapa(CapaRow row) =>
        new(
            CapaAction.Rehydrate(
                new CapaId(row.Id),
                new IncidentId(row.IncidentId),
                row.Summary,
                new ApprovalTokenId(row.ApprovalTokenId),
                row.ActionHash,
                Enum.Parse<CapaStage>(row.Stage),
                FromUtc(row.OpenedAt)),
            row.ExternalId);

    private sealed record EvidenceDocument(
        Guid Id,
        string Source,
        string Title,
        string Excerpt,
        DateTimeOffset ObservedAt,
        string? ToolId,
        string? ExternalRef);

    private sealed record HypothesisDocument(
        Guid Id,
        string Category,
        string Cause,
        double Confidence,
        Guid[] EvidenceIds,
        string[] CounterEvidence,
        string[] Actions);
}
