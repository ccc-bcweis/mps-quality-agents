// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Mps.Api.Auth;

namespace Mps.Api.Access;

/// <summary>
/// Hosted only: a browser opening the console without a signed-in session goes to the branded landing page instead of an
/// empty console. A signed-in account without access goes there too, with the no-access message. The console HTML is never
/// cached, so signing out cannot leave a stale console shell behind. Static assets and the API are not handled here.
/// </summary>
public sealed class ConsoleEntryGate
{
    public const string LandingPage = "/landing.html";

    private readonly RequestDelegate _next;

    public ConsoleEntryGate(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext http, IConfiguration configuration, JoinLinkStore store)
    {
        ArgumentNullException.ThrowIfNull(http);
        if (!IsConsoleNavigation(http.Request))
        {
            await _next(http).ConfigureAwait(false);
            return;
        }

        http.Response.Headers.CacheControl = "no-store";
        if (AccessGate.Hosted(configuration))
        {
            var header = http.Request.Headers[EasyAuthPrincipal.HeaderName].ToString();
            if (string.IsNullOrWhiteSpace(header))
            {
                var next = http.Request.Path.Value + http.Request.QueryString.Value;
                http.Response.Redirect(next is "/" or "/index.html" ? LandingPage : LandingPage + "?next=" + Uri.EscapeDataString(next));
                return;
            }

            // A join link must open for a signed-in guest who is not a member yet.
            if (!http.Request.Path.StartsWithSegments("/join")
                && !await AccessGate.IsAllowedAsync(http, configuration, store, http.RequestAborted).ConfigureAwait(false))
            {
                http.Response.Redirect(LandingPage + "?noAccess=1");
                return;
            }
        }

        await _next(http).ConfigureAwait(false);
    }

    /// <summary>A page load of the console: "/", "/index.html" or a route without a file extension outside the API.</summary>
    public static bool IsConsoleNavigation(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        {
            return false;
        }

        var path = request.Path;
        if (path.StartsWithSegments("/api") || path.StartsWithSegments("/hubs") || path.StartsWithSegments("/health") || path.StartsWithSegments("/.auth"))
        {
            return false;
        }

        return path.Value is null or "/" or "/index.html" || !Path.HasExtension(path.Value);
    }
}
