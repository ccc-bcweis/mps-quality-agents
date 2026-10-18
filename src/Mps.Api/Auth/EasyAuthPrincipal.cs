// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text;
using System.Text.Json;

namespace Mps.Api.Auth;

/// <summary>
/// Easy Auth principal from <c>X-MS-CLIENT-PRINCIPAL</c>. Local mode with no header is the dev user.
/// </summary>
public static class EasyAuthPrincipal
{
    public const string HeaderName = "X-MS-CLIENT-PRINCIPAL";

    public const string AdminRole = "admin";

    public const string LocalMode = "local";

    public const string AzureMode = "azure";

    /// <summary>Accepted spelling of <see cref="AzureMode"/>: Entra sign-in through Container Apps Easy Auth.</summary>
    public const string EntraAlias = "entra";

    public const string LocalDisplayName = "dev-user";

    public static DemoCaller Read(string? header, string? mode)
    {
        var azure = IsAzure(mode);
        if (string.IsNullOrWhiteSpace(header))
        {
            return azure
                ? new DemoCaller(AzureMode, false, "anonymous")
                : new DemoCaller(LocalMode, true, LocalDisplayName);
        }

        if (!TryParse(header, out var displayName, out var admin))
        {
            return new DemoCaller(azure ? AzureMode : LocalMode, false, "unknown");
        }

        return new DemoCaller(azure ? AzureMode : LocalMode, admin, displayName);
    }

    /// <summary>True for Entra sign-in mode (<c>azure</c> or <c>entra</c>). Anything else, including blank, is local mode.</summary>
    public static bool IsAzure(string? mode)
    {
        var value = string.IsNullOrWhiteSpace(mode) ? LocalMode : mode.Trim();
        return string.Equals(value, AzureMode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, EntraAlias, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParse(string header, out string displayName, out bool admin)
    {
        displayName = LocalDisplayName;
        admin = false;
        if (!TryDecode(header.Trim(), out var bytes))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var roleType = Text(document.RootElement, "role_typ");
            var nameType = Text(document.RootElement, "name_typ");
            if (!document.RootElement.TryGetProperty("claims", out var claims) || claims.ValueKind != JsonValueKind.Array)
            {
                return true;
            }

            foreach (var claim in claims.EnumerateArray())
            {
                if (claim.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var type = Text(claim, "typ");
                var value = Text(claim, "val");
                if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(value))
                {
                    continue;
                }

                if (IsRole(type, roleType) && string.Equals(value, AdminRole, StringComparison.OrdinalIgnoreCase))
                {
                    admin = true;
                }

                if (IsName(type, nameType))
                {
                    displayName = TrimName(value);
                }
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryDecode(string header, out byte[] bytes)
    {
        if (TryFromBase64(header, out bytes))
        {
            return true;
        }

        var url = header.Replace('-', '+').Replace('_', '/');
        return TryFromBase64(url, out bytes);
    }

    private static bool TryFromBase64(string value, out byte[] bytes)
    {
        try
        {
            var mod = value.Length % 4;
            var padded = mod == 0 ? value : value + new string('=', 4 - mod);
            bytes = Convert.FromBase64String(padded);
            return true;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }

    private static string? Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static bool IsRole(string type, string? roleType)
    {
        if (string.Equals(type, "roles", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "role", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(roleType) && string.Equals(type, roleType, StringComparison.Ordinal))
        {
            return true;
        }

        return type.EndsWith("/role", StringComparison.OrdinalIgnoreCase)
            || type.EndsWith("/roles", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsName(string type, string? nameType)
    {
        if (string.Equals(type, "name", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "preferred_username", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrEmpty(nameType) && string.Equals(type, nameType, StringComparison.Ordinal);
    }

    private static string TrimName(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return LocalDisplayName;
        }

        return trimmed.Length <= 200 ? trimmed : trimmed[..200];
    }
}

public sealed record DemoCaller(string Mode, bool IsAdmin, string DisplayName);
