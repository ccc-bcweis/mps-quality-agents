// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Application;

/// <summary>
/// Marks the application assembly. It may depend on the domain and on the ports defined here.
/// </summary>
public static class LayerAnchor
{
    public static string Name => "Mps.Application";

    public static string DependsOn => Mps.Domain.LayerAnchor.Name;
}
