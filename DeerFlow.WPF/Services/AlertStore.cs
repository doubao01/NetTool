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
    private volatile bool _disposed;

    /// <summary>默认单次读取上限</summary>
    public const int DefaultLoadLimit = 200;

    /// <summary>启动时一次性加载的历史告警上限</summary>
    public const int StartupLoadLimit = 5000;

    public AlertStore(string? dbFilePath = null)
    {
        if (string.IsNullOrWhiteSpace(dbFilePath))
        {
            // 默认落盘位置：LocalApplicationData，避免安装目录只读导致失败
            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeerFlow.WPF");
            Directory.CreateDirectory(dataDir);
            dbFilePath = Path.Combine(dataDir, "alerts.db");
        }
        else
        {
            // 自定义路径可能是相对路径或多级目录，SQLite 不会自动创建父目录
            var fullPath = Path.GetFullPath(dbFilePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            dbFilePath = fullPath;
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbFilePath,
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
                    Type = excluded.Type,
                    Severity = excluded.Severity,
                    Title = excluded.Title,
                    Message = excluded.Message,
                    CreatedAt = excluded.CreatedAt,
                    IsAcknowledged = excluded.IsAcknowledged,
                    RelatedTaskId = excluded.RelatedTaskId,
                    Context = excluded.Context";
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
        // SQLite 把 LIMIT -1 当作不限制，负数/零会把整库读入内存，这里统一收敛
        if (limit <= 0)
        {
            limit = DefaultLoadLimit;
        }

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
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, object>();
            }

            var result = new Dictionary<string, object>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                result[property.Name] = ConvertElement(property.Value);
            }

            return result;
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// 把 JsonElement 还原为常见 CLR 基元类型，避免调用方拿到 JsonElement 后强转失败
    /// </summary>
    private static object ConvertElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Number => element.TryGetInt64(out var l) ? (object)l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
        _ => element.GetRawText()
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // 仅清理本库的连接池，避免 ClearAllPools 波及其它组件的 SQLite 连接
        using var connection = new SqliteConnection(_connectionString);
        SqliteConnection.ClearPool(connection);
    }
}
