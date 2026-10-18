// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Api.Access;

/// <summary>
/// Access settings for Entra sign-in mode. Home-tenant users are let in by Entra (assignment required). Anyone else must
/// have redeemed a join link. Admins come only from configuration: <c>Mps:Auth:AdminEmails</c> or <c>ADMIN_EMAILS</c>
/// (comma or semicolon separated). There is no built-in admin; Entra mode refuses to start with an empty list.
/// </summary>
public sealed class AccessOptions
{
    public const string AdminEmailsSetting = "Mps:Auth:AdminEmails";

    public const string AdminEmailsVariable = "ADMIN_EMAILS";

    public const string HomeTenantSetting = "Mps:Auth:HomeTenantId";

    public string? HomeTenantId { get; init; }

    public IReadOnlyList<string> AdminEmails { get; init; } = [];

    public static AccessOptions From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var configured = configuration[AdminEmailsSetting];
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = configuration[AdminEmailsVariable];
        }

        var admins = (configured ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new AccessOptions
        {
            HomeTenantId = configuration[HomeTenantSetting],
            AdminEmails = admins,
        };
    }

    public bool IsAdminEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && AdminEmails.Any(admin => string.Equals(admin, email.Trim(), StringComparison.OrdinalIgnoreCase));

    public bool IsHomeTenant(string? tenantId) =>
        !string.IsNullOrWhiteSpace(HomeTenantId) && string.Equals(HomeTenantId, tenantId, StringComparison.OrdinalIgnoreCase);
}
