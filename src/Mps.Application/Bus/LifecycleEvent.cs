// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Application.Bus;

/// <summary>
/// Dotted names shared by commands and the facts that follow them.
/// The bus project owns the same spellings. This list keeps the application free of that reference.
/// </summary>
public static class LifecycleTypes
{
    public const string TelemetryReading = "telemetry.reading";

    public const string AlertRaised = "alert.raised";

    public const string TicketCreated = "ticket.created";

    public const string IncidentChanged = "incident.changed";

    public const string InvestigationStep = "investigation.step";

    public const string InvestigationChanged = "investigation.changed";

    public const string ApprovalGranted = "approval.granted";

    public const string CapaCreated = "capa.created";

    public const string CapaAdvanced = "capa.advanced";

    public const string DocumentUploaded = "document.uploaded";

    public const string FaultInject = "command.fault.inject";

    public const string ScenarioStart = "command.scenario.start";

    public const string ResetHard = "command.reset.hard";

    public const string ResetSoft = "command.reset.soft";

    public const string OperatorReport = "command.operator.report";

    public const string DocumentUpload = "command.document.upload";

    public const string ApprovalGrant = "command.approval.grant";

    public const string CapaAdvance = "command.capa.advance";

    public const string InvestigationStart = "command.investigation.start";
}

/// <summary>
/// A fact the application wants on the bus. The host fills the event id, sequence, and recorded time.
/// </summary>
public sealed record LifecycleEvent(
    string Type,
    string CorrelationId,
    string? CausationId,
    string? ParentEventId,
    string Source,
    string? SiteId,
    string? ToolId,
    string? ChamberId,
    DateTimeOffset OccurredAt,
    object? Payload);

/// <summary>
/// Port the application uses to emit. The host adapts it to the in-process bus.
/// </summary>
public interface ILifecycleEventSink
{
    ValueTask<string> PublishAsync(LifecycleEvent draft, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<string>> PublishManyAsync(
        IReadOnlyList<LifecycleEvent> drafts,
        CancellationToken cancellationToken = default);
}

/// <summary>Used by unit tests that construct a service without a host.</summary>
public sealed class NullLifecycleEventSink : ILifecycleEventSink
{
    public static NullLifecycleEventSink Instance { get; } = new();

    public ValueTask<string> PublishAsync(LifecycleEvent draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        _ = cancellationToken;
        return ValueTask.FromResult(string.Empty);
    }

    public ValueTask<IReadOnlyList<string>> PublishManyAsync(
        IReadOnlyList<LifecycleEvent> drafts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        _ = cancellationToken;
        return ValueTask.FromResult<IReadOnlyList<string>>([]);
    }
}
