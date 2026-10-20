// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Azure.Core;
using Azure.Identity;

namespace Mps.Api.Access;

/// <summary>
/// Picks the access store from configuration. <c>Mps:Access:Store</c> is <c>Sqlite</c> (the default) or <c>AzureTable</c>.
/// Azure Table storage uses <c>Mps:Access:TableEndpoint</c> and the table names in configuration. A configured client id
/// (<c>AZURE_CLIENT_ID</c> or <c>Mps:Access:ManagedIdentityClientId</c>) uses <see cref="ManagedIdentityCredential"/>.
/// Otherwise the credential is <see cref="DefaultAzureCredential"/>.
/// </summary>
public static class AccessStoreFactory
{
    public const string StoreSetting = "Mps:Access:Store";

    public const string EndpointSetting = "Mps:Access:TableEndpoint";

    public const string LinksTableSetting = "Mps:Access:LinksTable";

    public const string MembersTableSetting = "Mps:Access:MembersTable";

    public const string ClientIdSetting = "Mps:Access:ManagedIdentityClientId";

    public static IJoinLinkStore Create(IConfiguration configuration, string sqliteConnectionString, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sqliteConnectionString);
        ArgumentNullException.ThrowIfNull(time);
        var kind = configuration[StoreSetting];
        if (string.IsNullOrWhiteSpace(kind) || kind.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return new JoinLinkStore(new SqliteAccessDirectory(sqliteConnectionString), time);
        }

        if (!kind.Equals("AzureTable", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(StoreSetting + " '" + kind.Trim() + "' is not a known access store. Use Sqlite or AzureTable.");
        }

        var endpoint = configuration[EndpointSetting];
        if (string.IsNullOrWhiteSpace(endpoint) || !Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(EndpointSetting + " must be the https table endpoint. Do not set a connection string or account key.");
        }

        var links = Required(configuration, LinksTableSetting);
        var members = Required(configuration, MembersTableSetting);
        var directory = AzureTableAccessDirectory.Connect(uri, Credential(configuration), links, members);
        return new JoinLinkStore(directory, time);
    }

    public static TokenCredential Credential(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var clientId = configuration["AZURE_CLIENT_ID"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            clientId = configuration[ClientIdSetting];
        }

        return string.IsNullOrWhiteSpace(clientId)
            ? new DefaultAzureCredential()
            : new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId.Trim()));
    }

    private static string Required(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(key + " is not set. appsettings.json ships the table name.");
        }

        return value.Trim();
    }
}
