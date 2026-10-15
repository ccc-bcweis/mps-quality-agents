// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Auth;

namespace Mps.Api.Access;

/// <summary>
/// Hosted demo only: lets home-tenant users and invited members reach the API. Others get a 403 that tells them to ask for a join link.
/// Entra (Easy Auth) has already signed the caller in; this is the app-side allowlist.
/// </summary>
public sealed class AccessGate
{
    private readonly RequestDelegate _next;

    public AccessGate(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext http, IConfiguration configuration, JoinLinkStore store)
    {
        ArgumentNullException.ThrowIfNull(http);
        var path = http.Request.Path;
        var gated = path.StartsWithSegments("/api") || path.StartsWithSegments("/hubs");
        var open = path.StartsWithSegments("/api/join") || path.StartsWithSegments("/api/admin/session");
        if (!gated || open || !Hosted(configuration) || await IsAllowedAsync(http, configuration, store, http.RequestAborted).ConfigureAwait(false))
        {
            await _next(http).ConfigureAwait(false);
            return;
        }

        http.Response.StatusCode = StatusCodes.Status403Forbidden;
        await http.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
            title = "You are signed in but not part of this workspace yet. Ask the owner for a join link.",
            status = 403,
        }).ConfigureAwait(false);
    }

    public static bool Hosted(IConfiguration configuration) =>
        string.Equals(configuration["Mps:Auth:Mode"], EasyAuthPrincipal.AzureMode, StringComparison.OrdinalIgnoreCase);

    public static bool IsAdmin(HttpContext http, IConfiguration configuration)
    {
        var header = http.Request.Headers[EasyAuthPrincipal.HeaderName].ToString();
        var caller = EasyAuthPrincipal.Read(string.IsNullOrWhiteSpace(header) ? null : header, configuration["Mps:Auth:Mode"]);
        return caller.IsAdmin || AccessOptions.From(configuration).IsAdminEmail(CallerIdentity.Read(header).Email);
    }

    public static async Task<bool> IsAllowedAsync(HttpContext http, IConfiguration configuration, JoinLinkStore store, CancellationToken cancellationToken)
    {
        var header = http.Request.Headers[EasyAuthPrincipal.HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(header))
        {
            // Bearer calls (CLI smoke tests) carry no principal header here; Easy Auth already required an assigned home-tenant user.
            return true;
        }

        var options = AccessOptions.From(configuration);
        var identity = CallerIdentity.Read(header);
        if (IsAdmin(http, configuration) || string.IsNullOrWhiteSpace(identity.TenantId) || options.IsHomeTenant(identity.TenantId))
        {
            return true;
        }

        return identity.MemberKey is not null && await store.IsMemberAsync(identity.MemberKey, cancellationToken).ConfigureAwait(false);
    }
}
