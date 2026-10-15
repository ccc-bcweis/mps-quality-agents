// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;

using Microsoft.Data.Sqlite;

using Mps.Bus;

namespace Mps.Api.Bus;

/// <summary>
/// Append-only event log plus a telemetry table. Readings are not copied into the log.
/// One connection, write-ahead logging, and a short busy wait so the request thread and the bus reader can share the file.
/// </summary>
public sealed class SqliteEventStore : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly object _gate = new();
    private readonly string _dataSource;
    private bool _ready;
    private bool _disposed;

    public SqliteEventStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var builder = new SqliteConnectionStringBuilder(connectionString);
        _dataSource = builder.DataSource;
        _connection = new SqliteConnection(builder.ConnectionString);
    }

    public void AppendLog(EventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        Ready();
        if (EventTypes.IsTelemetry(envelope.Type))
        {
            return;
        }

        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO eventlog(id, type, correlation, causation, parent, seq, site, tool, "time", payload)
                VALUES ($id, $type, $correlation, $causation, $parent, $seq, $site, $tool, $time, $payload)
                ON CONFLICT(id) DO NOTHING;
                """;
            BindIdentity(command, envelope);
            command.Parameters.AddWithValue("$payload", envelope.Payload.GetRawText());
            command.ExecuteNonQuery();
        }
    }

    public void AppendTelemetry(IReadOnlyList<EventEnvelope> envelopes)
    {
        ArgumentNullException.ThrowIfNull(envelopes);
        if (envelopes.Count == 0)
        {
            return;
        }

        Ready();
        lock (_gate)
        {
            using var transaction = _connection.BeginTransaction();
            using var command = _connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO telemetry(eventId, tool, parameter, "time", value, correlation, causation, parent, site, chamber, unit, low, high)
                VALUES ($id, $tool, $parameter, $time, $value, $correlation, $causation, $parent, $site, $chamber, $unit, $low, $high)
                ON CONFLICT(eventId) DO NOTHING;
                """;
            var id = command.Parameters.Add("$id", SqliteType.Text);
            var tool = command.Parameters.Add("$tool", SqliteType.Text);
            var parameter = command.Parameters.Add("$parameter", SqliteType.Text);
            var time = command.Parameters.Add("$time", SqliteType.Text);
            var value = command.Parameters.Add("$value", SqliteType.Real);
            var correlation = command.Parameters.Add("$correlation", SqliteType.Text);
            var causation = command.Parameters.Add("$causation", SqliteType.Text);
            var parent = command.Parameters.Add("$parent", SqliteType.Text);
            var site = command.Parameters.Add("$site", SqliteType.Text);
            var chamber = command.Parameters.Add("$chamber", SqliteType.Text);
            var unit = command.Parameters.Add("$unit", SqliteType.Text);
            var low = command.Parameters.Add("$low", SqliteType.Real);
            var high = command.Parameters.Add("$high", SqliteType.Real);
            foreach (var envelope in envelopes)
            {
                if (!EventTypes.IsTelemetry(envelope.Type) || string.IsNullOrWhiteSpace(envelope.ToolId))
                {
                    continue;
                }

                var name = PayloadReader.String(envelope.Payload, "parameter");
                var reading = PayloadReader.Number(envelope.Payload, "value");
                if (name is null || reading is null)
                {
                    continue;
                }

                id.Value = envelope.EventId;
                tool.Value = envelope.ToolId;
                parameter.Value = name;
                time.Value = Stamp(envelope.OccurredAt);
                value.Value = reading.Value;
                correlation.Value = envelope.CorrelationId;
                causation.Value = (object?)envelope.CausationId ?? DBNull.Value;
                parent.Value = (object?)envelope.ParentEventId ?? DBNull.Value;
                site.Value = (object?)envelope.SiteId ?? DBNull.Value;
                chamber.Value = (object?)envelope.ChamberId ?? DBNull.Value;
                unit.Value = (object?)PayloadReader.String(envelope.Payload, "unit") ?? DBNull.Value;
                low.Value = (object?)PayloadReader.Number(envelope.Payload, "low") ?? DBNull.Value;
                high.Value = (object?)PayloadReader.Number(envelope.Payload, "high") ?? DBNull.Value;
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    public void Clear()
    {
        Ready();
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM eventlog; DELETE FROM telemetry;";
            command.ExecuteNonQuery();
        }
    }

    public void ClearTelemetry()
    {
        Ready();
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM telemetry;";
            command.ExecuteNonQuery();
        }
    }

    public long CountTelemetry() => CountRows("SELECT COUNT(*) FROM telemetry;");

    public long CountLog() => CountRows("SELECT COUNT(*) FROM eventlog;");

    private long CountRows(string sql)
    {
        Ready();
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            var raw = command.ExecuteScalar();
            return raw is long value ? value : Convert.ToInt64(raw, CultureInfo.InvariantCulture);
        }
    }

    public StoredEvent? Find(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return null;
        }

        Ready();
        lock (_gate)
        {
            return FindLog(eventId.Trim()) ?? FindTelemetry(eventId.Trim());
        }
    }

    public StoredEvent? FindFirst(string correlationId, string type)
    {
        if (string.IsNullOrWhiteSpace(correlationId) || string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        Ready();
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText =
                """
                SELECT id, type, correlation, causation, parent, seq, site, tool, "time", payload
                FROM eventlog
                WHERE correlation = $correlation AND type = $type
                ORDER BY seq
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$correlation", correlationId);
            command.Parameters.AddWithValue("$type", type);
            using var reader = command.ExecuteReader();
            return reader.Read() ? ReadLog(reader) : null;
        }
    }

    public IReadOnlyList<StoredEvent> Children(string eventId, int limit)
    {
        if (string.IsNullOrWhiteSpace(eventId) || limit < 1)
        {
            return [];
        }

        Ready();
        lock (_gate)
        {
            var rows = new List<StoredEvent>();
            using (var command = _connection.CreateCommand())
            {
                command.CommandText =
                    """
                    SELECT id, type, correlation, causation, parent, seq, site, tool, "time", payload
                    FROM eventlog
                    WHERE parent = $id OR causation = $id
                    ORDER BY "time", seq
                    LIMIT $limit;
                    """;
                command.Parameters.AddWithValue("$id", eventId);
                command.Parameters.AddWithValue("$limit", limit);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    rows.Add(ReadLog(reader));
                }
            }

            if (rows.Count >= limit)
            {
                return rows;
            }

            using (var command = _connection.CreateCommand())
            {
                command.CommandText =
                    """
                    SELECT eventId, tool, parameter, "time", value, correlation, causation, parent, site, chamber, unit, low, high
                    FROM telemetry
                    WHERE parent = $id OR causation = $id
                    ORDER BY "time", eventId
                    LIMIT $limit;
                    """;
                command.Parameters.AddWithValue("$id", eventId);
                command.Parameters.AddWithValue("$limit", limit - rows.Count);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    rows.Add(ReadTelemetry(reader));
                }
            }

            return rows;
        }
    }

    public int ReadChart(string toolId, string parameter, DateTimeOffset from, DateTimeOffset until)
    {
        Ready();
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText =
                """
                SELECT "time", value
                FROM telemetry
                WHERE tool = $tool AND parameter = $parameter AND "time" >= $from AND "time" <= $until
                ORDER BY "time";
                """;
            command.Parameters.AddWithValue("$tool", toolId);
            command.Parameters.AddWithValue("$parameter", parameter);
            command.Parameters.AddWithValue("$from", Stamp(from));
            command.Parameters.AddWithValue("$until", Stamp(until));
            var count = 0;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                count++;
            }

            return count;
        }
    }

    public long DatabaseBytes()
    {
        long total = 0;
        if (File.Exists(_dataSource))
        {
            total += new FileInfo(_dataSource).Length;
        }

        var wal = _dataSource + "-wal";
        if (File.Exists(wal))
        {
            total += new FileInfo(wal).Length;
        }

        return total;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connection.Dispose();
    }

    private void Ready()
    {
        if (_ready)
        {
            return;
        }

        lock (_gate)
        {
            if (_ready)
            {
                return;
            }

            _connection.Open();
            using var pragma = _connection.CreateCommand();
            pragma.CommandText = "PRAGMA journal_mode=WAL;";
            pragma.ExecuteScalar();
            pragma.CommandText = "PRAGMA synchronous=NORMAL;";
            pragma.ExecuteNonQuery();
            pragma.CommandText = "PRAGMA busy_timeout=5000;";
            pragma.ExecuteNonQuery();
            EnsureSchema();
            _ready = true;
        }
    }

    private void EnsureSchema()
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS eventlog (
              id TEXT PRIMARY KEY,
              type TEXT NOT NULL,
              correlation TEXT NOT NULL,
              causation TEXT NULL,
              parent TEXT NULL,
              seq INTEGER NOT NULL,
              site TEXT NULL,
              tool TEXT NULL,
              "time" TEXT NOT NULL,
              payload TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_eventlog_correlation ON eventlog(correlation);
            CREATE INDEX IF NOT EXISTS ix_eventlog_causation ON eventlog(causation);
            CREATE INDEX IF NOT EXISTS ix_eventlog_tool_time ON eventlog(tool, "time");
            CREATE INDEX IF NOT EXISTS ix_eventlog_parent ON eventlog(parent);

            CREATE TABLE IF NOT EXISTS telemetry (
              eventId TEXT PRIMARY KEY,
              tool TEXT NOT NULL,
              parameter TEXT NOT NULL,
              "time" TEXT NOT NULL,
              value REAL NOT NULL,
              correlation TEXT NOT NULL,
              causation TEXT NULL,
              parent TEXT NULL,
              site TEXT NULL,
              chamber TEXT NULL,
              unit TEXT NULL,
              low REAL NULL,
              high REAL NULL
            );
            CREATE INDEX IF NOT EXISTS ix_telemetry_tool_parameter_time ON telemetry(tool, parameter, "time");
            CREATE INDEX IF NOT EXISTS ix_telemetry_parent ON telemetry(parent);
            CREATE INDEX IF NOT EXISTS ix_telemetry_causation ON telemetry(causation);
            """;
        command.ExecuteNonQuery();
    }

    private StoredEvent? FindLog(string eventId)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, type, correlation, causation, parent, seq, site, tool, "time", payload
            FROM eventlog WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", eventId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadLog(reader) : null;
    }

    private StoredEvent? FindTelemetry(string eventId)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            SELECT eventId, tool, parameter, "time", value, correlation, causation, parent, site, chamber, unit, low, high
            FROM telemetry WHERE eventId = $id;
            """;
        command.Parameters.AddWithValue("$id", eventId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadTelemetry(reader) : null;
    }

    private static void BindIdentity(SqliteCommand command, EventEnvelope envelope)
    {
        command.Parameters.AddWithValue("$id", envelope.EventId);
        command.Parameters.AddWithValue("$type", envelope.Type);
        command.Parameters.AddWithValue("$correlation", envelope.CorrelationId);
        command.Parameters.AddWithValue("$causation", (object?)envelope.CausationId ?? DBNull.Value);
        command.Parameters.AddWithValue("$parent", (object?)envelope.ParentEventId ?? DBNull.Value);
        command.Parameters.AddWithValue("$seq", envelope.Sequence);
        command.Parameters.AddWithValue("$site", (object?)envelope.SiteId ?? DBNull.Value);
        command.Parameters.AddWithValue("$tool", (object?)envelope.ToolId ?? DBNull.Value);
        command.Parameters.AddWithValue("$time", Stamp(envelope.OccurredAt));
    }

    private static StoredEvent ReadLog(SqliteDataReader reader) =>
        new(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            Optional(reader, 3),
            Optional(reader, 4),
            reader.GetInt64(5),
            Optional(reader, 6),
            Optional(reader, 7),
            null,
            ParseTime(reader.GetString(8)),
            reader.GetString(9),
            null,
            null,
            null,
            null,
            null,
            false);

    private static StoredEvent ReadTelemetry(SqliteDataReader reader) =>
        new(
            reader.GetString(0),
            EventTypes.TelemetryReading,
            reader.GetString(5),
            Optional(reader, 6),
            Optional(reader, 7),
            0,
            Optional(reader, 8),
            reader.GetString(1),
            Optional(reader, 9),
            ParseTime(reader.GetString(3)),
            "{}",
            reader.GetString(2),
            reader.GetDouble(4),
            OptionalDouble(reader, 11),
            OptionalDouble(reader, 12),
            Optional(reader, 10),
            true);

    private static string? Optional(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static double? OptionalDouble(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);

    private static DateTimeOffset ParseTime(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static string Stamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
}
