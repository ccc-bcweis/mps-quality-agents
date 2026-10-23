// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Domain.Approval;
using Mps.Domain.Identity;

namespace Mps.Domain.Tests;

public sealed class ApprovalTokenTests
{
    private static readonly byte[] Key = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

    private static readonly DateTimeOffset Issued = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);

    private static readonly Guid TokenGuid = Guid.Parse("22222222-2222-4222-8222-222222222222");

    [Fact]
    public void IssueTrimsTheApproverAndHashAndSignsTheCanonicalClaims()
    {
        var claims = new ApprovalClaims(
            TokenGuid,
            "eng-17",
            "ABC",
            Issued,
            Issued.AddMinutes(15));

        ApprovalSignatures.Canonical(claims).Should().Be(
            "22222222-2222-4222-8222-222222222222|eng-17|ABC|2026-10-14T15:00:00.0000000Z|2026-10-14T15:15:00.0000000Z");
        ApprovalSignatures.Canonical(claims with { ApproverId = "  eng-17  ", ActionHash = "  ABC  " })
            .Should().Be(ApprovalSignatures.Canonical(claims));

        var signature = ApprovalSignatures.Sign(claims, Key);
        signature.Should().Be("34DECDD36206CAD05E1AF314534958BFF4FA043FA60F0C7E64760705E6A1731E");
        ApprovalSignatures.Matches(claims, "  " + signature.ToLowerInvariant() + "  ", Key).Should().BeFalse();
        ApprovalSignatures.Matches(claims, "  " + signature + "  ", Key).Should().BeTrue();
        ApprovalSignatures.Matches(claims, signature, Key).Should().BeTrue();
        ApprovalSignatures.Matches(claims, null!, Key).Should().BeFalse();
        ApprovalSignatures.Matches(claims, string.Empty, Key).Should().BeFalse();
        ApprovalSignatures.Matches(claims, "   ", Key).Should().BeFalse();
        ApprovalSignatures.Matches(claims, signature, ReadOnlySpan<byte>.Empty).Should().BeFalse();
        ApprovalSignatures.Matches(claims, new string('A', signature.Length), Key).Should().BeFalse();

        var token = ApprovalToken.Issue(new ApprovalTokenId(TokenGuid), "  eng-17  ", "  ABC  ", Issued, TimeSpan.FromMinutes(15), Key);

        token.Id.Value.Should().Be(TokenGuid);
        token.ApproverId.Should().Be("eng-17");
        token.ActionHash.Should().Be("ABC");
        token.IssuedAt.Should().Be(Issued);
        token.ExpiresAt.Should().Be(Issued.AddMinutes(15));
        token.Signature.Should().Be(signature);
        token.IsConsumed.Should().BeFalse();
    }

    [Fact]
    public void ASignatureUsesUtcAndRejectsAnEmptyKey()
    {
        var offset = new DateTimeOffset(2026, 10, 14, 17, 0, 0, TimeSpan.FromHours(2));
        var claims = new ApprovalClaims(TokenGuid, "eng-17", "ABC", offset, offset.AddMinutes(15));

        ApprovalSignatures.Canonical(claims).Should().Contain("2026-10-14T15:00:00.0000000Z");

        var sign = () => ApprovalSignatures.Sign(claims, ReadOnlySpan<byte>.Empty);
        sign.Should().Throw<DomainRuleException>().WithMessage("Approval signing key is required.");
    }

    [Fact]
    public void IssueRejectsAnEmptyIdApproverHashOrLifetime()
    {
        Rejects(() => ApprovalToken.Issue(default, "eng-17", "ABC", Issued, TimeSpan.FromMinutes(1), Key), "Approval token id is required.");
        Rejects(() => ApprovalToken.Issue(ApprovalTokenId.New(), "  ", "ABC", Issued, TimeSpan.FromMinutes(1), Key), "Approver id is required.");
        Rejects(() => ApprovalToken.Issue(ApprovalTokenId.New(), null!, "ABC", Issued, TimeSpan.FromMinutes(1), Key), "Approver id is required.");
        Rejects(() => ApprovalToken.Issue(ApprovalTokenId.New(), "eng-17", " ", Issued, TimeSpan.FromMinutes(1), Key), "Action hash is required.");
        Rejects(() => ApprovalToken.Issue(ApprovalTokenId.New(), "eng-17", null!, Issued, TimeSpan.FromMinutes(1), Key), "Action hash is required.");
        Rejects(() => ApprovalToken.Issue(ApprovalTokenId.New(), "eng-17", "ABC", Issued, TimeSpan.FromTicks(-1), Key), "Approval token lifetime must be positive.");

        var issued = ApprovalToken.Issue(ApprovalTokenId.New(), "eng-17", "ABC", Issued, TimeSpan.FromTicks(1), Key);
        issued.ExpiresAt.Should().Be(Issued.AddTicks(1));
    }

    [Fact]
    public void RehydrateRequiresAnIdALaterExpiryAndTheSignedFields()
    {
        var id = new ApprovalTokenId(TokenGuid);
        var expires = Issued.AddMinutes(15);
        var token = ApprovalToken.Rehydrate(id, "  eng-17  ", "  ABC  ", Issued, expires, "  SIG  ", consumed: true);

        token.ApproverId.Should().Be("eng-17");
        token.ActionHash.Should().Be("ABC");
        token.Signature.Should().Be("SIG");
        token.IssuedAt.Should().Be(Issued);
        token.ExpiresAt.Should().Be(expires);
        token.IsConsumed.Should().BeTrue();

        Rejects(() => ApprovalToken.Rehydrate(default, "eng-17", "ABC", Issued, expires, "SIG", false), "Approval token id is required.");
        Rejects(() => ApprovalToken.Rehydrate(id, " ", "ABC", Issued, expires, "SIG", false), "Approver id is required.");
        Rejects(() => ApprovalToken.Rehydrate(id, null!, "ABC", Issued, expires, "SIG", false), "Approver id is required.");
        Rejects(() => ApprovalToken.Rehydrate(id, "eng-17", " ", Issued, expires, "SIG", false), "Action hash is required.");
        Rejects(() => ApprovalToken.Rehydrate(id, "eng-17", null!, Issued, expires, "SIG", false), "Action hash is required.");
        Rejects(() => ApprovalToken.Rehydrate(id, "eng-17", "ABC", Issued, expires, " ", false), "Approval token signature is required.");
        Rejects(() => ApprovalToken.Rehydrate(id, "eng-17", "ABC", Issued, expires, null!, false), "Approval token signature is required.");
        Rejects(() => ApprovalToken.Rehydrate(id, "eng-17", "ABC", Issued, Issued, "SIG", false), "Approval token expiry must be after it was issued.");
        Rejects(() => ApprovalToken.Rehydrate(id, "eng-17", "ABC", Issued, Issued.AddTicks(-1), "SIG", false), "Approval token expiry must be after it was issued.");

        ApprovalToken.Rehydrate(id, "eng-17", "ABC", Issued, Issued.AddTicks(1), "SIG", false).ExpiresAt.Should().Be(Issued.AddTicks(1));
    }

    [Fact]
    public void ConsumeAcceptsTheInstantBeforeExpiryAndRejectsTheExpiryInstant()
    {
        var token = LiveToken(TimeSpan.FromMinutes(15));
        var expires = token.ExpiresAt;

        token.Consume(token.ActionHash, expires.AddTicks(-1), Key);

        token.IsConsumed.Should().BeTrue();

        var expired = LiveToken(TimeSpan.FromMinutes(15));
        var act = () => expired.Consume(expired.ActionHash, expired.ExpiresAt, Key);
        act.Should().Throw<DomainRuleException>().WithMessage("Approval token is expired.");
        expired.IsConsumed.Should().BeFalse();
    }

    [Fact]
    public void ConsumeRejectsASecondUseADifferentActionABadSignatureAndAnEmptyKey()
    {
        var token = LiveToken(TimeSpan.FromMinutes(15));
        token.Consume(token.ActionHash, Issued, Key);

        var again = () => token.Consume(token.ActionHash, Issued, Key);
        again.Should().Throw<DomainRuleException>().WithMessage("Approval token is single-use and has already been consumed.");

        var rebound = LiveToken(TimeSpan.FromMinutes(15));
        var otherAction = () => rebound.Consume("abc", Issued, Key);
        otherAction.Should().Throw<DomainRuleException>().WithMessage("Approval token is bound to a different action.");
        rebound.IsConsumed.Should().BeFalse();

        var forged = ApprovalToken.Rehydrate(
            rebound.Id,
            rebound.ApproverId,
            rebound.ActionHash,
            rebound.IssuedAt,
            rebound.ExpiresAt,
            new string('B', rebound.Signature.Length),
            consumed: false);
        var badSignature = () => forged.Consume(forged.ActionHash, Issued, Key);
        badSignature.Should().Throw<DomainRuleException>().WithMessage("Approval token signature is invalid.");
        forged.IsConsumed.Should().BeFalse();

        var noKey = () => rebound.Consume(rebound.ActionHash, Issued, ReadOnlySpan<byte>.Empty);
        noKey.Should().Throw<DomainRuleException>().WithMessage("Approval token signature is invalid.");

        var already = ApprovalToken.Rehydrate(
            rebound.Id,
            rebound.ApproverId,
            rebound.ActionHash,
            rebound.IssuedAt,
            rebound.ExpiresAt,
            "not-a-signature",
            consumed: true);
        var used = () => already.Consume(already.ActionHash, Issued, Key);
        used.Should().Throw<DomainRuleException>().WithMessage("Approval token is single-use and has already been consumed.");
    }

    private static ApprovalToken LiveToken(TimeSpan lifetime)
    {
        return ApprovalToken.Issue(new ApprovalTokenId(TokenGuid), "eng-17", "ABC", Issued, lifetime, Key);
    }

    private static void Rejects<T>(Func<T> act, string message)
    {
        act.Should().Throw<DomainRuleException>().WithMessage(message);
    }
}
