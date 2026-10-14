// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Application.Errors;
using Mps.Application.Notifications;
using Mps.Application.Persistence;
using Mps.Domain.Capa;
using Mps.Domain.Identity;

namespace Mps.Application.Lifecycle;

public interface ICapaLifecycleService
{
    Task<StoredCapa> GetAsync(CapaId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredCapa>> ListAsync(CancellationToken cancellationToken);

    Task<StoredCapa> AdvanceAsync(CapaId id, CapaStage stage, CancellationToken cancellationToken);
}

public sealed class CapaLifecycleService : ICapaLifecycleService
{
    private readonly ICapaLog _capas;
    private readonly IQualityNotifier _notifier;

    public CapaLifecycleService(ICapaLog capas, IQualityNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(capas);
        ArgumentNullException.ThrowIfNull(notifier);
        _capas = capas;
        _notifier = notifier;
    }

    public async Task<StoredCapa> GetAsync(CapaId id, CancellationToken cancellationToken)
    {
        return await _capas.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new ResourceNotFoundException("CAPA was not found.");
    }

    public Task<IReadOnlyList<StoredCapa>> ListAsync(CancellationToken cancellationToken) =>
        _capas.ListAsync(cancellationToken);

    public async Task<StoredCapa> AdvanceAsync(CapaId id, CapaStage stage, CancellationToken cancellationToken)
    {
        var stored = await GetAsync(id, cancellationToken).ConfigureAwait(false);
        LifecycleMoves.MoveOne(stored.Capa, stage);
        await _capas.UpdateAsync(stored.Capa, cancellationToken).ConfigureAwait(false);
        await _notifier.CapaChangedAsync(stored.Capa, "capaAdvanced", cancellationToken).ConfigureAwait(false);
        return stored;
    }
}
