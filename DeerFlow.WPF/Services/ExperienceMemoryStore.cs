using System.Collections.Concurrent;
using System.IO;
using DeerFlow.WPF.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;

namespace DeerFlow.WPF.Services;

/// <summary>
/// 经验记忆存储服务接口
/// </summary>
public interface IExperienceMemoryStore
{
    /// <summary>
    /// 保存经验记忆
    /// </summary>
    Task<string> SaveExperienceAsync(
        ExperienceMemoryItem experience,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检索相关经验（按重要度、置信度排序，支持分页）
    /// </summary>
    IEnumerable<ExperienceMemoryItem> RetrieveExperiences(
        string query,
        int limit = 10,
        int offset = 0);

    /// <summary>
    /// 获取经验记忆
    /// </summary>
    ExperienceMemoryItem? GetExperience(string id);

    /// <summary>
    /// 更新经验置信度
    /// </summary>
    void UpdateConfidence(string id, bool positive);

    /// <summary>
    /// 获取经验统计
    /// </summary>
    Dictionary<string, object> GetStatistics();

    /// <summary>
    /// 获取全部经验（按重要度、置信度排序，支持分页）
    /// </summary>
    IEnumerable<ExperienceMemoryItem> GetAllExperiences(int limit = 100, int offset = 0);
}

/// <summary>
/// 经验记忆存储服务 - 持久化存储经验和模式。
/// 数据库采用 WAL 模式，每次操作使用独立连接，由连接池复用；
/// 写操作串行化，读操作依赖 WAL 快照并发执行，避免共享连接跨线程访问。
/// </summary>
public class ExperienceMemoryStore : IExperienceMemoryStore, IDisposable
{
    private readonly string _connectionString;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, ExperienceMemoryItem> _cache = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private volatile bool _disposed;
    private const string DbFileName = "experiences.db";
    private const int BusyTimeoutSeconds = 30;

    public ExperienceMemoryStore(ILoggerFactory loggerFactory)
        : this(loggerFactory, null)
    {
    }

    /// <summary>
    /// 允许指定数据库文件路径，便于测试隔离与多实例部署。
    /// </summary>
    public ExperienceMemoryStore(ILoggerFactory loggerFactory, string? dbFilePath)
    {
        _logger = loggerFactory.CreateLogger("ExperienceMemory");

        if (string.IsNullOrWhiteSpace(dbFilePath))
        {
            // 数据目录优先使用可写的 LocalApplicationData，避免安装目录只读导致失败
            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeerFlow.WPF");
            Directory.CreateDirectory(dataDir);
            dbFilePath = Path.Combine(dataDir, DbFileName);
        }
        else
        {
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
            DefaultTimeout = BusyTimeoutSeconds
        }.ToString();
        InitializeDatabase();
    }

    /// <inheritdoc/>
    public async Task<string> SaveExperienceAsync(
        ExperienceMemoryItem experience,
        CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = OpenConnection();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Experiences (
                    Id, Content, Type, Tags, Importance, 
                    ExperienceType, RelatedTaskId, Summary, 
                    DetailedContent, Confidence, ValidationCount, 
                    CreatedAt
                ) VALUES (
                    @Id, @Content, @Type, @Tags, @Importance,
                    @ExperienceType, @RelatedTaskId, @Summary,
                    @DetailedContent, @Confidence, @ValidationCount,
                    @CreatedAt
                )";

            command.Parameters.AddWithValue("@Id", experience.Id);
            command.Parameters.AddWithValue("@Content", experience.Content);
            command.Parameters.AddWithValue("@Type", experience.Type ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Tags", string.Join(",", experience.Tags));
            command.Parameters.AddWithValue("@Importance", experience.Importance);
            command.Parameters.AddWithValue("@ExperienceType", experience.ExperienceType);
            command.Parameters.AddWithValue("@RelatedTaskId", experience.RelatedTaskId);
            command.Parameters.AddWithValue("@Summary", experience.Summary);
            command.Parameters.AddWithValue("@DetailedContent", experience.DetailedContent);
            command.Parameters.AddWithValue("@Confidence", experience.Confidence);
            command.Parameters.AddWithValue("@ValidationCount", experience.ValidationCount);
            command.Parameters.AddWithValue("@CreatedAt", experience.CreatedAt);

            await command.ExecuteNonQueryAsync(cancellationToken);

            _cache[experience.Id] = experience;
            _logger.LogInformation(
                "[ExperienceMemory] 保存经验：{Id} - {Summary}",
                experience.Id, experience.Summary);

            return experience.Id;
        }
        finally
        {
            ReleaseWriteLock();
        }
    }

