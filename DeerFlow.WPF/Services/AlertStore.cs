using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace DeerFlow.WPF.Services;

/// <summary>
/// 告警持久化存储（SQLite），避免应用重启后告警丢失。
/// </summary>
public class AlertStore : IDisposable
{
    private readonly string _connectionString;
    private readonly object _syncRoot = new();
    private bool _disposed;

    public AlertStore(string? dbFilePath = null)
    {
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeerFlow.WPF");
        Directory.CreateDirectory(dataDir);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbFilePath ?? Path.Combine(dataDir, "alerts.db"),
            DefaultTimeout = 30
        }.ToString();

        InitializeDatabase();
    }

    private SqliteConnection OpenConnection()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AlertStore));

        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=30000;";
        pragma.ExecuteNonQuery();

        return connection;
    }

    private void InitializeDatabase()
    {
        lock (_syncRoot)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Alerts (
                    Id TEXT PRIMARY KEY,
                    Type INTEGER NOT NULL,
                    Severity INTEGER NOT NULL,
                    Title TEXT NOT NULL,
                    Message TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    IsAcknowledged INTEGER NOT NULL DEFAULT 0,
                    RelatedTaskId TEXT,
                    Context TEXT
                )";
            command.ExecuteNonQuery();

            using var index = connection.CreateCommand();
            index.CommandText = "CREATE INDEX IF NOT EXISTS IX_Alerts_CreatedAt ON Alerts (CreatedAt DESC)";
            index.ExecuteNonQuery();
        }
    }

    /// <summary>保存或更新一条告警</summary>
    public void Upsert(Alert alert)
    {
        lock (_syncRoot)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Alerts (Id, Type, Severity, Title, Message, CreatedAt, IsAcknowledged, RelatedTaskId, Context)
                VALUES (@Id, @Type, @Severity, @Title, @Message, @CreatedAt, @IsAcknowledged, @RelatedTaskId, @Context)
                ON CONFLICT(Id) DO UPDATE SET
                    IsAcknowledged = excluded.IsAcknowledged";
            command.Parameters.AddWithValue("@Id", alert.Id);
            command.Parameters.AddWithValue("@Type", (int)alert.Type);
            command.Parameters.AddWithValue("@Severity", (int)alert.Severity);
            command.Parameters.AddWithValue("@Title", alert.Title);
            command.Parameters.AddWithValue("@Message", alert.Message);
            command.Parameters.AddWithValue("@CreatedAt", alert.CreatedAt.ToString("O"));
            command.Parameters.AddWithValue("@IsAcknowledged", alert.IsAcknowledged ? 1 : 0);
            command.Parameters.AddWithValue("@RelatedTaskId", alert.RelatedTaskId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Context", SerializeContext(alert.Context));
            command.ExecuteNonQuery();
        }
    }

    /// <summary>按时间倒序读取最近告警</summary>
    public List<Alert> Load(int limit = 200)
    {
        lock (_syncRoot)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Type, Severity, Title, Message, CreatedAt, IsAcknowledged, RelatedTaskId, Context
                FROM Alerts
                ORDER BY CreatedAt DESC
                LIMIT @Limit";
            command.Parameters.AddWithValue("@Limit", limit);

            using var reader = command.ExecuteReader();
            var alerts = new List<Alert>();
            while (reader.Read())
            {
                alerts.Add(new Alert
                {
                    Id = reader.GetString(0),
                    Type = (AlertType)reader.GetInt32(1),
                    Severity = (AlertSeverity)reader.GetInt32(2),
                    Title = reader.GetString(3),
                    Message = reader.GetString(4),
                    CreatedAt = DateTime.TryParse(reader.GetString(5), out var created) ? created : DateTime.Now,
                    IsAcknowledged = reader.GetInt32(6) == 1,
                    RelatedTaskId = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Context = DeserializeContext(reader.IsDBNull(8) ? null : reader.GetString(8))
                });
            }

            return alerts;
        }
    }

    /// <summary>删除指定告警</summary>
    public void Delete(string id)
    {
        lock (_syncRoot)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Alerts WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", id);
            command.ExecuteNonQuery();
        }
    }

    /// <summary>删除全部已确认告警</summary>
    public int DeleteAcknowledged()
    {
        lock (_syncRoot)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Alerts WHERE IsAcknowledged = 1";
            return command.ExecuteNonQuery();
        }
    }

    private static string SerializeContext(Dictionary<string, object> context)
    {
        try
        {
            return JsonSerializer.Serialize(context);
        }
        catch
        {
            return "{}";
        }
    }

    private static Dictionary<string, object> DeserializeContext(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, object>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                   ?? new Dictionary<string, object>();
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        SqliteConnection.ClearAllPools();
    }
}