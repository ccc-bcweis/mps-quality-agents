// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Access;

/// <summary>
/// Demo access settings. Home-tenant users are let in by Entra (assignment required). Anyone else must have redeemed a join link.
/// </summary>
public sealed class AccessOptions
{
    public const string DefaultAdmin = "bcweis@commonconsulting.us";

    public string? HomeTenantId { get; init; }

    public IReadOnlyList<string> AdminEmails { get; init; } = [DefaultAdmin];

    public static AccessOptions From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var admins = (configuration["Mps:Auth:AdminEmails"] ?? DefaultAdmin)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new AccessOptions
        {
            HomeTenantId = configuration["Mps:Auth:HomeTenantId"],
            AdminEmails = admins.Length == 0 ? [DefaultAdmin] : admins,
        };
    }

    public bool IsAdminEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && AdminEmails.Any(admin => string.Equals(admin, email.Trim(), StringComparison.OrdinalIgnoreCase));

    public bool IsHomeTenant(string? tenantId) =>
        !string.IsNullOrWhiteSpace(HomeTenantId) && string.Equals(HomeTenantId, tenantId, StringComparison.OrdinalIgnoreCase);
}
