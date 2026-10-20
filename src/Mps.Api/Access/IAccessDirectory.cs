// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Access;

/// <summary>Persistence for links and members. The store applies the rules; an adapter only reads and writes.</summary>
public interface IAccessDirectory
{
    Task<JoinLink?> FindLinkByTokenAsync(string token, CancellationToken cancellationToken);

    Task<JoinLink?> FindLinkByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<JoinLink>> ListLinksAsync(CancellationToken cancellationToken);

    Task InsertLinkAsync(JoinLink link, CancellationToken cancellationToken);

    Task UpdateLinkAsync(JoinLink link, CancellationToken cancellationToken);

    Task<AccessMember?> FindMemberAsync(string memberKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<AccessMember>> ListMembersAsync(CancellationToken cancellationToken);

    Task UpsertMemberAsync(AccessMember member, CancellationToken cancellationToken);

    Task SaveRedeemAsync(JoinLink link, AccessMember member, CancellationToken cancellationToken);
}
