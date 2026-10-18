// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Faults;

namespace Mps.Api.Demo;

/// <summary>
/// Enterprise records for faults that are running, keyed by the correlation the cascade stamps on each fault.
/// A lookup reads <see cref="CurrentCorrelation"/> (the investigation or incident on this async flow).
/// Reset clears every entry. Stopping one fault or one scenario removes only its entries.
/// </summary>
public sealed class ScenarioEvidenceBoard : IFaultEvidenceBoard
{
    private readonly object _gate = new();
    private readonly Dictionary<string, EvidenceRegistration> _byCorrelation = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _byScenario = new(StringComparer.OrdinalIgnoreCase);
    private readonly AsyncLocal<string?> _current = new();

    /// <summary>The correlation bound on this async flow, or null when the caller did not bind one.</summary>
    public string? CurrentCorrelation => string.IsNullOrWhiteSpace(_current.Value) ? null : _current.Value;

    /// <summary>Binds lookups on this async flow to one fault correlation. Dispose restores the previous binding.</summary>
    public IDisposable Use(string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        return new Scope(this, correlationId.Trim());
    }

    public EvidenceRegistration? Find(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return null;
        }

        lock (_gate)
        {
            return _byCorrelation.TryGetValue(correlationId.Trim(), out var registration) ? registration : null;
        }
    }

    public void RegisterScenario(string correlationId, string scenarioId, DateTimeOffset? activatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioId);
        lock (_gate)
        {
            var key = correlationId.Trim();
            var scenario = scenarioId.Trim();
            _byCorrelation.TryGetValue(key, out var existing);
            _byCorrelation[key] = new EvidenceRegistration(scenario, activatedAt ?? existing?.ActivatedAt, existing?.Records);
            Track(scenario, key, existing?.ScenarioId);
        }
    }

    public void RegisterRecords(string correlationId, FaultEvidenceAttachment records, DateTimeOffset? activatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentNullException.ThrowIfNull(records);
        if (records.IsEmpty)
        {
            return;
        }

        lock (_gate)
        {
            var key = correlationId.Trim();
            _byCorrelation.TryGetValue(key, out var existing);
            var merged = existing?.Records is { IsEmpty: false } prior ? FaultEvidenceAttachment.Merge(prior, records) : records;
            _byCorrelation[key] = new EvidenceRegistration(existing?.ScenarioId, activatedAt ?? existing?.ActivatedAt, merged);
        }
    }

    public void Remove(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return;
        }

        lock (_gate)
        {
            var key = correlationId.Trim();
            if (!_byCorrelation.Remove(key, out var registration))
            {
                return;
            }

            if (registration.ScenarioId is not null)
            {
                Untrack(registration.ScenarioId, key);
            }
        }
    }

    public void RemoveScenario(string scenarioId)
    {
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            return;
        }

        List<string> keys;
        lock (_gate)
        {
            if (!_byScenario.TryGetValue(scenarioId.Trim(), out var set) || set.Count == 0)
            {
                return;
            }

            keys = [.. set];
        }

        foreach (var key in keys)
        {
            Remove(key);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _byCorrelation.Clear();
            _byScenario.Clear();
        }
    }

    private void Track(string scenarioId, string correlationId, string? previousScenarioId)
    {
        if (previousScenarioId is not null && !string.Equals(previousScenarioId, scenarioId, StringComparison.OrdinalIgnoreCase))
        {
            Untrack(previousScenarioId, correlationId);
        }

        if (!_byScenario.TryGetValue(scenarioId, out var set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            _byScenario.Add(scenarioId, set);
        }

        set.Add(correlationId);
    }

    private void Untrack(string scenarioId, string correlationId)
    {
        if (!_byScenario.TryGetValue(scenarioId, out var set))
        {
            return;
        }

        set.Remove(correlationId);
        if (set.Count == 0)
        {
            _byScenario.Remove(scenarioId);
        }
    }

    private sealed class Scope : IDisposable
    {
        private readonly ScenarioEvidenceBoard _board;
        private readonly string? _previous;
        private bool _disposed;

        public Scope(ScenarioEvidenceBoard board, string correlationId)
        {
            _board = board;
            _previous = board._current.Value;
            board._current.Value = correlationId;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _board._current.Value = _previous;
        }
    }
}

/// <summary>What one correlation serves: a scenario overlay, inline records, or both.</summary>
public sealed class EvidenceRegistration
{
    public EvidenceRegistration(string? scenarioId, DateTimeOffset? activatedAt, FaultEvidenceAttachment? records)
    {
        ScenarioId = string.IsNullOrWhiteSpace(scenarioId) ? null : scenarioId.Trim();
        ActivatedAt = activatedAt;
        Records = records is { IsEmpty: true } ? null : records;
    }

    public string? ScenarioId { get; }

    /// <summary>Plant time the records were staged. Scenario rows keep their spacing from their anchor, shifted to here.</summary>
    public DateTimeOffset? ActivatedAt { get; }

    public FaultEvidenceAttachment? Records { get; }
}
