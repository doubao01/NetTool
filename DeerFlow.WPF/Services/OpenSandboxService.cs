using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenSandbox;
using OpenSandbox.Config;
using OpenSandbox.Core;
using OpenSandbox.Models;

namespace DeerFlow.WPF.Services;

/// <summary>
/// 沙箱编程语言枚举
/// </summary>
public enum SandboxLanguage
{
    Python,
    JavaScript,
    TypeScript,
    Go,
    Java,
    Bash
}

/// <summary>
/// OpenSandbox 服务接口,提供沙箱生命周期管理、代码执行和文件操作
/// </summary>
public interface IOpenSandboxService : IDisposable, IAsyncDisposable
{
    bool IsConnected { get; }
    string Status { get; }
    string SandboxId { get; }

    Task ConfigureAsync(string apiKey, string domain = "api.opensandbox.io", CancellationToken cancellationToken = default);
    Task CreateSandboxAsync(string image = "ubuntu", int timeoutSeconds = 600, CancellationToken cancellationToken = default);
    Task DestroyAsync(CancellationToken cancellationToken = default);

    Task<Execution> ExecuteCommandAsync(
        string command,
        bool background = false,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null,
        CancellationToken cancellationToken = default);

    Task<Execution> ExecuteCodeAsync(
        string code,
        SandboxLanguage language,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null,
        CancellationToken cancellationToken = default);

    Task<string> ReadFileAsync(string path, CancellationToken cancellationToken = default);
    Task WriteFileAsync(string path, string content, int mode = 644, CancellationToken cancellationToken = default);
    Task<SandboxInfo> GetInfoAsync(CancellationToken cancellationToken = default);
    Task RenewAsync(int timeoutSeconds = 3600, CancellationToken cancellationToken = default);
}

/// <summary>
/// OpenSandbox 服务实现
/// </summary>
public sealed class OpenSandboxService : IOpenSandboxService
{
    private ConnectionConfig? _config;
    private Sandbox? _sandbox;
    private readonly ILogger _logger;
    private string _status = "未连接";
    private string _sandboxId = string.Empty;

    public bool IsConnected => _sandbox is not null && _status == "运行中";

    public string Status
    {
        get => _status;
        private set => _status = value;
    }

    public string SandboxId
    {
        get => _sandboxId;
        private set => _sandboxId = value;
    }

    public OpenSandboxService(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger("OpenSandbox");
    }

    public async Task ConfigureAsync(string apiKey, string domain = "api.opensandbox.io", CancellationToken cancellationToken = default)
    {
        _config = new ConnectionConfig(new ConnectionConfigOptions
        {
            Domain = domain,
            ApiKey = apiKey,
            RequestTimeoutSeconds = 60
        });

        _logger.LogInformation("OpenSandbox 已配置: {Domain}", domain);
        Status = "已配置";
    }

    public async Task CreateSandboxAsync(string image = "ubuntu", int timeoutSeconds = 600, CancellationToken cancellationToken = default)
    {
        if (_config is null)
        {
            throw new InvalidOperationException("请先调用 ConfigureAsync 配置连接");
        }

        Status = "创建中...";
        _logger.LogInformation("创建沙箱: 镜像={Image}, 超时={Timeout}s", image, timeoutSeconds);

        _sandbox = await Sandbox.CreateAsync(new SandboxCreateOptions
        {
            ConnectionConfig = _config,
            Image = image,
            TimeoutSeconds = timeoutSeconds
        }, cancellationToken: cancellationToken);

        _sandboxId = _sandbox.Id ?? "未知";
        Status = "运行中";

        _logger.LogInformation("沙箱创建成功: ID={Id}", _sandboxId);
    }

