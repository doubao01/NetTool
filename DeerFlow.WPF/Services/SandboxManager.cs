using System.Diagnostics;
using System.IO;
using System.Text;

namespace DeerFlow.WPF.Services;

public interface ISandboxManager
{
    Task<string> CreateSandboxAsync(string taskId);
    Task DestroySandboxAsync(string taskId);
    Task<List<string>> GetSandboxFilesAsync(string taskId);
    Task<string> ExecuteInSandboxAsync(string taskId, string command);
}

public class SandboxManager : ISandboxManager
{
    private readonly ILoggerService _logger;
    private static readonly string SandboxRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeerFlow.WPF", "sandboxes");

    private static readonly HashSet<string> AllowedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "dir", "ls", "type", "cat", "echo", "mkdir", "findstr", "tree"
    };

    private const int CommandTimeoutMs = 8000;

    public SandboxManager(ILoggerService logger)
    {
        _logger = logger;
        Directory.CreateDirectory(SandboxRoot);
    }

    public Task<string> CreateSandboxAsync(string taskId)
    {
        var sandboxPath = ResolveSandboxPath(taskId);
        Directory.CreateDirectory(sandboxPath);
        Directory.CreateDirectory(Path.Combine(sandboxPath, "workspace"));
        Directory.CreateDirectory(Path.Combine(sandboxPath, "temp"));
        Directory.CreateDirectory(Path.Combine(sandboxPath, "output"));
        _logger.Info($"沙箱已创建: {sandboxPath}");
        return Task.FromResult(sandboxPath);
    }

    public Task DestroySandboxAsync(string taskId)
    {
        var sandboxPath = ResolveSandboxPath(taskId);
        if (Directory.Exists(sandboxPath))
        {
            try
            {
                Directory.Delete(sandboxPath, true);
                _logger.Info($"沙箱已销毁: {taskId}");
            }
            catch (Exception ex)
            {
                _logger.Error($"销毁沙箱失败: {taskId}", ex);
            }
        }

        return Task.CompletedTask;
    }

    public Task<List<string>> GetSandboxFilesAsync(string taskId)
    {
        var sandboxPath = ResolveSandboxPath(taskId);
        if (!Directory.Exists(sandboxPath))
        {
            return Task.FromResult(new List<string>());
        }

        var files = Directory.GetFiles(sandboxPath, "*.*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(sandboxPath, f))
            .ToList();
        return Task.FromResult(files);
    }

    public async Task<string> ExecuteInSandboxAsync(string taskId, string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return "命令为空";
        }

        var sandboxPath = ResolveSandboxPath(taskId);
        if (!Directory.Exists(sandboxPath))
        {
            return "沙箱不存在";
        }

        var tokens = SplitCommand(command);
        if (tokens.Count == 0)
        {
            return "命令为空";
        }

        var cmdBase = Path.GetFileNameWithoutExtension(tokens[0]);
        if (!AllowedCommands.Contains(cmdBase))
        {
            return $"命令 '{cmdBase}' 不在白名单中";
        }

        foreach (var token in tokens.Skip(1))
        {
            if (LooksLikeAbsolutePath(token) && !IsInsideSandbox(token, sandboxPath))
            {
                return "禁止访问沙箱外路径";
            }
        }

        _logger.Info($"沙箱执行命令: {taskId} -> {command}");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c " + command,
                WorkingDirectory = Path.Combine(sandboxPath, "workspace"),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                return "无法启动进程";
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            using var cts = new CancellationTokenSource(CommandTimeoutMs);
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return "命令超时已终止";
            }

            var output = (await stdoutTask).Trim();
            var error = (await stderrTask).Trim();
            var builder = new StringBuilder();
            builder.AppendLine($"Sandbox[{taskId}] > {command}");
            if (!string.IsNullOrEmpty(output))
            {
                builder.AppendLine(output);
            }
            if (!string.IsNullOrEmpty(error))
            {
                builder.AppendLine(error);
            }
            builder.Append($"exit={process.ExitCode}");
            return builder.ToString();
        }
        catch (Exception ex)
        {
            _logger.Error($"沙箱执行失败: {taskId}", ex);
            return $"执行失败: {ex.Message}";
        }
    }

    private static string ResolveSandboxPath(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId) ||
            taskId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            taskId.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("非法 taskId", nameof(taskId));
        }

        return Path.GetFullPath(Path.Combine(SandboxRoot, taskId));
    }

    private static bool LooksLikeAbsolutePath(string token)
    {
        if (token.StartsWith("~", StringComparison.Ordinal) ||
            token.StartsWith("/", StringComparison.Ordinal) ||
            token.StartsWith("\\", StringComparison.Ordinal))
        {
            return true;
        }

        return token.Length >= 2 && char.IsLetter(token[0]) && token[1] == ':';
    }

    private static bool IsInsideSandbox(string path, string sandboxPath)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetFullPath(sandboxPath);
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static List<string> SplitCommand(string command)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        foreach (var ch in command)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(ch);
            }
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }

        return result;
    }
}
