// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Agents;

/// <summary>
/// Marks the agent assembly. Agents may use read-only application tools and the model gateway.
/// </summary>
public static class LayerAnchor
{
    public static string Name => "Mps.Agents";

    public static string DependsOn =>
        Mps.Application.LayerAnchor.Name + "+" + Mps.ModelGateway.LayerAnchor.Name;
}