    public async Task DestroyAsync(CancellationToken cancellationToken = default)
    {
        if (_sandbox is not null)
        {
            Status = "销毁中...";
            _logger.LogInformation("销毁沙箱: ID={Id}", _sandboxId);

            try
            {
                await _sandbox.KillAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "销毁沙箱时出错: {Id}", _sandboxId);
            }
            finally
            {
                _sandbox = null;
                Status = "未连接";
                _sandboxId = string.Empty;
            }
        }
    }

    public async Task<Execution> ExecuteCommandAsync(
        string command,
        bool background = false,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null,
        CancellationToken cancellationToken = default)
    {
        if (_sandbox is null)
        {
            throw new InvalidOperationException("沙箱未创建");
        }

        _logger.LogDebug("执行命令: {Command}", command);

        var handlers = new ExecutionHandlers
        {
            OnStdout = onStdout is not null
                ? msg => { onStdout(msg.Text); return Task.CompletedTask; }
                : null,
            OnStderr = onStderr is not null
                ? msg => { onStderr(msg.Text); return Task.CompletedTask; }
                : null,
            OnExecutionComplete = c =>
            {
                _logger.LogDebug("命令执行完成: 耗时 {Elapsed}ms", c.ExecutionTimeMs);
                return Task.CompletedTask;
            }
        };

        var options = new RunCommandOptions
        {
            Background = background,
            TimeoutSeconds = 120
        };

        return await _sandbox.Commands.RunAsync(
            command,
            handlers: handlers,
            options: options,
            cancellationToken: cancellationToken);
    }

    public async Task<Execution> ExecuteCodeAsync(
        string code,
        SandboxLanguage language,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null,
        CancellationToken cancellationToken = default)
    {
        if (_sandbox is null)
        {
            throw new InvalidOperationException("沙箱未创建");
        }

        _logger.LogDebug("执行代码: 语言={Language}", language);

        var languageString = language switch
        {
            SandboxLanguage.Python => "python",
            SandboxLanguage.JavaScript => "javascript",
            SandboxLanguage.TypeScript => "typescript",
            SandboxLanguage.Go => "go",
            SandboxLanguage.Java => "java",
            SandboxLanguage.Bash => "bash",
            _ => "python"
        };

        var wrappedCode = $"# language: {languageString}\n{code}";

        var handlers = new ExecutionHandlers
        {
            OnStdout = onStdout is not null
                ? msg => { onStdout(msg.Text); return Task.CompletedTask; }
                : null,
            OnStderr = onStderr is not null
                ? msg => { onStderr(msg.Text); return Task.CompletedTask; }
                : null
        };

        return await _sandbox.Commands.RunAsync(
            wrappedCode,
            handlers: handlers,
            cancellationToken: cancellationToken);
    }

    public async Task<string> ReadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        if (_sandbox is null)
        {
            throw new InvalidOperationException("沙箱未创建");
        }

        return await _sandbox.Files.ReadFileAsync(path, cancellationToken: cancellationToken);
    }

    public async Task WriteFileAsync(string path, string content, int mode = 644, CancellationToken cancellationToken = default)
    {
        if (_sandbox is null)
        {
            throw new InvalidOperationException("沙箱未创建");
        }

        await _sandbox.Files.WriteFilesAsync(new[]
        {
            new WriteEntry { Path = path, Data = content, Mode = mode }
        }, cancellationToken: cancellationToken);

        _logger.LogDebug("文件写入成功: {Path}", path);
    }

    public async Task<SandboxInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        if (_sandbox is null)
        {
            throw new InvalidOperationException("沙箱未创建");
        }

        return await _sandbox.GetInfoAsync(cancellationToken);
    }

    public async Task RenewAsync(int timeoutSeconds = 3600, CancellationToken cancellationToken = default)
    {
        if (_sandbox is null)
        {
            throw new InvalidOperationException("沙箱未创建");
        }

        await _sandbox.RenewAsync(timeoutSeconds, cancellationToken);
        _logger.LogInformation("沙箱续期: +{Seconds}s", timeoutSeconds);
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_sandbox is not null)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _sandbox.KillAsync(cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "释放沙箱时出错: {Id}", _sandboxId);
            }
            finally
            {
                _sandbox = null;
            }
        }
    }
}
