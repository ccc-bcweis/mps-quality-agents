// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Tools;
using Mps.Domain.Enterprise;

namespace Mps.Connectors.Docs;

public sealed class HybridDocumentSearch : IDocumentSearchTool
{
    private readonly DocumentIndex _index;

    public HybridDocumentSearch(string indexPath)
    {
        _index = DocumentIndex.Load(indexPath);
    }

    public Task<IReadOnlyList<DocumentHit>> SearchAsync(string query, string? toolFamily, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<DocumentHit> hits = _index.Search(query, toolFamily);
        return Task.FromResult(hits);
    }
}
