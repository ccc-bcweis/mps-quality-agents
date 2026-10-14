// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Orchestration;

/// <summary>
/// Marks the orchestration assembly. The workflow composes agents and domain gates. It does not bypass them.
/// </summary>
public static class LayerAnchor
{
    public static string Name => "Mps.Orchestration";

    public static string DependsOn =>
        Mps.Domain.LayerAnchor.Name
        + "+"
        + Mps.Application.LayerAnchor.Name
        + "+"
        + Mps.Agents.LayerAnchor.Name;
}
