using System.IO;
using DeerFlow.WPF.Services;
using Moq;
using Xunit;

namespace DeerFlow.WPF.Tests.Services;

public class SandboxManagerTests
{
    private readonly Mock<ILoggerService> _loggerMock;
    private readonly SandboxManager _sandboxManager;

    public SandboxManagerTests()
    {
        _loggerMock = new Mock<ILoggerService>();
        _sandboxManager = new SandboxManager(_loggerMock.Object);
    }

    [Fact]
    public async Task CreateSandbox_CreatesDirectory()
    {
        var taskId = $"test_{Guid.NewGuid():N}";
        var path = await _sandboxManager.CreateSandboxAsync(taskId);

        Assert.True(Directory.Exists(path));
        Assert.Contains(taskId, path);

        await _sandboxManager.DestroySandboxAsync(taskId);
    }

    [Fact]
    public async Task CreateSandbox_CreatesSubDirectories()
    {
        var taskId = $"test_{Guid.NewGuid():N}";
        var path = await _sandboxManager.CreateSandboxAsync(taskId);

        Assert.True(Directory.Exists(Path.Combine(path, "workspace")));
        Assert.True(Directory.Exists(Path.Combine(path, "temp")));
        Assert.True(Directory.Exists(Path.Combine(path, "output")));

        await _sandboxManager.DestroySandboxAsync(taskId);
    }

    [Fact]
    public async Task DestroySandbox_RemovesDirectory()
    {
        var taskId = $"test_{Guid.NewGuid():N}";
        var path = await _sandboxManager.CreateSandboxAsync(taskId);

        await _sandboxManager.DestroySandboxAsync(taskId);
        Assert.False(Directory.Exists(path));
    }

    [Fact]
    public async Task GetSandboxFiles_ReturnsEmpty_WhenNotExists()
    {
        var files = await _sandboxManager.GetSandboxFilesAsync("non_existent");
        Assert.Empty(files);
    }

    [Fact]
    public async Task ExecuteInSandbox_BlockedCommand_ReturnsError()
    {
        var taskId = $"test_{Guid.NewGuid():N}";
        await _sandboxManager.CreateSandboxAsync(taskId);

        var result = await _sandboxManager.ExecuteInSandboxAsync(taskId, "rm -rf /");
        Assert.Contains("不在白名单中", result);

        await _sandboxManager.DestroySandboxAsync(taskId);
    }

    [Fact]
    public async Task ExecuteInSandbox_BlocksAbsolutePathOutsideSandbox()
    {
        var taskId = $"test_{Guid.NewGuid():N}";
        await _sandboxManager.CreateSandboxAsync(taskId);

        var result = await _sandboxManager.ExecuteInSandboxAsync(taskId, @"type C:\Windows\System32\drivers\etc\hosts");
        Assert.Contains("禁止访问沙箱外路径", result);

        await _sandboxManager.DestroySandboxAsync(taskId);
    }

    [Fact]
    public async Task ExecuteInSandbox_BlocksPythonAndGit()
    {
        var taskId = $"test_{Guid.NewGuid():N}";
        await _sandboxManager.CreateSandboxAsync(taskId);

        var python = await _sandboxManager.ExecuteInSandboxAsync(taskId, "python script.py");
        var git = await _sandboxManager.ExecuteInSandboxAsync(taskId, "git status");
        Assert.Contains("不在白名单中", python);
        Assert.Contains("不在白名单中", git);

        await _sandboxManager.DestroySandboxAsync(taskId);
    }
}
