// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Equipment;
using Mps.Simulator.Sites;

namespace Mps.Simulator.Tests;

internal static class KnownSiteTools
{
    public static IReadOnlyDictionary<string, ToolFamily> Families { get; } =
        SiteLayoutLoader.Load(Path.Combine(RepoPaths.FleetDirectory(), "sites.yaml")).Families;
}
