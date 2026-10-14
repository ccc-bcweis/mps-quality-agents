// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api;

/// <summary>
/// Marks the API host. The host composes application services and orchestration and does not own business rules.
/// </summary>
public static class LayerAnchor
{
    public static string Name => "Mps.Api";

    public static string DependsOn =>
        Mps.Application.LayerAnchor.Name + "+" + Mps.Orchestration.LayerAnchor.Name;
}
