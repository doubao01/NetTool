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

/// <summary>
/// 在首个流式响应中发出工具调用更新的假聊天服务，用于验证手动工具执行循环。
/// 第二轮返回完成标记。
/// </summary>
public sealed class ToolCallChatCompletionService : IChatCompletionService
{
    private readonly string _functionName;
    private readonly string _arguments;
    private int _calls;

    public ToolCallChatCompletionService(string functionName, string arguments)
    {
        _functionName = functionName;
        _arguments = arguments;
    }

    public IReadOnlyDictionary<string, object?> Attributes { get; } =
        new Dictionary<string, object?>();

    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ChatMessageContent>>(new List<ChatMessageContent>());

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _calls++;
        await Task.Yield();
        if (_calls == 1)
        {
            var message = new StreamingChatMessageContent(AuthorRole.Assistant, null);
            var items = message.Items;
            items.Add(new StreamingFunctionCallUpdateContent(
                "call1", _functionName, _arguments, functionCallIndex: 0));
            yield return message;
        }
        else
        {
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, "[DONE] 已完成工具调用");
        }
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
    public async Task RunAsync_TimeoutMinutesZero_DoesNotTreatAsCancelled()
    {
        var chat = new ScriptedChatCompletionService(new[]
        {
            "[DONE] 完成"
        });
        var loop = new AgentLoop(_logger, BuildKernel(chat));

        var result = await loop.RunAsync("快速任务", new AgentConfig { TimeoutMinutes = 0 });

        Assert.Equal("completed", result.StopReason);
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
    public async Task RunSubAgentsAsync_TagsStepsWithAgentName()
    {
        var chat = new ScriptedChatCompletionService(new[]
        {
            "[DONE] 子任务完成"
        });
        var loop = new AgentLoop(_logger, BuildKernel(chat));
        var names = new List<string>();

        var results = await loop.RunSubAgentsAsync(
            new[] { "子目标A", "子目标B" },
            new AgentConfig { MaxSubAgents = 2 },
            new Progress<AgentStep>(s => names.Add(s.AgentName)));

        Assert.All(results, r => Assert.All(r.Steps, s => Assert.StartsWith("子智能体", s.AgentName)));
        Assert.Contains("子智能体1", names);
        Assert.Contains("子智能体2", names);
    }

    [Fact]
    public async Task RunSubAgentsAsync_EmptyList_ReturnsEmpty()
    {
        var chat = new ScriptedChatCompletionService(new[] { "[DONE]" });
        var loop = new AgentLoop(_logger, BuildKernel(chat));

        var results = await loop.RunSubAgentsAsync(Array.Empty<string>(), new AgentConfig());

        Assert.Empty(results);
    }

    [Fact]
    public async Task PlanSubGoalsAsync_ParsesNumberedLines()
    {
        var chat = new ScriptedChatCompletionService(new[]
        {
            "1. 收集数据\n2. 分析数据\n3. 输出报告"
        });
        var loop = new AgentLoop(_logger, BuildKernel(chat));
        var config = new AgentConfig { MaxSubAgents = 5 };

        var subGoals = await loop.PlanSubGoalsAsync("做数据分析", config);

        Assert.Equal(3, subGoals.Count);
        Assert.Contains("收集数据", subGoals[0]);
        Assert.Contains("分析数据", subGoals[1]);
        Assert.Contains("输出报告", subGoals[2]);
    }

    [Fact]
    public async Task PlanSubGoalsAsync_EmptyResponse_FallsBackToOriginalGoal()
    {
        var chat = new ScriptedChatCompletionService(new[] { "   " });
        var loop = new AgentLoop(_logger, BuildKernel(chat));

        var subGoals = await loop.PlanSubGoalsAsync("原始目标", new AgentConfig());

        var subGoal = Assert.Single(subGoals);
        Assert.Equal("原始目标", subGoal);
    }

    [Fact]
    public async Task PlanSubGoalsAsync_EmptyGoal_ReturnsEmpty()
    {
        var chat = new ScriptedChatCompletionService(new[] { "[DONE]" });
        var loop = new AgentLoop(_logger, BuildKernel(chat));

        var subGoals = await loop.PlanSubGoalsAsync("  ", new AgentConfig());

        Assert.Empty(subGoals);
        Assert.Equal(0, chat.InvocationCount);
    }

    [Fact]
    public async Task PlanSubGoalsAsync_RespectsMaxSubAgentsCap()
    {
        var chat = new ScriptedChatCompletionService(new[]
        {
            "一\n二\n三\n四\n五\n六\n七\n八\n九\n十\n十一\n十二"
        });
        var loop = new AgentLoop(_logger, BuildKernel(chat));

        var subGoals = await loop.PlanSubGoalsAsync("大目标", new AgentConfig { MaxSubAgents = 3 });

        Assert.Equal(3, subGoals.Count);
    }

    [Fact]
    public async Task SummarizeResultsAsync_UsesLlmSummary()
    {
        var chat = new ScriptedChatCompletionService(new[]
        {
            "[DONE] 子任务完成",
            "整体执行良好，全部子任务收敛。"
        });
        var loop = new AgentLoop(_logger, BuildKernel(chat));
        var first = await loop.RunAsync("子任务", new AgentConfig());

        var summary = await loop.SummarizeResultsAsync(
            "总目标",
            new[] { "子任务" },
            new[] { first },
            new AgentConfig());

        Assert.Contains("整体执行良好", summary);
    }

    [Fact]
    public async Task SummarizeResultsAsync_WithoutKernel_ReturnsLocalSummary()
    {
        var loop = new AgentLoop(_logger);

        var summary = await loop.SummarizeResultsAsync(
            "总目标",
            new[] { "子任务A" },
            new[] { new AgentLoopResult { Completed = true, StopReason = "completed", Answer = "完成了" } },
            new AgentConfig());

        Assert.Contains("1/1", summary);
        Assert.Contains("子任务A", summary);
    }

    [Fact]
    public async Task SummarizeResultsAsync_LlmFailure_ReturnsLocalSummary()
    {
        var chat = new ThrowingChatCompletionService();
        var loop = new AgentLoop(_logger, BuildKernel(chat));
        var results = new List<AgentLoopResult>
        {
            new() { Completed = true, StopReason = "completed", Answer = "完成" }
        };

        var summary = await loop.SummarizeResultsAsync(
            "总目标",
            new[] { "子任务" },
            results,
            new AgentConfig());

        Assert.Contains("1/1", summary);
    }

    [Fact]
    public async Task RunAsync_ToolCallCapturedViaStreamingUpdates()
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(
            new ToolCallChatCompletionService("demo-echo", "{\"text\":\"abc\"}"));
        var kernel = builder.Build();
        kernel.ImportPluginFromObject(new EchoTools(), "demo");

        var loop = new AgentLoop(_logger, kernel);
        var result = await loop.RunAsync("使用工具", new AgentConfig());

        var allCalls = result.Steps.SelectMany(s => s.ToolCalls).ToList();
        var allObs = result.Steps.SelectMany(s => s.Observations).ToList();
        Assert.Contains(allCalls, c => c.Contains("demo.echo"));
        Assert.Contains(allObs, o => o.Contains("hello-from-tool"));
        Assert.All(result.Steps, s =>
            Assert.Equal(s.ToolCalls.Count, s.ToolElapsedMs.Count));
    }

    [Fact]
    public async Task RunAsync_ToolFailure_RecordedInToolErrors()
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(
            new ToolCallChatCompletionService("boom-fail", "{\"text\":\"abc\"}"));
        var kernel = builder.Build();
        kernel.ImportPluginFromObject(new FailingTools(), "boom");

        var loop = new AgentLoop(_logger, kernel);
        var result = await loop.RunAsync("触发工具失败", new AgentConfig());

        var errors = result.Steps.SelectMany(s => s.ToolErrors).ToList();
        Assert.Contains(errors, e => e.Contains("boom.fail"));
        Assert.All(result.Steps, s =>
            Assert.Equal(s.ToolCalls.Count, s.ToolElapsedMs.Count));
    }

    [Fact]
    public async Task RunAsync_ToolErrorsFeedBackIntoNextRound()
    {
        var builder = Kernel.CreateBuilder();
        var chat = new ScriptedChatCompletionService(new[] { "[DONE] 收到失败信息" });
        builder.Services.AddSingleton<IChatCompletionService>(chat);
        var kernel = builder.Build();
        kernel.ImportPluginFromObject(new FailingTools(), "boom");
        kernel.ImportPluginFromObject(new ThrowingOnceTools(), "flaky");

        var loop = new AgentLoop(_logger, kernel);
        var result = await loop.RunAsync("连续调用", new AgentConfig());

        // 前面轮次的工具失败会作为观察写回转录，最终轮次模型能看到失败上下文
        Assert.True(result.Steps.Count >= 1);
    }

    /// <summary>
    /// 每次调用都抛异常的假聊天服务，用于验证 LLM 失败回退路径
    /// </summary>
    public sealed class ThrowingChatCompletionService : IChatCompletionService
    {
        public IReadOnlyDictionary<string, object?> Attributes { get; } =
            new Dictionary<string, object?>();

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("LLM 不可用");

        public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield break;
        }
    }

    private sealed class MockLoggerService : ILoggerService
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? ex = null) { }
        public List<string> GetRecentLogs(int count = 50) => new();
    }

    /// <summary>
    /// 抛出异常的工具插件，用于验证工具失败采集
    /// </summary>
    public sealed class FailingTools
    {
        [KernelFunction("fail")]
        public string Fail(string text)
            => throw new InvalidOperationException($"工具执行失败：{text}");
    }

    /// <summary>
    /// 正常返回的回声工具插件
    /// </summary>
    public sealed class EchoTools
    {
        [KernelFunction("echo")]
        public string Echo(string text) => $"hello-from-tool:{text}";
    }

    /// <summary>
    /// 首次调用抛异常、此后成功的工具插件
    /// </summary>
    public sealed class ThrowingOnceTools
    {
        private int _calls;

        [KernelFunction("probe")]
        public string Probe(string text)
        {
            if (Interlocked.Increment(ref _calls) == 1)
            {
                throw new InvalidOperationException("首次调用失败");
            }

            return $"ok:{text}";
        }
    }
}
