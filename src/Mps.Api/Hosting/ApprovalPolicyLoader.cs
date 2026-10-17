// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Security.Cryptography;

using Mps.Application.Decisions;

namespace Mps.Api.Hosting;

/// <summary>
/// Reads the approval-token signing key once, from <c>Mps:Approval:SigningKey</c> (hex, at least 32 bytes).
/// The hosted app gets it from Key Vault (secret <c>approval-signing-key</c>) through a Container Apps secret reference.
/// There is no built-in key. Outside Development a missing key stops the app at startup. In Development a random key
/// is made for this process, so approval tokens do not survive a restart.
/// </summary>
public static partial class ApprovalPolicyLoader
{
    public const string SigningKeySetting = "Mps:Approval:SigningKey";
    public const int MinimumKeyBytes = 32;

    public static ApprovalPolicy Load(IConfiguration configuration, IHostEnvironment environment, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);
        var minutes = configuration.GetValue("Approval:LifetimeMinutes", 15);
        var lifetime = TimeSpan.FromMinutes(minutes <= 0 ? 15 : minutes);
        return new ApprovalPolicy(SigningKey(configuration[SigningKeySetting], environment, logger), lifetime);
    }

    public static byte[] SigningKey(string? configured, IHostEnvironment environment, ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            byte[] key;
            try
            {
                key = Convert.FromHexString(configured.Trim());
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException($"{SigningKeySetting} must be hex.", ex);
            }

            if (key.Length < MinimumKeyBytes)
            {
                throw new InvalidOperationException($"{SigningKeySetting} must be at least {MinimumKeyBytes} bytes ({MinimumKeyBytes * 2} hex characters).");
            }

            return key;
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"{SigningKeySetting} is not set. Approvals cannot be signed, so the app will not start in {environment.EnvironmentName}. " +
                "Set it from the Key Vault secret approval-signing-key.");
        }

        RandomKeyInUse(logger, SigningKeySetting);
        return RandomNumberGenerator.GetBytes(MinimumKeyBytes);
    }

    [LoggerMessage(EventId = 7301, Level = LogLevel.Warning, Message = "{Setting} is not set. Development uses a random approval signing key for this process; approval tokens will not survive a restart.")]
    private static partial void RandomKeyInUse(ILogger logger, string setting);
}
