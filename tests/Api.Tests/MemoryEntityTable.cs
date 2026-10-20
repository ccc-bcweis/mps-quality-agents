// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Azure.Data.Tables;

using Mps.Api.Access;

namespace Mps.Api.Tests;

/// <summary>Azurite-free table. Two hosts can share one instance, so a restart test keeps the rows.</summary>
public sealed class MemoryEntityTable : IEntityTable
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, TableEntity> _rows = new(StringComparer.Ordinal);

    public Task UpsertAsync(TableEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _rows[Key(entity.PartitionKey, entity.RowKey)] = Copy(entity);
        }

        return Task.CompletedTask;
    }

    public Task<TableEntity?> TryGetAsync(string partitionKey, string rowKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_rows.TryGetValue(Key(partitionKey, rowKey), out var entity) ? Copy(entity) : null);
        }
    }

    public Task<IReadOnlyList<TableEntity>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            IReadOnlyList<TableEntity> copy = _rows.Values.Select(Copy).ToArray();
            return Task.FromResult(copy);
        }
    }

    private static string Key(string partitionKey, string rowKey) => partitionKey + "\n" + rowKey;

    private static TableEntity Copy(TableEntity source)
    {
        var copy = new TableEntity(source.PartitionKey, source.RowKey);
        foreach (var pair in source)
        {
            if (pair.Key is "PartitionKey" or "RowKey" or "Timestamp" or "odata.etag")
            {
                continue;
            }

            copy[pair.Key] = pair.Value;
        }

        return copy;
    }
}
