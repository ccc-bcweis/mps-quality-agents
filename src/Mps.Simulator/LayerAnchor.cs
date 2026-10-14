// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Simulator;

/// <summary>
/// Marks the simulator assembly. It depends on the domain and must not expose ground truth to agents.
/// </summary>
public static class LayerAnchor
{
    public static string Name => "Mps.Simulator";

    public static string DependsOn => Mps.Domain.LayerAnchor.Name;
}
