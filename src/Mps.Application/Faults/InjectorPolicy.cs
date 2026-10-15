// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Application.Faults;

/// <summary>
/// Historical configuration key <c>Mps:Injector:DirectTicket</c>. The flag no longer opens a ticket.
/// Alerts come from the live telemetry stream crossing an SPC rule.
/// </summary>
public sealed class InjectorPolicy
{
    public InjectorPolicy(bool directTicket) => DirectTicket = directTicket;

    public bool DirectTicket { get; }
}
