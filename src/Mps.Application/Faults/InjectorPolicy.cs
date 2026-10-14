// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Application.Faults;

/// <summary>
/// The simulator owns alert intake. Direct ticket creation from the injector stays available for a self-contained demo.
/// </summary>
public sealed class InjectorPolicy
{
    public InjectorPolicy(bool directTicket) => DirectTicket = directTicket;

    public bool DirectTicket { get; }
}
