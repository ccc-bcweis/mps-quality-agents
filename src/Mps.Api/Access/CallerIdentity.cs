// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Text;
using System.Text.Json;

namespace Mps.Api.Access;

/// <summary>
/// Who signed in, read from the Easy Auth principal header: object id, home tenant, email-like name, and display name.
/// </summary>
public sealed record CallerIdentity(string? ObjectId, string? TenantId, string? Email, string? DisplayName)
{
    private const string OidClaim = "http://schemas.microsoft.com/identity/claims/objectidentifier";
    private const string TidClaim = "http://schemas.microsoft.com/identity/claims/tenantid";
    private const string EmailClaim = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress";
    private const string UpnClaim = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn";

    public static readonly CallerIdentity None = new(null, null, null, null);

    /// <summary>Stable member key across tenants: tenant id and object id.</summary>
    public string? MemberKey => string.IsNullOrWhiteSpace(ObjectId) ? null : (TenantId ?? "unknown") + ":" + ObjectId;

    public static CallerIdentity Read(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return None;
        }

        try
        {
            var text = header.Trim().Replace('-', '+').Replace('_', '/');
            var mod = text.Length % 4;
            var bytes = Convert.FromBase64String(mod == 0 ? text : text + new string('=', 4 - mod));
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(bytes));
            if (!document.RootElement.TryGetProperty("claims", out var claims) || claims.ValueKind != JsonValueKind.Array)
            {
                return None;
            }

            string? oid = null, tid = null, email = null, upn = null, preferred = null, name = null;
            foreach (var claim in claims.EnumerateArray())
            {
                if (claim.ValueKind != JsonValueKind.Object
                    || !claim.TryGetProperty("typ", out var typ) || typ.ValueKind != JsonValueKind.String
                    || !claim.TryGetProperty("val", out var val) || val.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var type = typ.GetString();
                var value = val.GetString();
                switch (type)
                {
                    case "oid" or OidClaim:
                        oid ??= value;
                        break;
                    case "tid" or TidClaim:
                        tid ??= value;
                        break;
                    case "email" or EmailClaim:
                        email ??= value;
                        break;
                    case "upn" or UpnClaim:
                        upn ??= value;
                        break;
                    case "preferred_username":
                        preferred ??= value;
                        break;
                    case "name":
                        name ??= value;
                        break;
                }
            }

            return new CallerIdentity(oid, tid, email ?? preferred ?? upn, name);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return None;
        }
    }
}
