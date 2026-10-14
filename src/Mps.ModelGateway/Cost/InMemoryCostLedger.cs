// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.ModelGateway.Cost;

/// <summary>
/// Process-local ledger used by tests and the local demo.
/// </summary>
public sealed class InMemoryCostLedger : ICostLedger
{
    private readonly object _gate = new();

    private readonly List<CostEntry> _entries = [];

    public Task<decimal> GetSpendAsync(DateOnly day, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            decimal sum = 0m;
            foreach (var entry in _entries)
            {
                if (DateOnly.FromDateTime(entry.OccurredAt.UtcDateTime) == day)
                {
                    sum += entry.CostUsd;
                }
            }

            return Task.FromResult(sum);
        }
    }

    public Task RecordAsync(CostEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _entries.Add(entry);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CostEntry>> GetEntriesAsync(DateOnly day, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var matches = _entries
                .Where(entry => DateOnly.FromDateTime(entry.OccurredAt.UtcDateTime) == day)
                .ToArray();
            return Task.FromResult<IReadOnlyList<CostEntry>>(matches);
        }
    }
}
