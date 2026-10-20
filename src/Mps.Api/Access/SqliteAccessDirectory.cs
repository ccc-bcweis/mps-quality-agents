// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

using Microsoft.Data.Sqlite;

namespace Mps.Api.Access;

/// <summary>Join links and members in the local SQLite file, outside the quality schema, so a reset leaves them.</summary>
public sealed class SqliteAccessDirectory : IAccessDirectory, IDisposable
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _schema = new(1, 1);
    private bool _ready;

    public SqliteAccessDirectory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public async Task<JoinLink?> FindLinkByTokenAsync(string token, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Token, CreatedAt, ExpiresAt, MaxUses, Uses, Revoked, CreatedBy FROM JoinLinks WHERE Token = $token";
        command.Parameters.AddWithValue("$token", token);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadLink(reader) : null;
    }

    public async Task<JoinLink?> FindLinkByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Token, CreatedAt, ExpiresAt, MaxUses, Uses, Revoked, CreatedBy FROM JoinLinks WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadLink(reader) : null;
    }

    public async Task<IReadOnlyList<JoinLink>> ListLinksAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Token, CreatedAt, ExpiresAt, MaxUses, Uses, Revoked, CreatedBy FROM JoinLinks";
        var links = new List<JoinLink>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            links.Add(ReadLink(reader));
        }

        return links;
    }

    public async Task InsertLinkAsync(JoinLink link, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await InsertLinkAsync(connection, null, link, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateLinkAsync(JoinLink link, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await UpdateLinkAsync(connection, null, link, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AccessMember?> FindMemberAsync(string memberKey, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MemberKey, Email, DisplayName, JoinedAt, LinkId, TenantId, ObjectId, Status, RevokedAt FROM Members WHERE MemberKey = $key";
        command.Parameters.AddWithValue("$key", memberKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadMember(reader) : null;
    }

    public async Task<IReadOnlyList<AccessMember>> ListMembersAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MemberKey, Email, DisplayName, JoinedAt, LinkId, TenantId, ObjectId, Status, RevokedAt FROM Members";
        var members = new List<AccessMember>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            members.Add(ReadMember(reader));
        }

        return members;
    }

    public async Task UpsertMemberAsync(AccessMember member, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await UpsertMemberAsync(connection, null, member, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveRedeemAsync(JoinLink link, AccessMember member, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await UpdateLinkAsync(connection, transaction, link, cancellationToken).ConfigureAwait(false);
        await UpsertMemberAsync(connection, transaction, member, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private async Task EnsureAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (_ready)
        {
            return;
        }

        await _schema.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_ready)
            {
                return;
            }

            await using (var create = connection.CreateCommand())
            {
                create.CommandText = """
                    CREATE TABLE IF NOT EXISTS JoinLinks (
                        Id TEXT PRIMARY KEY,
                        Token TEXT NOT NULL UNIQUE,
                        CreatedAt TEXT NOT NULL,
                        ExpiresAt TEXT NOT NULL,
                        MaxUses INTEGER NOT NULL,
                        Uses INTEGER NOT NULL,
                        Revoked INTEGER NOT NULL,
                        CreatedBy TEXT NOT NULL);
                    CREATE TABLE IF NOT EXISTS Members (
                        MemberKey TEXT PRIMARY KEY,
                        Email TEXT NULL,
                        DisplayName TEXT NULL,
                        JoinedAt TEXT NOT NULL,
                        LinkId TEXT NOT NULL,
                        TenantId TEXT NULL,
                        ObjectId TEXT NULL,
                        Status TEXT NOT NULL DEFAULT 'active',
                        RevokedAt TEXT NULL);
                    """;
                await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await AddColumnAsync(connection, "Members", "TenantId", "ALTER TABLE Members ADD COLUMN TenantId TEXT NULL", cancellationToken).ConfigureAwait(false);
            await AddColumnAsync(connection, "Members", "ObjectId", "ALTER TABLE Members ADD COLUMN ObjectId TEXT NULL", cancellationToken).ConfigureAwait(false);
            await AddColumnAsync(connection, "Members", "Status", "ALTER TABLE Members ADD COLUMN Status TEXT NOT NULL DEFAULT 'active'", cancellationToken).ConfigureAwait(false);
            await AddColumnAsync(connection, "Members", "RevokedAt", "ALTER TABLE Members ADD COLUMN RevokedAt TEXT NULL", cancellationToken).ConfigureAwait(false);
            _ready = true;
        }
        finally
        {
            _schema.Release();
        }
    }

    private static async Task AddColumnAsync(SqliteConnection connection, string table, string column, string sql, CancellationToken cancellationToken)
    {
        await using var probe = connection.CreateCommand();
        probe.CommandText = table switch
        {
            "Members" => "SELECT COUNT(1) FROM pragma_table_info('Members') WHERE name = $name",
            _ => throw new ArgumentOutOfRangeException(nameof(table), "Only Members grows new columns."),
        };
        probe.Parameters.AddWithValue("$name", column);
        var count = Convert.ToInt64(await probe.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
        if (count > 0)
        {
            return;
        }

        await using var alter = connection.CreateCommand();
        alter.CommandText = sql;
        await alter.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => _schema.Dispose();

    private static async Task InsertLinkAsync(SqliteConnection connection, SqliteTransaction? transaction, JoinLink link, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO JoinLinks (Id, Token, CreatedAt, ExpiresAt, MaxUses, Uses, Revoked, CreatedBy) VALUES ($id, $token, $created, $expires, $max, $uses, $revoked, $by)";
        BindLink(command, link);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpdateLinkAsync(SqliteConnection connection, SqliteTransaction? transaction, JoinLink link, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE JoinLinks SET ExpiresAt = $expires, MaxUses = $max, Uses = $uses, Revoked = $revoked WHERE Id = $id";
        command.Parameters.AddWithValue("$expires", Stamp(link.ExpiresAt));
        command.Parameters.AddWithValue("$max", link.MaxUses);
        command.Parameters.AddWithValue("$uses", link.Uses);
        command.Parameters.AddWithValue("$revoked", link.Revoked ? 1 : 0);
        command.Parameters.AddWithValue("$id", link.Id.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertMemberAsync(SqliteConnection connection, SqliteTransaction? transaction, AccessMember member, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Members (MemberKey, Email, DisplayName, JoinedAt, LinkId, TenantId, ObjectId, Status, RevokedAt)
            VALUES ($key, $email, $name, $at, $link, $tenant, $object, $status, $revoked)
            ON CONFLICT(MemberKey) DO UPDATE SET
                Email = excluded.Email,
                DisplayName = excluded.DisplayName,
                JoinedAt = excluded.JoinedAt,
                LinkId = excluded.LinkId,
                TenantId = excluded.TenantId,
                ObjectId = excluded.ObjectId,
                Status = excluded.Status,
                RevokedAt = excluded.RevokedAt
            """;
        command.Parameters.AddWithValue("$key", member.MemberKey);
        command.Parameters.AddWithValue("$email", (object?)member.Email ?? DBNull.Value);
        command.Parameters.AddWithValue("$name", (object?)member.DisplayName ?? DBNull.Value);
        command.Parameters.AddWithValue("$at", Stamp(member.JoinedAt));
        command.Parameters.AddWithValue("$link", member.LinkId.ToString("D"));
        command.Parameters.AddWithValue("$tenant", (object?)member.TenantId ?? DBNull.Value);
        command.Parameters.AddWithValue("$object", (object?)member.ObjectId ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", member.Status);
        command.Parameters.AddWithValue("$revoked", member.RevokedAt is { } revoked ? Stamp(revoked) : DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void BindLink(SqliteCommand command, JoinLink link)
    {
        command.Parameters.AddWithValue("$id", link.Id.ToString("D"));
        command.Parameters.AddWithValue("$token", link.Token);
        command.Parameters.AddWithValue("$created", Stamp(link.CreatedAt));
        command.Parameters.AddWithValue("$expires", Stamp(link.ExpiresAt));
        command.Parameters.AddWithValue("$max", link.MaxUses);
        command.Parameters.AddWithValue("$uses", link.Uses);
        command.Parameters.AddWithValue("$revoked", link.Revoked ? 1 : 0);
        command.Parameters.AddWithValue("$by", link.CreatedBy);
    }

    private static JoinLink ReadLink(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            Parse(reader.GetString(2)),
            Parse(reader.GetString(3)),
            reader.GetInt32(4),
            reader.GetInt32(5),
            reader.GetInt32(6) != 0,
            reader.GetString(7));

    private static AccessMember ReadMember(SqliteDataReader reader)
    {
        var key = reader.GetString(0);
        var tenant = reader.IsDBNull(5) ? null : reader.GetString(5);
        var objectId = reader.IsDBNull(6) ? null : reader.GetString(6);
        if (string.IsNullOrWhiteSpace(objectId))
        {
            var split = key.Split(':', 2);
            if (split.Length == 2)
            {
                tenant ??= split[0];
                objectId = split[1];
            }
        }

        var status = reader.IsDBNull(7) ? AccessMember.ActiveStatus : reader.GetString(7);
        if (string.IsNullOrWhiteSpace(status))
        {
            status = AccessMember.ActiveStatus;
        }

        return new AccessMember(
            key,
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            Parse(reader.GetString(3)),
            Guid.Parse(reader.GetString(4)),
            tenant,
            objectId,
            status,
            reader.IsDBNull(8) ? null : Parse(reader.GetString(8)));
    }

    private static string Stamp(DateTimeOffset value) => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset Parse(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
