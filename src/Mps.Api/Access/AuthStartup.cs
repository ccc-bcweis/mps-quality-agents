// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Net;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

using Mps.Api.Auth;

namespace Mps.Api.Access;

/// <summary>
/// Checks the sign-in setup once at startup. Two modes, set by <c>Mps:Auth:Mode</c>:
/// <list type="bullet">
/// <item><c>local</c> (default): no sign-in, the local user is an admin. Meant for Development on localhost; a loud
/// warning is logged when the app runs outside Development or listens on a non-loopback address.</item>
/// <item><c>azure</c> (or <c>entra</c>): Container Apps Easy Auth signs people in with your own Entra app. Admins come
/// from <c>Mps:Auth:AdminEmails</c> / <c>ADMIN_EMAILS</c>; the app will not start with an empty list.</item>
/// </list>
/// </summary>
public static partial class AuthStartup
{
    public const string ModeSetting = "Mps:Auth:Mode";

    /// <summary>Set to false when a container publishes the port only on the host's loopback (docker compose does).</summary>
    public const string LocalNetworkWarningSetting = "Mps:Auth:LocalNetworkWarning";

    [LoggerMessage(EventId = 7401, Level = LogLevel.Information, Message = "Sign-in mode: Entra (Easy Auth). {Count} admin email(s) configured.")]
    private static partial void EntraMode(ILogger logger, int count);

    [LoggerMessage(EventId = 7402, Level = LogLevel.Warning, Message = "Sign-in mode: Entra, but Mps:Auth:HomeTenantId is not set. Only admins and join-link members can use the console.")]
    private static partial void NoHomeTenant(ILogger logger);

    [LoggerMessage(EventId = 7403, Level = LogLevel.Information, Message = "Sign-in mode: local. No sign-in; the local user is an admin.")]
    private static partial void LocalMode(ILogger logger);

    [LoggerMessage(EventId = 7404, Level = LogLevel.Warning, Message = "*** WARNING *** Sign-in mode is local (no sign-in, everyone is an admin) but the environment is {Environment}. Use Mps:Auth:Mode=azure with your own Entra app for anything shared.")]
    private static partial void LocalOutsideDevelopment(ILogger logger, string environment);

    [LoggerMessage(EventId = 7405, Level = LogLevel.Warning, Message = "*** WARNING *** Sign-in mode is local (no sign-in, everyone is an admin) and the app listens on {Address}, which other machines may reach. Bind to http://localhost:8080 unless you mean to share it.")]
    private static partial void LocalExposed(ILogger logger, string address);

    /// <summary>Validates the configuration. Throws when Entra mode has no admin, so a deploy never loses its owner silently.</summary>
    public static void Validate(IConfiguration configuration, IHostEnvironment environment, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);
        if (EasyAuthPrincipal.IsAzure(configuration[ModeSetting]))
        {
            var options = AccessOptions.From(configuration);
            if (options.AdminEmails.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Sign-in mode is Entra ({ModeSetting}=azure) but no admin is configured. Set {AccessOptions.AdminEmailsVariable} " +
                    $"or {AccessOptions.AdminEmailsSetting} to one or more email addresses (comma separated). The app will not start without an admin.");
            }

            EntraMode(logger, options.AdminEmails.Count);
            if (string.IsNullOrWhiteSpace(options.HomeTenantId))
            {
                NoHomeTenant(logger);
            }

            return;
        }

        LocalMode(logger);
        if (!environment.IsDevelopment())
        {
            LocalOutsideDevelopment(logger, environment.EnvironmentName);
        }
    }

    /// <summary>After Kestrel binds, warns when local mode listens beyond loopback.</summary>
    public static void WarnIfExposed(IServer server, IConfiguration configuration, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        if (EasyAuthPrincipal.IsAzure(configuration[ModeSetting]) || !configuration.GetValue(LocalNetworkWarningSetting, true))
        {
            return;
        }

        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses ?? [];
        foreach (var address in addresses)
        {
            if (!IsLoopback(address))
            {
                LocalExposed(logger, address);
            }
        }
    }

    public static bool IsLoopback(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var normalized = address.Replace("://+", "://wildcard", StringComparison.Ordinal).Replace("://*", "://wildcard", StringComparison.Ordinal);
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(uri.Host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip);
    }
}
