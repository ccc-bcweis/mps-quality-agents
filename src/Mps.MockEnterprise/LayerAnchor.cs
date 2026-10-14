// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.MockEnterprise;

/// <summary>
/// Marks the mock enterprise assembly. It serves synthetic MES, QMS, PLM, ERP, CMMS, and ticketing data.
/// </summary>
public static class LayerAnchor
{
    public static string Name => "Mps.MockEnterprise";

    public static string DependsOn => Mps.Domain.LayerAnchor.Name;
}
