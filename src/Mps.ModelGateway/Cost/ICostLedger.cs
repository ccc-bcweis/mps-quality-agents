// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.ModelGateway.Cost;

/// <summary>
/// Daily token and dollar ledger. The hosted demo can replace the in-memory store with SQL.
/// </summary>
public interface ICostLedger
{
    Task<decimal> GetSpendAsync(DateOnly day, CancellationToken cancellationToken);

    Task RecordAsync(CostEntry entry, CancellationToken cancellationToken);

    Task<IReadOnlyList<CostEntry>> GetEntriesAsync(DateOnly day, CancellationToken cancellationToken);
}
