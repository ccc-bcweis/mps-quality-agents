// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Auth;
using Mps.Application.Bus;

namespace Mps.Api.Bus;

/// <summary>
/// Publishes a command, then runs the existing service while that command is the ambient parent.
/// </summary>
public sealed class CommandGateway
{
    private readonly CommandCatalog _catalog;
    private readonly ILifecycleEventSink _sink;
    private readonly ICascadeDirectory _cascade;
    private readonly TimeProvider _time;

    public CommandGateway(CommandCatalog catalog, ILifecycleEventSink sink, ICascadeDirectory cascade, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(cascade);
        ArgumentNullException.ThrowIfNull(time);
        _catalog = catalog;
        _sink = sink;
        _cascade = cascade;
        _time = time;
    }

    public ICascadeDirectory Cascade => _cascade;

    public void Ensure(string commandName, DemoCaller caller) => _catalog.EnsureMayIssue(commandName, caller);

    public async Task<T> RunAsync<T>(
        string type,
        string correlationId,
        string source,
        string? toolId,
        string? chamberId,
        string? parentEventId,
        object payload,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        var eventId = await _sink.PublishAsync(
            new LifecycleEvent(
                type,
                correlationId,
                parentEventId,
                parentEventId,
                source,
                null,
                toolId,
                chamberId,
                _time.GetUtcNow(),
                payload),
            cancellationToken).ConfigureAwait(false);
        using (CommandAmbient.Push(correlationId, eventId))
        {
            return await action(cancellationToken).ConfigureAwait(false);
        }
    }
}
