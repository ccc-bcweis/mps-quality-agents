// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Agents.Impact;

/// <summary>
/// Shape of a parameter trace. The resolution agent receives this feature and does not infer it.
/// </summary>
public enum SignalShape
{
    Step,
    Ramp,
    Oscillation,
    Intermittent,
    Unknown,
}
