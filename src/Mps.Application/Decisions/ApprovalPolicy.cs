// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Application.Decisions;

/// <summary>
/// Local signing material for approval tokens. The demo key is not a production secret.
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

    public static byte[] DemoSigningKey() => [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
}
