// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Access;

/// <summary>
/// Join links and the members who redeemed them. Adapters hide where the rows live.
/// A demo reset does not call this store, so links and members stay.
/// </summary>
public interface IJoinLinkStore
{
    Task<JoinLink> CreateAsync(TimeSpan lifetime, int maxUses, string createdBy, CancellationToken cancellationToken);

    Task<IReadOnlyList<JoinLink>> ListLinksAsync(CancellationToken cancellationToken);

    Task<bool> RevokeLinkAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ReactivateLinkAsync(Guid id, CancellationToken cancellationToken);

    Task<RestoreLinkResult> RestoreAsync(string token, TimeSpan lifetime, int maxUses, string createdBy, CancellationToken cancellationToken);

    Task<bool> IsMemberAsync(string memberKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<AccessMember>> ListMembersAsync(CancellationToken cancellationToken);

    Task<bool> RevokeMemberAsync(string memberKey, CancellationToken cancellationToken);

    Task<RedeemResult> RedeemAsync(string token, CallerIdentity caller, CancellationToken cancellationToken);
}
