// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

using Azure;
using Azure.Data.Tables;

namespace Mps.Api.Access;

/// <summary>
/// Join links and members in Azure Table storage. Callers pass an endpoint and a token credential.
/// There is no account key, connection string, or shared access signature.
/// </summary>
public sealed class AzureTableAccessDirectory : IAccessDirectory, IDisposable
{
    private const string LinkRow = "link";
    private const string MemberRow = "member";

    private readonly IEntityTable _links;
    private readonly IEntityTable _members;
    private readonly TableServiceClient? _owned;

    public AzureTableAccessDirectory(IEntityTable links, IEntityTable members)
        : this(links, members, null)
    {
    }

    private AzureTableAccessDirectory(IEntityTable links, IEntityTable members, TableServiceClient? owned)
    {
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(members);
        _links = links;
        _members = members;
        _owned = owned;
    }

    public static AzureTableAccessDirectory Connect(Uri endpoint, Azure.Core.TokenCredential credential, string linksTable, string membersTable)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentException.ThrowIfNullOrWhiteSpace(linksTable);
        ArgumentException.ThrowIfNullOrWhiteSpace(membersTable);
        var service = new TableServiceClient(endpoint, credential);
        return new AzureTableAccessDirectory(
            new AzureEntityTable(service.GetTableClient(linksTable)),
            new AzureEntityTable(service.GetTableClient(membersTable)),
            service);
    }

    public async Task<JoinLink?> FindLinkByTokenAsync(string token, CancellationToken cancellationToken)
    {
        var entity = await _links.TryGetAsync(token, LinkRow, cancellationToken).ConfigureAwait(false);
        return entity is null ? null : ReadLink(entity);
    }

    public async Task<JoinLink?> FindLinkByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var idText = id.ToString("D");
        foreach (var entity in await _links.ListAsync(cancellationToken).ConfigureAwait(false))
        {
            if (string.Equals(ReadString(entity, "Id"), idText, StringComparison.Ordinal))
            {
                return ReadLink(entity);
            }
        }

        return null;
    }

    public async Task<IReadOnlyList<JoinLink>> ListLinksAsync(CancellationToken cancellationToken)
    {
        var links = new List<JoinLink>();
        foreach (var entity in await _links.ListAsync(cancellationToken).ConfigureAwait(false))
        {
            links.Add(ReadLink(entity));
        }

        return links;
    }

    public Task InsertLinkAsync(JoinLink link, CancellationToken cancellationToken) =>
        _links.UpsertAsync(WriteLink(link), cancellationToken);

    public Task UpdateLinkAsync(JoinLink link, CancellationToken cancellationToken) =>
        _links.UpsertAsync(WriteLink(link), cancellationToken);

    public async Task<AccessMember?> FindMemberAsync(string memberKey, CancellationToken cancellationToken)
    {
        var entity = await _members.TryGetAsync(memberKey, MemberRow, cancellationToken).ConfigureAwait(false);
        return entity is null ? null : ReadMember(entity);
    }

    public async Task<IReadOnlyList<AccessMember>> ListMembersAsync(CancellationToken cancellationToken)
    {
        var members = new List<AccessMember>();
        foreach (var entity in await _members.ListAsync(cancellationToken).ConfigureAwait(false))
        {
            members.Add(ReadMember(entity));
        }

        return members;
    }

    public Task UpsertMemberAsync(AccessMember member, CancellationToken cancellationToken) =>
        _members.UpsertAsync(WriteMember(member), cancellationToken);

    public async Task SaveRedeemAsync(JoinLink link, AccessMember member, CancellationToken cancellationToken)
    {
        await _links.UpsertAsync(WriteLink(link), cancellationToken).ConfigureAwait(false);
        await _members.UpsertAsync(WriteMember(member), cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_owned is null)
        {
            return;
        }

        if (_links is IDisposable links)
        {
            links.Dispose();
        }

        if (_members is IDisposable members)
        {
            members.Dispose();
        }

        if (_owned is IDisposable client)
        {
            client.Dispose();
        }
    }

    private static TableEntity WriteLink(JoinLink link)
    {
        var entity = new TableEntity(link.Token, LinkRow)
        {
            ["Id"] = link.Id.ToString("D"),
            ["CreatedAt"] = link.CreatedAt,
            ["ExpiresAt"] = link.ExpiresAt,
            ["MaxUses"] = link.MaxUses,
            ["Uses"] = link.Uses,
            ["Revoked"] = link.Revoked,
            ["CreatedBy"] = link.CreatedBy,
        };
        return entity;
    }

    private static TableEntity WriteMember(AccessMember member)
    {
        var entity = new TableEntity(member.MemberKey, MemberRow)
        {
            ["JoinedAt"] = member.JoinedAt,
            ["LinkId"] = member.LinkId.ToString("D"),
            ["Status"] = member.Status,
        };
        SetOptional(entity, "Email", member.Email);
        SetOptional(entity, "DisplayName", member.DisplayName);
        SetOptional(entity, "TenantId", member.TenantId);
        SetOptional(entity, "ObjectId", member.ObjectId);
        if (member.RevokedAt is { } revoked)
        {
            entity["RevokedAt"] = revoked;
        }

        return entity;
    }

    private static void SetOptional(TableEntity entity, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            entity[name] = value;
        }
    }

    private static JoinLink ReadLink(TableEntity entity) =>
        new(
            Guid.Parse(RequiredString(entity, "Id")),
            entity.PartitionKey,
            ReadTime(entity, "CreatedAt"),
            ReadTime(entity, "ExpiresAt"),
            ReadInt(entity, "MaxUses"),
            ReadInt(entity, "Uses"),
            ReadBool(entity, "Revoked"),
            RequiredString(entity, "CreatedBy"));

    private static AccessMember ReadMember(TableEntity entity)
    {
        var key = entity.PartitionKey;
        var tenant = ReadString(entity, "TenantId");
        var objectId = ReadString(entity, "ObjectId");
        if (string.IsNullOrWhiteSpace(objectId))
        {
            var split = key.Split(':', 2);
            if (split.Length == 2)
            {
                tenant ??= split[0];
                objectId = split[1];
            }
        }

        var status = ReadString(entity, "Status");
        if (string.IsNullOrWhiteSpace(status))
        {
            status = AccessMember.ActiveStatus;
        }

        DateTimeOffset? revoked = entity.ContainsKey("RevokedAt") ? ReadTime(entity, "RevokedAt") : null;
        return new AccessMember(
            key,
            ReadString(entity, "Email"),
            ReadString(entity, "DisplayName"),
            ReadTime(entity, "JoinedAt"),
            Guid.Parse(RequiredString(entity, "LinkId")),
            tenant,
            objectId,
            status,
            revoked);
    }

    private static string RequiredString(TableEntity entity, string name) =>
        ReadString(entity, name) ?? throw new InvalidOperationException("Access record is missing " + name + ".");

    private static string? ReadString(TableEntity entity, string name)
    {
        if (!entity.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static int ReadInt(TableEntity entity, string name)
    {
        if (!entity.TryGetValue(name, out var value) || value is null)
        {
            throw new InvalidOperationException("Access record is missing " + name + ".");
        }

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static bool ReadBool(TableEntity entity, string name)
    {
        if (!entity.TryGetValue(name, out var value) || value is null)
        {
            return false;
        }

        return value switch
        {
            bool flag => flag,
            int number => number != 0,
            long number => number != 0,
            string text => text is "1" or "true",
            _ => false,
        };
    }

    private static DateTimeOffset ReadTime(TableEntity entity, string name)
    {
        if (!entity.TryGetValue(name, out var value) || value is null)
        {
            throw new InvalidOperationException("Access record is missing " + name + ".");
        }

        return value switch
        {
            DateTimeOffset stamp => stamp,
            DateTime date => new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc)),
            string text => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            _ => throw new InvalidOperationException("Access record has an unreadable " + name + "."),
        };
    }
}

/// <summary>Azure Table client behind <see cref="IEntityTable"/>. Creates the table on first use.</summary>
public sealed class AzureEntityTable : IEntityTable, IDisposable
{
    private readonly TableClient _client;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _ready;

    public AzureEntityTable(TableClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    public async Task UpsertAsync(TableEntity entity, CancellationToken cancellationToken)
    {
        await EnsureAsync(cancellationToken).ConfigureAwait(false);
        await _client.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TableEntity?> TryGetAsync(string partitionKey, string rowKey, CancellationToken cancellationToken)
    {
        await EnsureAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var response = await _client.GetEntityAsync<TableEntity>(partitionKey, rowKey, cancellationToken: cancellationToken).ConfigureAwait(false);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<TableEntity>> ListAsync(CancellationToken cancellationToken)
    {
        await EnsureAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<TableEntity>();
        await foreach (var entity in _client.QueryAsync<TableEntity>(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            rows.Add(entity);
        }

        return rows;
    }

    public void Dispose() => _gate.Dispose();

    private async Task EnsureAsync(CancellationToken cancellationToken)
    {
        if (_ready)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_ready)
            {
                await _client.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
                _ready = true;
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}
