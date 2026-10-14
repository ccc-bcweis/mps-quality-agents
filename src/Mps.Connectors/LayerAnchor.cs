// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Connectors;

/// <summary>
/// Marks the connector assembly. Connectors implement application ports, including write ports the agents cannot see.
/// </summary>
public static class LayerAnchor
{
    public static string Name => "Mps.Connectors";

    public static string DependsOn => Mps.Application.LayerAnchor.Name;
}
