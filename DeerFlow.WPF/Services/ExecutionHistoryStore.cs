using System.IO;
using Microsoft.Data.Sqlite;

namespace DeerFlow.WPF.Services;

/// <summary>
/// 一次编排运行的持久化记录
/// </summary>
public class ExecutionHistoryRecord
{
    public long Id { get; set; }

    /// <summary>工作流名称</summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>总体目标</summary>
    public string Goal { get; set; } = string.Empty;

    /// <summary>子目标列表（JSON 数组）</summary>
    public string SubGoalsJson { get; set; } = "[]";

    /// <summary>是否成功收敛的步骤数</summary>
    public int CompletedSteps { get; set; }

    /// <summary>总步骤数</summary>
    public int TotalSteps { get; set; }

    /// <summary>工具调用总次数</summary>
    public int ToolCallCount { get; set; }

    /// <summary>工具失败总次数</summary>
    public int ToolErrorCount { get; set; }

    /// <summary>总耗时（毫秒）</summary>
    public long TotalElapsedMs { get; set; }

    /// <summary>执行轨迹（逐行文本）</summary>
    public string ExecutionLog { get; set; } = string.Empty;

    /// <summary>结果汇总文本</summary>
    public string ResultSummary { get; set; } = string.Empty;

    /// <summary>结束状态（完成/取消/失败）</summary>
    public string Status { get; set; } = "完成";

    /// <summary>记录时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 编排执行历史存储（SQLite 持久化），供编排页回看历史运行
/// </summary>
public class ExecutionHistoryStore : IDisposable
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private SqliteConnection? _connection;
    private bool _disposed;

    public ExecutionHistoryStore(string? dbFilePath = null)
    {
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeerFlow.WPF");
        Directory.CreateDirectory(dataDir);

        _connectionString = $"Data Source={dbFilePath ?? Path.Combine(dataDir, "execution_history.db")}";
        InitializeDatabase();
    }

    /// <summary>
    /// 追加一条运行记录，返回数据库行 ID
    /// </summary>
    public async Task<long> AddAsync(ExecutionHistoryRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var connection = GetConnection();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO ExecutionHistory (
                    WorkflowName, Goal, SubGoalsJson, CompletedSteps, TotalSteps,
                    ToolCallCount, ToolErrorCount, TotalElapsedMs,
                    ExecutionLog, ResultSummary, Status, CreatedAt
                ) VALUES (
                    @WorkflowName, @Goal, @SubGoalsJson, @CompletedSteps, @TotalSteps,
                    @ToolCallCount, @ToolErrorCount, @TotalElapsedMs,
                    @ExecutionLog, @ResultSummary, @Status, @CreatedAt
                );
                SELECT last_insert_rowid();";

            command.Parameters.AddWithValue("@WorkflowName", record.WorkflowName);
            command.Parameters.AddWithValue("@Goal", record.Goal);
            command.Parameters.AddWithValue("@SubGoalsJson", record.SubGoalsJson);
            command.Parameters.AddWithValue("@CompletedSteps", record.CompletedSteps);
            command.Parameters.AddWithValue("@TotalSteps", record.TotalSteps);
            command.Parameters.AddWithValue("@ToolCallCount", record.ToolCallCount);
            command.Parameters.AddWithValue("@ToolErrorCount", record.ToolErrorCount);
            command.Parameters.AddWithValue("@TotalElapsedMs", record.TotalElapsedMs);
            command.Parameters.AddWithValue("@ExecutionLog", record.ExecutionLog);
            command.Parameters.AddWithValue("@ResultSummary", record.ResultSummary);
            command.Parameters.AddWithValue("@Status", record.Status);
            command.Parameters.AddWithValue("@CreatedAt", record.CreatedAt.ToString("O"));

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// 按时间倒序取最近的运行记录
    /// </summary>
    public IReadOnlyList<ExecutionHistoryRecord> GetRecent(int limit = 50)
    {
        var connection = GetConnection();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, WorkflowName, Goal, SubGoalsJson, CompletedSteps, TotalSteps,
                   ToolCallCount, ToolErrorCount, TotalElapsedMs,
                   ExecutionLog, ResultSummary, Status, CreatedAt
            FROM ExecutionHistory
            ORDER BY Id DESC
            LIMIT @Limit";
        command.Parameters.AddWithValue("@Limit", Math.Max(1, limit));

        var records = new List<ExecutionHistoryRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            records.Add(new ExecutionHistoryRecord
            {
                Id = reader.GetInt64(0),
                WorkflowName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                Goal = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                SubGoalsJson = reader.IsDBNull(3) ? "[]" : reader.GetString(3),
                CompletedSteps = reader.GetInt32(4),
                TotalSteps = reader.GetInt32(5),
                ToolCallCount = reader.GetInt32(6),
                ToolErrorCount = reader.GetInt32(7),
                TotalElapsedMs = reader.GetInt64(8),
                ExecutionLog = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                ResultSummary = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                Status = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                CreatedAt = reader.IsDBNull(12)
                    ? DateTime.MinValue
                    : DateTime.Parse(reader.GetString(12))
            });
        }

        return records;
    }

    /// <summary>
    /// 删除指定记录
    /// </summary>
    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var connection = GetConnection();

            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM ExecutionHistory WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", id);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// 记录总数
    /// </summary>
    public long Count()
    {
        var connection = GetConnection();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ExecutionHistory";
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private SqliteConnection GetConnection()
    {
        if (_connection is null)
        {
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();
            using var command = _connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL;";
            command.ExecuteNonQuery();
        }

        return _connection;
    }

    private void InitializeDatabase()
    {
        var connection = GetConnection();

        using var command = connection.CreateCommand();
        command.CommandText = @"
                CREATE TABLE IF NOT EXISTS ExecutionHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    WorkflowName TEXT NOT NULL DEFAULT '',
                    Goal TEXT NOT NULL DEFAULT '',
                    SubGoalsJson TEXT NOT NULL DEFAULT '[]',
                    CompletedSteps INTEGER NOT NULL DEFAULT 0,
                    TotalSteps INTEGER NOT NULL DEFAULT 0,
                    ToolCallCount INTEGER NOT NULL DEFAULT 0,
                    ToolErrorCount INTEGER NOT NULL DEFAULT 0,
                    TotalElapsedMs INTEGER NOT NULL DEFAULT 0,
                    ExecutionLog TEXT NOT NULL DEFAULT '',
                    ResultSummary TEXT NOT NULL DEFAULT '',
                    Status TEXT NOT NULL DEFAULT '',
                    CreatedAt TEXT NOT NULL DEFAULT ''
                );
                CREATE INDEX IF NOT EXISTS idx_execution_history_id
                    ON ExecutionHistory(Id DESC);";
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connection?.Dispose();
        _connection = null;
        _writeLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
