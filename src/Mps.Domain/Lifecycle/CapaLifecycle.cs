// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Capa;

namespace Mps.Domain.Lifecycle;

/// <summary>
/// CAPA stages after Open advance one step at a time. Verification failure is not a backward edge in this contract.
/// </summary>
public static class CapaLifecycle
{
    private static readonly HashSet<(CapaStage From, CapaStage To)> Allowed = new()
    {
        (CapaStage.Open, CapaStage.Containment),
        (CapaStage.Containment, CapaStage.RootCauseConfirmed),
        (CapaStage.RootCauseConfirmed, CapaStage.CorrectiveAction),
        (CapaStage.CorrectiveAction, CapaStage.Verification),
        (CapaStage.Verification, CapaStage.Closed),
    };

    public static bool CanTransition(CapaStage from, CapaStage to) => Allowed.Contains((from, to));
}
