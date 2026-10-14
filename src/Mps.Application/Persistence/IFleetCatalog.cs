// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Equipment;
using Mps.Domain.Identity;

namespace Mps.Application.Persistence;

public interface IFleetCatalog
{
    Task<IReadOnlyList<Tool>> ListAsync(CancellationToken cancellationToken);

    Task<Tool?> FindAsync(ToolId toolId, CancellationToken cancellationToken);
}
