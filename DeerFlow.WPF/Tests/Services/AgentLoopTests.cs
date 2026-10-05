using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DeerFlow.WPF.Models;
using DeerFlow.WPF.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Xunit;

namespace DeerFlow.WPF.Tests.Services;

/// <summary>
/// 按脚本返回固定文本的假聊天补全服务，用于验证智能体循环的控制流。
/// 每次调用依次消费一条脚本；脚本耗尽后重复最后一条。
/// </summary>
public sealed class ScriptedChatCompletionService : IChatCompletionService
{
    private readonly Queue<string> _script;
    private string _last = "无脚本内容";

    public int InvocationCount { get; private set; }

    public ScriptedChatCompletionService(IEnumerable<string> script)
    {
        _script = new Queue<string>(script);
    }

    public IReadOnlyDictionary<string, object?> Attributes { get; } =
        new Dictionary<string, object?>();

    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        InvocationCount++;
        var text = Consume();
        IReadOnlyList<ChatMessageContent> result =
            new List<ChatMessageContent> { new(AuthorRole.Assistant, text) };
        return Task.FromResult(result);
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        InvocationCount++;
        var text = Consume();
        await Task.Yield();
        yield return new StreamingChatMessageContent(AuthorRole.Assistant, text);
    }

    private string Consume()
    {
        if (_script.Count > 0)
        {
            _last = _script.Dequeue();
        }

        return _last;
    }
}

public class AgentLoopTests
{
    private readonly MockLoggerService _logger = new();

    private static Kernel BuildKernel(IChatCompletionService chatService)
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatService);
        return builder.Build();
    }

    [Fact]
    public async Task RunAsync_StopsWhenDoneMarkerPresent()
    {
        var chat = new ScriptedChatCompletionService(new[]
        {
            "正在分析目标\n[DONE] 分析完成"
        });
        var loop = new AgentLoop(_logger, BuildKernel(chat));

        var result = await loop.RunAsync("分析代码", new AgentConfig());

        Assert.True(result.Completed);
        Assert.Equal("completed", result.StopReason);
        Assert.Contains("分析完成", result.Answer);
        Assert.DoesNotContain("[DONE]", result.Answer);
        Assert.Single(result.Steps);
        Assert.True(result.Steps[0].IsFinal);
        Assert.Equal(1, chat.InvocationCount);
    }

    [Fact]
    public async Task RunAsync_WithoutDoneMarker_ContinuesUntilNoProgress()
    {
        var chat = new ScriptedChatCompletionService(new[]
        {
            "尝试一",
            "尝试二",
            "尝试三",
            "尝试四"
        });
        var loop = new AgentLoop(_logger, BuildKernel(chat));

        var result = await loop.RunAsync("无法完成的任务", new AgentConfig());

        Assert.False(result.Completed);
        Assert.Equal("no_progress", result.StopReason);
        Assert.True(result.Steps.Count < 4, "无进展保护应提前终止循环");
    }

    [Fact]
    public async Task RunAsync_EmptyGoal_ReturnsError()
    {
        var chat = new ScriptedChatCompletionService(new[] { "[DONE]" });
        var loop = new AgentLoop(_logger, BuildKernel(chat));

        var result = await loop.RunAsync("   ", new AgentConfig());

        Assert.Equal("error", result.StopReason);
        Assert.Contains("为空", result.Error);
        Assert.Equal(0, chat.InvocationCount);
    }

    [Fact]
    public async Task RunAsync_WithoutKernel_ReturnsError()
    {
        var loop = new AgentLoop(_logger);

        var result = await loop.RunAsync("任意目标", new AgentConfig());

        Assert.Equal("error", result.StopReason);
        Assert.Contains("未配置", result.Error);
    }

    [Fact]
    public async Task RunAsync_Cancellation_StopsLoop()
    {
        var chat = new ScriptedChatCompletionService(new[] { "第一步", "第二步", "第三步" });
        var loop = new AgentLoop(_logger, BuildKernel(chat));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await loop.RunAsync("取消的任务", new AgentConfig(), null, cts.Token);

        Assert.Equal("cancelled", result.StopReason);
    }

    [Fact]
    public async Task RunSubAgentsAsync_ReturnsOneResultPerSubGoal()
    {
        var chat = new ScriptedChatCompletionService(new[]
        {
            "[DONE] 子任务完成"
        });
        var loop = new AgentLoop(_logger, BuildKernel(chat));
        var config = new AgentConfig { MaxSubAgents = 3 };

        var results = await loop.RunSubAgentsAsync(
            new[] { "子目标A", "子目标B", "子目标C" },
            config);

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.Completed));
    }

    [Fact]
    public async Task RunSubAgentsAsync_EmptyList_ReturnsEmpty()
    {
        var chat = new ScriptedChatCompletionService(new[] { "[DONE]" });
        var loop = new AgentLoop(_logger, BuildKernel(chat));

        var results = await loop.RunSubAgentsAsync(Array.Empty<string>(), new AgentConfig());

        Assert.Empty(results);
    }

    private sealed class MockLoggerService : ILoggerService
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? ex = null) { }
        public List<string> GetRecentLogs(int count = 50) => new();
    }
}
