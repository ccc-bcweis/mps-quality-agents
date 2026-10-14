// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Mps.Domain;
using Mps.Domain.Identity;

namespace Mps.Domain.Approval;

public readonly record struct ApprovalClaims(
    Guid TokenId,
    string ApproverId,
    string ActionHash,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

public static class ApprovalSignatures
{
    public static string Sign(ApprovalClaims claims, ReadOnlySpan<byte> signingKey)
    {
        if (signingKey.IsEmpty)
        {
            throw new DomainRuleException("Approval signing key is required.");
        }

        var hash = HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(Canonical(claims)));
        return Convert.ToHexString(hash);
    }

    public static bool Matches(ApprovalClaims claims, string signature, ReadOnlySpan<byte> signingKey)
    {
        if (string.IsNullOrWhiteSpace(signature) || signingKey.IsEmpty)
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(Sign(claims, signingKey));
        var actual = Encoding.UTF8.GetBytes(signature.Trim());
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    public static string Canonical(ApprovalClaims claims)
    {
        return string.Join(
            '|',
            claims.TokenId.ToString("D", CultureInfo.InvariantCulture),
            claims.ApproverId.Trim(),
            claims.ActionHash.Trim(),
            claims.IssuedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            claims.ExpiresAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// HMAC-signed, single-use, and bound to one action hash. The model never receives the signing key.
/// </summary>
public sealed class ApprovalToken
{
    private bool _consumed;

    private ApprovalToken(
        ApprovalTokenId id,
        string approverId,
        string actionHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        string signature,
        bool consumed)
    {
        Id = id;
        ApproverId = approverId;
        ActionHash = actionHash;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        Signature = signature;
        _consumed = consumed;
    }

    public ApprovalTokenId Id { get; }

    public string ApproverId { get; }

    public string ActionHash { get; }

    public DateTimeOffset IssuedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public string Signature { get; }

    public bool IsConsumed => _consumed;

    public static ApprovalToken Issue(
        ApprovalTokenId id,
        string approverId,
        string actionHash,
        DateTimeOffset issuedAt,
        TimeSpan lifetime,
        ReadOnlySpan<byte> signingKey)
    {
        if (id.IsEmpty)
        {
            throw new DomainRuleException("Approval token id is required.");
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new DomainRuleException("Approval token lifetime must be positive.");
        }

        var normalizedApprover = RequiredText.Value(approverId, "Approver id is required.");
        var normalizedHash = RequiredText.Value(actionHash, "Action hash is required.");
        var expiresAt = issuedAt.Add(lifetime);
        var claims = new ApprovalClaims(id.Value, normalizedApprover, normalizedHash, issuedAt, expiresAt);
        var signature = ApprovalSignatures.Sign(claims, signingKey);
        return new ApprovalToken(id, normalizedApprover, normalizedHash, issuedAt, expiresAt, signature, consumed: false);
    }

    public static ApprovalToken Rehydrate(
        ApprovalTokenId id,
        string approverId,
        string actionHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        string signature,
        bool consumed)
    {
        if (id.IsEmpty)
        {
            throw new DomainRuleException("Approval token id is required.");
        }

        if (expiresAt <= issuedAt)
        {
            throw new DomainRuleException("Approval token expiry must be after it was issued.");
        }

        return new ApprovalToken(
            id,
            RequiredText.Value(approverId, "Approver id is required."),
            RequiredText.Value(actionHash, "Action hash is required."),
            issuedAt,
            expiresAt,
            RequiredText.Value(signature, "Approval token signature is required."),
            consumed);
    }

    public void Consume(string actionHash, DateTimeOffset now, ReadOnlySpan<byte> signingKey)
    {
        if (_consumed)
        {
            throw new DomainRuleException("Approval token is single-use and has already been consumed.");
        }

        if (now >= ExpiresAt)
        {
            throw new DomainRuleException("Approval token is expired.");
        }

        if (!string.Equals(actionHash, ActionHash, StringComparison.Ordinal))
        {
            throw new DomainRuleException("Approval token is bound to a different action.");
        }

        var claims = new ApprovalClaims(Id.Value, ApproverId, ActionHash, IssuedAt, ExpiresAt);
        if (!ApprovalSignatures.Matches(claims, Signature, signingKey))
        {
            throw new DomainRuleException("Approval token signature is invalid.");
        }

        _consumed = true;
    }
}
