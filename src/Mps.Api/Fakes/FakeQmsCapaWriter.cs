// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Writes;
using Mps.Domain.Capa;

namespace Mps.Api.Fakes;

/// <summary>
/// Stand-in QMS. The external id is derived from the CAPA id so a repeat create stays recognizable.
/// </summary>
public sealed class FakeQmsCapaWriter : IQmsCapaWriter
{
    public Task<string> CreateAsync(CapaAction capa, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capa);
        _ = cancellationToken;
        return Task.FromResult("CAPA-" + capa.Id.Value.ToString("N")[..8]);
    }
}