    /// <inheritdoc/>
    public IEnumerable<ExperienceMemoryItem> RetrieveExperiences(
        string query,
        int limit = 10,
        int offset = 0)
    {
        if (limit <= 0)
        {
            return Enumerable.Empty<ExperienceMemoryItem>();
        }

        try
        {
            using var connection = OpenConnection();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM Experiences 
                WHERE Content LIKE @Query 
                   OR Summary LIKE @Query 
                   OR DetailedContent LIKE @Query
                ORDER BY Importance DESC, Confidence DESC, LastAccessedAt DESC
                LIMIT @Limit OFFSET @Offset";

            command.Parameters.AddWithValue("@Query", $"%{query}%");
            command.Parameters.AddWithValue("@Limit", limit);
            command.Parameters.AddWithValue("@Offset", offset);

            using var reader = command.ExecuteReader();
            var experiences = new List<ExperienceMemoryItem>();

            while (reader.Read())
            {
                var experience = ReadExperience(reader);
                experiences.Add(experience);
                _cache.AddOrUpdate(experience.Id, experience, (_, _) => experience);
            }

            _logger.LogInformation(
                "[ExperienceMemory] 检索到{Count}条相关经验",
                experiences.Count);

            // 异步持久化访问时间，不阻塞检索
            TouchAccessedAsync(experiences.Select(e => e.Id).ToList());

            return experiences;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ExperienceMemory] 检索经验失败");
            return Enumerable.Empty<ExperienceMemoryItem>();
        }
    }

    /// <inheritdoc/>
    public ExperienceMemoryItem? GetExperience(string id)
    {
        if (_cache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        try
        {
            using var connection = OpenConnection();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Experiences WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", id);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var experience = ReadExperience(reader);
                _cache.TryAdd(id, experience);
                return experience;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ExperienceMemory] 获取经验失败：{Id}", id);
        }

        return null;
    }

    /// <inheritdoc/>
    public void UpdateConfidence(string id, bool positive)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _writeLock.Wait();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            var source = LoadFromDatabase(id);
            if (source is null)
            {
                return;
            }

            var updated = CloneWithNewConfidence(source, positive);
            PersistConfidenceLocked(updated);
            _cache[id] = updated;

