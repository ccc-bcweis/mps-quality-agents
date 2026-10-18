// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Auth;

namespace Mps.Api.Access;

/// <summary>
/// Hosted demo only: lets home-tenant users and invited members reach the API. A call with no signed-in caller gets a 401;
/// a signed-in caller who is not allowed gets a 403 that tells them to ask for a join link.
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
        if (!gated || !Hosted(configuration))
        {
            await _next(http).ConfigureAwait(false);
            return;
        }

        // The platform lets anonymous browsers reach the landing page; the API itself still needs a signed-in caller.
        // Easy Auth sets the principal header for a session cookie or a valid bearer token and strips any sent by the client.
        if (string.IsNullOrWhiteSpace(http.Request.Headers[EasyAuthPrincipal.HeaderName].ToString()))
        {
            http.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await http.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                title = "Log in to use the console.",
                status = 401,
            }).ConfigureAwait(false);
            return;
        }

        if (open || await IsAllowedAsync(http, configuration, store, http.RequestAborted).ConfigureAwait(false))
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
        EasyAuthPrincipal.IsAzure(configuration["Mps:Auth:Mode"]);

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
            // Only reached for local callers; hosted API calls without a principal are rejected before this check.
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
