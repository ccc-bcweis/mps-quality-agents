// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Application.Decisions;

/// <summary>
/// Signing material for approval tokens. The key comes from configuration; there is no built-in key.
/// </summary>
public sealed class ApprovalPolicy
{
    private readonly byte[] _signingKey;

    public ApprovalPolicy(byte[] signingKey, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        if (signingKey.Length == 0)
        {
            throw new ArgumentException("Approval signing key is required.", nameof(signingKey));
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Approval token lifetime must be positive.");
        }

        _signingKey = signingKey.ToArray();
        Lifetime = lifetime;
    }

    public ReadOnlyMemory<byte> SigningKey => _signingKey;

    public TimeSpan Lifetime { get; }
}
