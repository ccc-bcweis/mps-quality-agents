// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Collections.Concurrent;

using Mps.Domain.Identity;
using Mps.Domain.Investigations;

namespace Mps.Application.Lifecycle;

/// <summary>
/// Investigations that are gathering evidence now. The record is only saved when gathering ends (which can take
/// minutes while the agent waits for enough readings), so until then this shows a "started" placeholder: the list,
/// the ticket link and a second Start all see it instead of "no investigation".
/// A demo reset cancels every investigation in flight (<see cref="CancelAll"/>): one waiting for readings would
/// otherwise wait out its whole budget, because the reset stops the plant that sends them.
/// </summary>
public sealed class InvestigationsInFlight : IDisposable
{
    private readonly ConcurrentDictionary<IncidentId, InvestigationSnapshot> _running = new();
    private readonly ConcurrentDictionary<InvestigationId, InvestigationId> _finished = new();
    private CancellationTokenSource _reset = new();

    /// <summary>Cancelled by the next reset. Investigations link their own token to it when they start.</summary>
    public CancellationToken ResetToken => Volatile.Read(ref _reset).Token;

    /// <summary>
    /// Cancels every investigation started before now, and their waits. Investigations started afterwards get a fresh token.
    /// The old source is not disposed: an investigation starting at this moment may still be reading its token.
    /// </summary>
    public void CancelAll()
    {
        var previous = Interlocked.Exchange(ref _reset, new CancellationTokenSource());
        previous.Cancel();
    }

    public void Dispose() => Volatile.Read(ref _reset).Dispose();

    /// <summary>Registers a placeholder; false when one is already running for the incident.</summary>
    public bool TryBegin(InvestigationSnapshot placeholder, out InvestigationSnapshot running)
    {
        ArgumentNullException.ThrowIfNull(placeholder);
        running = _running.GetOrAdd(placeholder.IncidentId, placeholder);
        return ReferenceEquals(running, placeholder);
    }

    /// <summary>Gathering ended: drop the placeholder and remember which saved record it became.</summary>
    public void End(InvestigationSnapshot placeholder, InvestigationId? saved)
    {
        ArgumentNullException.ThrowIfNull(placeholder);
        _running.TryRemove(new KeyValuePair<IncidentId, InvestigationSnapshot>(placeholder.IncidentId, placeholder));
        if (saved is { } id)
        {
            _finished[placeholder.Id] = id;
        }
    }

    public InvestigationSnapshot? ForIncident(IncidentId incidentId) => _running.TryGetValue(incidentId, out var running) ? running : null;

    public InvestigationSnapshot? ById(InvestigationId id) => _running.Values.FirstOrDefault(item => item.Id == id);

    /// <summary>The saved record a placeholder id became, if it has finished.</summary>
    public InvestigationId? SavedAs(InvestigationId placeholderId) => _finished.TryGetValue(placeholderId, out var id) ? id : null;

    public IReadOnlyList<InvestigationSnapshot> Running => _running.Values.ToArray();

    public void Clear()
    {
        _running.Clear();
        _finished.Clear();
    }
}
