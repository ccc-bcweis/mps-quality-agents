// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Capa;
using Mps.Domain.Incidents;

namespace Mps.Application.Writes;

/// <summary>
/// Write ports. Agents must not take a dependency on this namespace. The API enforces the approval token
/// before these methods run, and <see cref="CapaAction"/> cannot be constructed without one.
/// </summary>
public interface IQmsCapaWriter
{
    Task<string> CreateAsync(CapaAction capa, CancellationToken cancellationToken);
}

public interface ITicketingPort
{
    Task<Ticket> OpenAsync(Incident incident, string title, CancellationToken cancellationToken);
}
