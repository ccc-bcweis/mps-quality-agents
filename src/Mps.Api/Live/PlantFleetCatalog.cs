// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Persistence;
using Mps.Application.Persistence;
using Mps.Domain.Equipment;
using Mps.Domain.Identity;

namespace Mps.Api.Live;

/// <summary>
/// The stored eight-tool list stays the default catalog. A lookup falls back to every placed tool, including the other plants.
/// </summary>
public sealed class PlantFleetCatalog : IFleetCatalog
{
    private readonly EfQualityStore _store;
    private readonly PlantSimulation _plant;

    public PlantFleetCatalog(EfQualityStore store, PlantSimulation plant)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(plant);
        _store = store;
        _plant = plant;
    }

    public Task<IReadOnlyList<Tool>> ListAsync(CancellationToken cancellationToken) =>
        _store.ListToolsAsync(cancellationToken);

    public async Task<Tool?> FindAsync(ToolId toolId, CancellationToken cancellationToken)
    {
        var stored = await _store.FindAsync(toolId, cancellationToken).ConfigureAwait(false);
        return stored ?? _plant.FindTool(toolId.Value);
    }
}
