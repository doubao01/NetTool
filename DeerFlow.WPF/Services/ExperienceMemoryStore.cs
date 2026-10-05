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
/// 数据库采用 WAL 模式并复用连接，避免并发访问时的锁冲突。
/// </summary>
public class ExperienceMemoryStore : IExperienceMemoryStore, IDisposable
{
    private readonly string _connectionString;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, ExperienceMemoryItem> _cache = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private SqliteConnection? _connection;
    private bool _disposed;
    private const string DbFileName = "experiences.db";

    public ExperienceMemoryStore(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger("ExperienceMemory");

        // 数据目录优先使用可写的 LocalApplicationData，避免安装目录只读导致失败
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeerFlow.WPF");
        Directory.CreateDirectory(dataDir);

        _connectionString = $"Data Source={Path.Combine(dataDir, DbFileName)}";
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
            var connection = GetConnection();

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
            _writeLock.Release();
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
            var connection = GetConnection();

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
            var connection = GetConnection();

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
        var experience = GetExperience(id);
        if (experience is null)
        {
            return;
        }

        if (positive)
        {
            experience.ValidationCount++;
            experience.Confidence = Math.Min(1.0, experience.ValidationCount / 10.0);
        }
        else
        {
            experience.Confidence = Math.Max(0.0, experience.Confidence - 0.1);
        }

        _cache.AddOrUpdate(id, experience, (_, _) => experience);
        PersistConfidence(experience);

        _logger.LogInformation(
            "[ExperienceMemory] 更新置信度：{Id} - {Confidence}",
            id, experience.Confidence);
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

    private SqliteConnection GetConnection()
    {
        if (_connection is null)
        {
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();
        }

        return _connection;
    }

    /// <summary>
    /// 将置信度与验证次数写回数据库，保证重启后不丢失。
    /// </summary>
    private void PersistConfidence(ExperienceMemoryItem experience)
    {
        try
        {
            _writeLock.Wait();
            try
            {
                var connection = GetConnection();
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
            finally
            {
                _writeLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ExperienceMemory] 持久化置信度失败：{Id}", experience.Id);
        }
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
            _writeLock.Wait();
            try
            {
                var connection = GetConnection();
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
                _writeLock.Release();
            }
        });
    }

    private void InitializeDatabase()
    {
        try
        {
            var connection = GetConnection();

            // 启用 WAL 模式，提升并发读写能力并降低锁冲突
            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;";
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
                ? null
                : reader.GetString(reader.GetOrdinal("Type")),
            Tags = reader.GetString(reader.GetOrdinal("Tags")).Split(',').ToList(),
            Importance = (int)reader.GetDouble(reader.GetOrdinal("Importance")),
            ExperienceType = reader.GetString(reader.GetOrdinal("ExperienceType")),
            RelatedTaskId = reader.GetString(reader.GetOrdinal("RelatedTaskId")),
            Summary = reader.GetString(reader.GetOrdinal("Summary")),
            DetailedContent = reader.GetString(reader.GetOrdinal("DetailedContent")),
            Confidence = reader.GetDouble(reader.GetOrdinal("Confidence")),
            ValidationCount = reader.GetInt32(reader.GetOrdinal("ValidationCount")),
            IsApplied = reader.GetInt32(reader.GetOrdinal("IsApplied")) == 1,
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            LastAccessedAt = reader.IsDBNull(reader.GetOrdinal("LastAccessedAt"))
                ? DateTime.Now
                : reader.GetDateTime(reader.GetOrdinal("LastAccessedAt"))
        };
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
    }

    #endregion
}
