// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Access;

/// <summary>
/// Rules for join links and members. The directory is SQLite outside Azure and Azure Table storage in Azure.
/// </summary>
public sealed class JoinLinkStore : IJoinLinkStore, IDisposable
{
    private readonly IAccessDirectory _directory;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JoinLinkStore(IAccessDirectory directory, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(directory);
        _directory = directory;
        _time = time ?? TimeProvider.System;
    }

    public async Task<JoinLink> CreateAsync(TimeSpan lifetime, int maxUses, string createdBy, CancellationToken cancellationToken)
    {
        RequireWindow(lifetime, maxUses);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        var now = _time.GetUtcNow();
        var link = new JoinLink(Guid.NewGuid(), JoinToken.New(), now, now + lifetime, maxUses, 0, false, createdBy);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _directory.InsertLinkAsync(link, cancellationToken).ConfigureAwait(false);
            return link;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<JoinLink>> ListLinksAsync(CancellationToken cancellationToken)
    {
        var links = await _directory.ListLinksAsync(cancellationToken).ConfigureAwait(false);
        return links.OrderByDescending(link => link.CreatedAt).ThenBy(link => link.Id).ToArray();
    }

    public async Task<bool> RevokeLinkAsync(Guid id, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var link = await _directory.FindLinkByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (link is null)
            {
                return false;
            }

            if (!link.Revoked)
            {
                await _directory.UpdateLinkAsync(link with { Revoked = true }, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> ReactivateLinkAsync(Guid id, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var link = await _directory.FindLinkByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (link is null)
            {
                return false;
            }

            if (link.Revoked)
            {
                await _directory.UpdateLinkAsync(link with { Revoked = false }, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RestoreLinkResult> RestoreAsync(string token, TimeSpan lifetime, int maxUses, string createdBy, CancellationToken cancellationToken)
    {
        RequireWindow(lifetime, maxUses);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        if (!JoinToken.IsToken(token))
        {
            throw new ArgumentException("That is not a join link. Paste the link or the token.", nameof(token));
        }

        var normalized = token.ToLowerInvariant();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = _time.GetUtcNow();
            var existing = await _directory.FindLinkByTokenAsync(normalized, cancellationToken).ConfigureAwait(false);
            if (existing is not null && existing.IsUsable(now))
            {
                return new RestoreLinkResult(RestoreOutcome.AlreadyActive, existing);
            }

            if (existing is null)
            {
                var created = new JoinLink(Guid.NewGuid(), normalized, now, now + lifetime, maxUses, 0, false, createdBy);
                await _directory.InsertLinkAsync(created, cancellationToken).ConfigureAwait(false);
                return new RestoreLinkResult(RestoreOutcome.Created, created);
            }

            var restored = existing with
            {
                ExpiresAt = now + lifetime,
                MaxUses = maxUses,
                Uses = 0,
                Revoked = false,
            };
            await _directory.UpdateLinkAsync(restored, cancellationToken).ConfigureAwait(false);
            return new RestoreLinkResult(RestoreOutcome.Restored, restored);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> IsMemberAsync(string memberKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(memberKey))
        {
            return false;
        }

        var member = await _directory.FindMemberAsync(memberKey, cancellationToken).ConfigureAwait(false);
        return member is { IsActive: true };
    }

    public async Task<IReadOnlyList<AccessMember>> ListMembersAsync(CancellationToken cancellationToken)
    {
        var members = await _directory.ListMembersAsync(cancellationToken).ConfigureAwait(false);
        return members.OrderByDescending(member => member.JoinedAt).ThenBy(member => member.MemberKey, StringComparer.Ordinal).ToArray();
    }

    public async Task<bool> RevokeMemberAsync(string memberKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memberKey);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var member = await _directory.FindMemberAsync(memberKey, cancellationToken).ConfigureAwait(false);
            if (member is null)
            {
                return false;
            }

            if (!member.IsActive)
            {
                return true;
            }

            var revoked = member with { Status = AccessMember.RevokedStatus, RevokedAt = _time.GetUtcNow() };
            await _directory.UpsertMemberAsync(revoked, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RedeemResult> RedeemAsync(string token, CallerIdentity caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var key = caller.MemberKey;
        if (string.IsNullOrWhiteSpace(token) || key is null || !JoinToken.IsToken(token.Trim()))
        {
            return RedeemResult.NotFound;
        }

        var normalized = token.Trim().ToLowerInvariant();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var link = await _directory.FindLinkByTokenAsync(normalized, cancellationToken).ConfigureAwait(false);
            if (link is null)
            {
                return RedeemResult.NotFound;
            }

            var member = await _directory.FindMemberAsync(key, cancellationToken).ConfigureAwait(false);
            if (member is { IsActive: true })
            {
                return RedeemResult.AlreadyMember;
            }

            var now = _time.GetUtcNow();
            if (link.Revoked)
            {
                return RedeemResult.Revoked;
            }

            if (now >= link.ExpiresAt)
            {
                return RedeemResult.Expired;
            }

            if (link.Uses >= link.MaxUses)
            {
                return RedeemResult.UsedUp;
            }

            var saved = new AccessMember(
                key,
                caller.Email,
                caller.DisplayName,
                now,
                link.Id,
                caller.TenantId,
                caller.ObjectId,
                AccessMember.ActiveStatus,
                null);
            await _directory.SaveRedeemAsync(link with { Uses = link.Uses + 1 }, saved, cancellationToken).ConfigureAwait(false);
            return RedeemResult.Joined;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
        if (_directory is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    internal static void RequireWindow(TimeSpan lifetime, int maxUses)
    {
        if (lifetime < TimeSpan.FromMinutes(1) || lifetime > TimeSpan.FromDays(30))
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "A link lasts between 1 minute and 30 days.");
        }

        if (maxUses is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(maxUses), "A link allows 1 to 500 uses.");
        }
    }
}
