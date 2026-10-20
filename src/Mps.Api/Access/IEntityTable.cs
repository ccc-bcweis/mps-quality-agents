// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Azure.Data.Tables;

namespace Mps.Api.Access;

/// <summary>One table of entities. Azure Table storage and the in-memory stand-in both implement this.</summary>
public interface IEntityTable
{
    Task UpsertAsync(TableEntity entity, CancellationToken cancellationToken);

    Task<TableEntity?> TryGetAsync(string partitionKey, string rowKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<TableEntity>> ListAsync(CancellationToken cancellationToken);
}
