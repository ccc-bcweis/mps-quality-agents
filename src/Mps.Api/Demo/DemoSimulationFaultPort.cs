// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Printouts;
using Mps.Simulator.Demo;

namespace Mps.Api.Demo;

/// <summary>Hands a confirmed bench reading to the running demo simulation.</summary>
public sealed class DemoSimulationFaultPort : ISimulationFaultPort
{
    private readonly DemoRuntime _runtime;

    public DemoSimulationFaultPort(DemoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    public bool TryApply(string toolId, string? chamberId, string parameter, string shape, double magnitude)
    {
        return _runtime.TryApplyFreeForm(toolId, chamberId, parameter, shape, magnitude);
    }
}
