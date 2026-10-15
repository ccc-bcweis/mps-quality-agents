// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Impact;
using Mps.Simulator.Seed;
using Mps.Simulator.Sites;

namespace Mps.Api.Live;

/// <summary>
/// Reads the enterprise seed so an investigation can see a material lot that crosses plants.
/// </summary>
public sealed class CrossSiteMaterialCatalog
{
    private readonly EnterpriseSeed _seed;

    public CrossSiteMaterialCatalog(string seedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seedPath);
        _seed = SeedJson.Read(seedPath);
    }

    public ImpactScope? Find(string toolId) => SharedLotFinder.Find(_seed, toolId);
}
