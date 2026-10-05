using System.IO;

namespace DeerFlow.WPF.Services;

/// <summary>
/// 日志服务接口
/// </summary>
public interface ILoggerService
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
    List<string> GetRecentLogs(int count = 50);
}

/// <summary>
/// 日志服务实现，输出到文件和控制台
/// </summary>
public class LoggerService : ILoggerService
{
    private readonly List<string> _recentLogs = new();
    private readonly string _logFilePath;
    private const int MAX_RECENT_LOGS = 200;
    private const int MAX_LOG_FILE_SIZE_MB = 10;

    public LoggerService()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeerFlow.WPF", "logs");

        Directory.CreateDirectory(logDir);

        _logFilePath = Path.Combine(logDir, $"deerflow_{DateTime.Now:yyyy-MM-dd}.log");

        CheckLogRotation();
    }

    /// <inheritdoc/>
    public void Info(string message)
    {
        WriteLog("INFO", message);
    }

    /// <inheritdoc/>
    public void Warn(string message)
    {
        WriteLog("WARN", message);
    }

    /// <inheritdoc/>
    public void Error(string message, Exception? ex = null)
    {
        var errorMsg = ex != null ? $"{message} | Exception: {ex.Message}" : message;
        WriteLog("ERROR", errorMsg);
    }

    /// <inheritdoc/>
    public List<string> GetRecentLogs(int count = 50)
    {
        lock (_recentLogs)
        {
            return _recentLogs.TakeLast(count).ToList();
        }
    }

    /// <summary>
    /// 写入日志条目
    /// </summary>
    private void WriteLog(string level, string message)
    {
        var logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}";

        lock (_recentLogs)
        {
            _recentLogs.Add(logEntry);
            while (_recentLogs.Count > MAX_RECENT_LOGS)
                _recentLogs.RemoveAt(0);
        }

        try
        {
            File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
        }
        catch (Exception ex)
        {
            // 日志写入失败不能影响主流程，仅输出到调试通道
            System.Diagnostics.Debug.WriteLine($"日志文件写入失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 检查日志文件大小并进行轮转
    /// </summary>
    private void CheckLogRotation()
    {
        if (File.Exists(_logFilePath))
        {
            var fileInfo = new FileInfo(_logFilePath);
            if (fileInfo.Length > MAX_LOG_FILE_SIZE_MB * 1024 * 1024)
            {
                var archivePath = _logFilePath.Replace(".log", $"_archive_{DateTime.Now:yyyyMMdd-HHmmss}.log");
                File.Move(_logFilePath, archivePath);
            }
        }
    }
}
