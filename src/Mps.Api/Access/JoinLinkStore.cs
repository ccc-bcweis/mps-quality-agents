// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Security.Cryptography;

using Microsoft.Data.Sqlite;

namespace Mps.Api.Access;

public sealed record JoinLink(Guid Id, string Token, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, int MaxUses, int Uses, bool Revoked, string CreatedBy)
{
    public bool IsUsable(DateTimeOffset now) => !Revoked && now < ExpiresAt && Uses < MaxUses;
}

public sealed record DemoMember(string MemberKey, string? Email, string? DisplayName, DateTimeOffset JoinedAt, Guid LinkId);

public enum RedeemResult
{
    Joined,
    AlreadyMember,
    NotFound,
    Expired,
    UsedUp,
    Revoked,
}

/// <summary>
/// Join links and the members who redeemed them. Lives in the demo database but outside the quality schema, so a demo reset keeps it.
/// </summary>
public sealed class JoinLinkStore : IDisposable
{
    private readonly string _connectionString;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _ready;

    public JoinLinkStore(string connectionString, TimeProvider? time = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
        _time = time ?? TimeProvider.System;
    }

    public async Task<JoinLink> CreateAsync(TimeSpan lifetime, int maxUses, string createdBy, CancellationToken cancellationToken)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromDays(30))
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "A link lasts between a minute and 30 days.");
        }

        if (maxUses is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(maxUses), "A link allows 1 to 500 uses.");
        }

        var now = _time.GetUtcNow();
        var link = new JoinLink(Guid.NewGuid(), NewToken(), now, now + lifetime, maxUses, 0, false, createdBy);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO JoinLinks (Id, Token, CreatedAt, ExpiresAt, MaxUses, Uses, Revoked, CreatedBy) VALUES ($id, $token, $created, $expires, $max, 0, 0, $by)";
        command.Parameters.AddWithValue("$id", link.Id.ToString("D"));
        command.Parameters.AddWithValue("$token", link.Token);
        command.Parameters.AddWithValue("$created", Stamp(link.CreatedAt));
        command.Parameters.AddWithValue("$expires", Stamp(link.ExpiresAt));
        command.Parameters.AddWithValue("$max", link.MaxUses);
        command.Parameters.AddWithValue("$by", createdBy);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return link;
    }

    public async Task<IReadOnlyList<JoinLink>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Token, CreatedAt, ExpiresAt, MaxUses, Uses, Revoked, CreatedBy FROM JoinLinks ORDER BY CreatedAt DESC";
        var links = new List<JoinLink>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            links.Add(ReadLink(reader));
        }

        return links;
    }

    public async Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE JoinLinks SET Revoked = 1 WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    public async Task<bool> IsMemberAsync(string memberKey, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM DemoMembers WHERE MemberKey = $key";
        command.Parameters.AddWithValue("$key", memberKey);
        var count = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
        return count > 0;
    }

    public async Task<IReadOnlyList<DemoMember>> ListMembersAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MemberKey, Email, DisplayName, JoinedAt, LinkId FROM DemoMembers ORDER BY JoinedAt DESC";
        var members = new List<DemoMember>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            members.Add(new DemoMember(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                Parse(reader.GetString(3)),
                Guid.Parse(reader.GetString(4))));
        }

        return members;
    }

    /// <summary>Spends one use of the link and adds the caller as a member. Members who redeem again do not spend a use.</summary>
    public async Task<RedeemResult> RedeemAsync(string token, CallerIdentity caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var key = caller.MemberKey;
        if (string.IsNullOrWhiteSpace(token) || key is null)
        {
            return RedeemResult.NotFound;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            JoinLink? link = null;
            await using (var find = connection.CreateCommand())
            {
                find.CommandText = "SELECT Id, Token, CreatedAt, ExpiresAt, MaxUses, Uses, Revoked, CreatedBy FROM JoinLinks WHERE Token = $token";
                find.Parameters.AddWithValue("$token", token.Trim());
                await using var reader = await find.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    link = ReadLink(reader);
                }
            }

            if (link is null)
            {
                return RedeemResult.NotFound;
            }

            if (await IsMemberAsync(key, cancellationToken).ConfigureAwait(false))
            {
                return RedeemResult.AlreadyMember;
            }

            var now = _time.GetUtcNow();
            if (link.Revoked)
            {
                return RedeemResult.Revoked;
            }

            if (now >= link.ExpiresAt)
            {
                return RedeemResult.Expired;
            }

            if (link.Uses >= link.MaxUses)
            {
                return RedeemResult.UsedUp;
            }

            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (var spend = connection.CreateCommand())
            {
                spend.Transaction = transaction;
                spend.CommandText = "UPDATE JoinLinks SET Uses = Uses + 1 WHERE Id = $id AND Uses < MaxUses AND Revoked = 0";
                spend.Parameters.AddWithValue("$id", link.Id.ToString("D"));
                if (await spend.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 0)
                {
                    return RedeemResult.UsedUp;
                }
            }

            await using (var add = connection.CreateCommand())
            {
                add.Transaction = transaction;
                add.CommandText = "INSERT OR IGNORE INTO DemoMembers (MemberKey, Email, DisplayName, JoinedAt, LinkId) VALUES ($key, $email, $name, $at, $link)";
                add.Parameters.AddWithValue("$key", key);
                add.Parameters.AddWithValue("$email", (object?)caller.Email ?? DBNull.Value);
                add.Parameters.AddWithValue("$name", (object?)caller.DisplayName ?? DBNull.Value);
                add.Parameters.AddWithValue("$at", Stamp(now));
                add.Parameters.AddWithValue("$link", link.Id.ToString("D"));
                await add.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return RedeemResult.Joined;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!_ready)
        {
            await using var create = connection.CreateCommand();
            create.CommandText = """
                CREATE TABLE IF NOT EXISTS JoinLinks (Id TEXT PRIMARY KEY, Token TEXT NOT NULL UNIQUE, CreatedAt TEXT NOT NULL, ExpiresAt TEXT NOT NULL, MaxUses INTEGER NOT NULL, Uses INTEGER NOT NULL, Revoked INTEGER NOT NULL, CreatedBy TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS DemoMembers (MemberKey TEXT PRIMARY KEY, Email TEXT NULL, DisplayName TEXT NULL, JoinedAt TEXT NOT NULL, LinkId TEXT NOT NULL);
                """;
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _ready = true;
        }

        return connection;
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

    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    private static string Stamp(DateTimeOffset value) => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset Parse(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
