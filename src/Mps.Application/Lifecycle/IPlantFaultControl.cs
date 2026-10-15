// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Application.Lifecycle;

/// <summary>Port onto the running plant so a lifecycle step can remove the faults behind an incident.</summary>
public interface IPlantFaultControl
{
    /// <summary>Removes every injected fault on the tool. Returns how many were removed.</summary>
    int ClearTool(string toolId);

    /// <summary>Removes every injected fault on every tool. Returns how many were removed.</summary>
    int ClearAll();
}
