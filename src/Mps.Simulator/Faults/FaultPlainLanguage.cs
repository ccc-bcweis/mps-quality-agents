// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Simulator.Faults;

/// <summary>
/// Labels that stay off the supervisor-facing lines. The category keys in the matrix are not sentences.
/// </summary>
public static class FaultPlainLanguage
{
    public static readonly string[] HiddenLabels =
    [
        "engineeringChange",
        "equipmentCalibrationOrAlignment",
        "supplyChainMaterial",
        "processDrift",
        "expectedBehavior",
        "GroundTruth",
        "rootCause",
    ];
}