            _logger.LogInformation(
                "[ExperienceMemory] 更新置信度：{Id} - {Confidence}",
                id, updated.Confidence);
        }
        finally
        {
            ReleaseWriteLock();
        }
    }

    private void ReleaseWriteLock()
    {
        try
        {
            _writeLock.Release();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private ExperienceMemoryItem? LoadFromDatabase(string id)
    {
        try
        {
            using var connection = OpenConnection();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Experiences WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", id);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return ReadExperience(reader);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ExperienceMemory] 读取经验失败：{Id}", id);
        }

        return null;
    }

    private static ExperienceMemoryItem CloneWithNewConfidence(ExperienceMemoryItem source, bool positive)
    {
        var validationCount = positive ? source.ValidationCount + 1 : source.ValidationCount;
        var confidence = positive
            ? Math.Min(1.0, validationCount / 10.0)
            : Math.Max(0.0, source.Confidence - 0.1);

        return new ExperienceMemoryItem
        {
            Id = source.Id,
            Content = source.Content,
            Type = source.Type,
            Tags = new List<string>(source.Tags),
            Importance = source.Importance,
            CreatedAt = source.CreatedAt,
            LastAccessedAt = source.LastAccessedAt,
            ExperienceType = source.ExperienceType,
            RelatedTaskId = source.RelatedTaskId,
            Summary = source.Summary,
            DetailedContent = source.DetailedContent,
            ActionableTips = new List<string>(source.ActionableTips),
            Confidence = confidence,
            ValidationCount = validationCount,
            IsApplied = source.IsApplied
        };
    }

    /// <inheritdoc/>
    public Dictionary<string, object> GetStatistics()
    {
        return new Dictionary<string, object>
        {
            ["TotalExperiences"] = _cache.Count,
            ["ByType"] = _cache.Values
                .GroupBy(e => e.ExperienceType)
                .ToDictionary(g => g.Key, g => g.Count()),
            ["AverageConfidence"] = _cache.Count > 0
                ? _cache.Values.Average(e => e.Confidence)
                : 0,
            ["HighConfidenceCount"] = _cache.Values.Count(e => e.Confidence >= 0.8),
            ["AppliedCount"] = _cache.Values.Count(e => e.IsApplied)
        };
    }

    /// <inheritdoc/>
    public IEnumerable<ExperienceMemoryItem> GetAllExperiences(int limit = 100, int offset = 0)
    {
        return RetrieveExperiences(string.Empty, limit, offset);
    }

    #region Private Methods

    private SqliteConnection OpenConnection()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ExperienceMemoryStore));

        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = $"PRAGMA busy_timeout={BusyTimeoutSeconds * 1000};";
        pragma.ExecuteNonQuery();

        return connection;
    }

    /// <summary>
    /// 将置信度与验证次数写回数据库，保证重启后不丢失。
    /// 调用方必须已持有写锁。
    /// </summary>
    private void PersistConfidenceLocked(ExperienceMemoryItem experience)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Experiences
            SET Confidence = @Confidence,
                ValidationCount = @ValidationCount,
                LastAccessedAt = @LastAccessedAt
            WHERE Id = @Id";

        command.Parameters.AddWithValue("@Confidence", experience.Confidence);
        command.Parameters.AddWithValue("@ValidationCount", experience.ValidationCount);
        command.Parameters.AddWithValue("@LastAccessedAt", DateTime.Now);
        command.Parameters.AddWithValue("@Id", experience.Id);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 异步更新经验最后访问时间，写回数据库。
    /// </summary>
    private void TouchAccessedAsync(IReadOnlyList<string> ids)
    {
        if (ids.Count == 0)
        {
            return;
        }

        _ = Task.Run(() =>
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _writeLock.Wait();
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            try
            {
                if (_disposed)
                {
                    return;
                }

                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "UPDATE Experiences SET LastAccessedAt = @Now WHERE Id = @Id";
                var nowParam = command.Parameters.Add("@Now", SqliteType.Text);
                var idParam = command.Parameters.Add("@Id", SqliteType.Text);
                nowParam.Value = DateTime.Now;

                foreach (var id in ids)
                {
                    idParam.Value = id;
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ExperienceMemory] 更新访问时间失败");
            }
            finally
            {
                ReleaseWriteLock();
            }
        });
    }

    private void InitializeDatabase()
    {
        try
        {
            using var connection = OpenConnection();

            // 启用 WAL 模式，提升并发读写能力并降低锁冲突
            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
                pragma.ExecuteNonQuery();
            }

            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Experiences (
                    Id TEXT PRIMARY KEY,
                    Content TEXT NOT NULL,
                    Type TEXT,
                    Tags TEXT,
                    Importance REAL DEFAULT 0.5,
                    ExperienceType TEXT NOT NULL,
                    RelatedTaskId TEXT,
                    Summary TEXT,
                    DetailedContent TEXT,
                    Confidence REAL DEFAULT 1.0,
                    ValidationCount INTEGER DEFAULT 0,
                    IsApplied INTEGER DEFAULT 0,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    LastAccessedAt DATETIME
                )";

            command.ExecuteNonQuery();

            using var indexCommand = connection.CreateCommand();
            indexCommand.CommandText = @"
                CREATE INDEX IF NOT EXISTS IX_Experiences_Type 
                ON Experiences (ExperienceType)";
            indexCommand.ExecuteNonQuery();

            using var indexCommand2 = connection.CreateCommand();
            indexCommand2.CommandText = @"
                CREATE INDEX IF NOT EXISTS IX_Experiences_Confidence 
                ON Experiences (Confidence DESC)";
            indexCommand2.ExecuteNonQuery();

            _logger.LogInformation("[ExperienceMemory] 数据库初始化完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ExperienceMemory] 数据库初始化失败");
            throw;
        }
    }

    private static ExperienceMemoryItem ReadExperience(SqliteDataReader reader)
    {
        return new ExperienceMemoryItem
        {
            Id = reader.GetString(reader.GetOrdinal("Id")),
            Content = reader.GetString(reader.GetOrdinal("Content")),
            Type = reader.IsDBNull(reader.GetOrdinal("Type"))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal("Type")),
            Tags = reader.IsDBNull(reader.GetOrdinal("Tags"))
                ? new List<string>()
                : reader.GetString(reader.GetOrdinal("Tags")).Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            Importance = (int)reader.GetDouble(reader.GetOrdinal("Importance")),
            ExperienceType = reader.GetString(reader.GetOrdinal("ExperienceType")),
            RelatedTaskId = ReadNullableString(reader, "RelatedTaskId"),
            Summary = ReadNullableString(reader, "Summary"),
            DetailedContent = ReadNullableString(reader, "DetailedContent"),
            Confidence = reader.GetDouble(reader.GetOrdinal("Confidence")),
            ValidationCount = reader.GetInt32(reader.GetOrdinal("ValidationCount")),
            IsApplied = reader.GetInt32(reader.GetOrdinal("IsApplied")) == 1,
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            LastAccessedAt = reader.IsDBNull(reader.GetOrdinal("LastAccessedAt"))
                ? DateTime.Now
                : reader.GetDateTime(reader.GetOrdinal("LastAccessedAt"))
        };
    }

    private static string ReadNullableString(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // 等待进行中的写操作结束，避免异步任务在同步原语释放后继续执行
        try
        {
            if (_writeLock.Wait(TimeSpan.FromSeconds(BusyTimeoutSeconds)))
            {
                _writeLock.Release();
            }
        }
        catch (ObjectDisposedException)
        {
        }

        _writeLock.Dispose();
    }

    #endregion
}
