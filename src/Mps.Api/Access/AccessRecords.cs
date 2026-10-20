// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Access;

public sealed record JoinLink(Guid Id, string Token, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, int MaxUses, int Uses, bool Revoked, string CreatedBy)
{
    public bool IsUsable(DateTimeOffset now) => !Revoked && now < ExpiresAt && Uses < MaxUses;

    /// <summary>First six characters of the link id. Screens show this instead of the id.</summary>
    public string Label => Id.ToString("D")[..6];

    public string StatusAt(DateTimeOffset now) =>
        Revoked ? "revoked" : now >= ExpiresAt ? "expired" : Uses >= MaxUses ? "usedUp" : "active";
}

/// <summary>A person who redeemed a join link. Active members pass the access gate. Revoked members do not.</summary>
public sealed record AccessMember(
    string MemberKey,
    string? Email,
    string? DisplayName,
    DateTimeOffset JoinedAt,
    Guid LinkId,
    string? TenantId,
    string? ObjectId,
    string Status,
    DateTimeOffset? RevokedAt)
{
    public const string ActiveStatus = "active";

    public const string RevokedStatus = "revoked";

    public bool IsActive => string.Equals(Status, ActiveStatus, StringComparison.Ordinal);

    public string LinkLabel => LinkId.ToString("D")[..6];
}

public enum RedeemResult
{
    Joined,
    AlreadyMember,
    NotFound,
    Expired,
    UsedUp,
    Revoked,
}

public enum RestoreOutcome
{
    Created,
    Restored,
    AlreadyActive,
}

public sealed record RestoreLinkResult(RestoreOutcome Outcome, JoinLink? Link);
