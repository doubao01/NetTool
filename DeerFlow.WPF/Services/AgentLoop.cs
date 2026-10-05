using System.Collections.Concurrent;
using DeerFlow.WPF.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace DeerFlow.WPF.Services;

/// <summary>
/// 智能体循环中的单步记录
/// </summary>
public class AgentStep
{
    /// <summary>步骤序号（从 1 开始）</summary>
    public int Index { get; set; }

    /// <summary>本步骤中模型产出的文本（思考 / 回答）</summary>
    public string Thought { get; set; } = string.Empty;

    /// <summary>本步骤触发的工具调用（插件.函数）</summary>
    public List<string> ToolCalls { get; set; } = new();

    /// <summary>工具执行返回的观察结果</summary>
    public List<string> Observations { get; set; } = new();

    /// <summary>本步骤耗时（毫秒）</summary>
    public long ElapsedMs { get; set; }

    /// <summary>本步骤结束时是否判定任务完成</summary>
    public bool IsFinal { get; set; }
}

/// <summary>
/// 智能体循环执行结果
/// </summary>
public class AgentLoopResult
{
    /// <summary>最终回答</summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>逐步执行轨迹</summary>
    public List<AgentStep> Steps { get; set; } = new();

    /// <summary>是否正常收敛</summary>
    public bool Completed { get; set; }

    /// <summary>结束原因：completed / max_iterations / no_progress / cancelled / error</summary>
    public string StopReason { get; set; } = string.Empty;

    /// <summary>总耗时（毫秒）</summary>
    public long TotalElapsedMs { get; set; }

    /// <summary>发生的错误信息（若有）</summary>
    public string? Error { get; set; }
}

/// <summary>
/// 智能体循环接口：以目标为输入，反复执行「推理 → 调用工具 → 观察结果」直至收敛
/// </summary>
public interface IAgentLoop
{
    /// <summary>
    /// 运行智能体循环
    /// </summary>
    /// <param name="goal">任务目标</param>
    /// <param name="config">智能体配置</param>
    /// <param name="progress">单步进度回调（每步完成后触发）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<AgentLoopResult> RunAsync(
        string goal,
        AgentConfig config,
        IProgress<AgentStep>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 以多个子智能体并发执行子目标，并发度受 <see cref="AgentConfig.MaxSubAgents"/> 限制
    /// </summary>
    Task<List<AgentLoopResult>> RunSubAgentsAsync(
        IReadOnlyList<string> subGoals,
        AgentConfig config,
        IProgress<AgentStep>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 智能体循环实现。
/// 使用 Semantic Kernel 的自动函数调用（FunctionChoiceBehavior.Auto）驱动工具调用，
/// 并以显式的多轮循环包裹：每轮结束后把工具观察结果回灌到对话历史，
/// 直到模型给出终止标记、达到最大轮数或触发无进展保护。
/// </summary>
public class AgentLoop : IAgentLoop
{
    /// <summary>模型判定任务完成时需输出的终止标记</summary>
    private const string DONE_MARKER = "[DONE]";

    /// <summary>默认最大轮数</summary>
    private const int DEFAULT_MAX_ITERATIONS = 8;

    /// <summary>瞬时失败重试次数</summary>
    private const int MAX_RETRY = 2;

    /// <summary>保护 Kernel 过滤器集合的并发访问锁</summary>
    private static readonly object FilterLock = new();

    private readonly Kernel? _kernel;
    private readonly ILoggerService _logger;

    public AgentLoop(ILoggerService logger, Kernel? kernel = null)
    {
        _logger = logger;
        _kernel = kernel;
    }

    /// <inheritdoc/>
    public async Task<AgentLoopResult> RunAsync(
        string goal,
        AgentConfig config,
        IProgress<AgentStep>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new AgentLoopResult();
        var totalWatch = System.Diagnostics.Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(goal))
        {
            result.StopReason = "error";
            result.Error = "任务目标为空";
            return result;
        }

        if (_kernel is null || !_kernel.GetAllServices<IChatCompletionService>().Any())
        {
            result.StopReason = "error";
            result.Error = "Semantic Kernel 或 LLM 服务未配置，无法运行智能体循环";
            _logger.Warn("智能体循环未启动：Kernel/IChatCompletionService 不可用");
            return result;
        }

        var maxIterations = ResolveMaxIterations(config);
        var transcript = new System.Text.StringBuilder();
        transcript.AppendLine($"任务目标：{goal.Trim()}");
        transcript.AppendLine();
        transcript.AppendLine("请按步骤推进任务。需要信息时调用可用工具；");
        transcript.AppendLine($"当且仅当任务已完成时，在回答末尾输出 {DONE_MARKER} 标记。");

        var lastObservation = string.Empty;
        var noProgressRounds = 0;

        for (var i = 1; i <= maxIterations; i++)
        {
            var step = new AgentStep { Index = i };
            var stepWatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var stepText = await InvokeWithRetryAsync(transcript.ToString(), config, step, cancellationToken)
                    .ConfigureAwait(false);
                step.ElapsedMs = stepWatch.ElapsedMilliseconds;
                step.Thought = stepText;

                if (stepText.Contains(DONE_MARKER, StringComparison.Ordinal))
                {
                    step.IsFinal = true;
                    result.Steps.Add(step);
                    progress?.Report(step);

                    result.Answer = stepText.Replace(DONE_MARKER, string.Empty).Trim();
                    result.Completed = true;
                    result.StopReason = "completed";
                    break;
                }

                // 将本轮的思考与工具观察回灌到上下文，形成下一轮的输入
                transcript.AppendLine($"[第 {i} 轮] 助手：{stepText}");
                foreach (var call in step.ToolCalls)
                {
                    transcript.AppendLine($"  · 调用工具：{call}");
                }
                foreach (var observation in step.Observations)
                {
                    transcript.AppendLine($"  · 工具返回：{Truncate(observation, 2000)}");
                }

                var observationFingerprint = string.Join("|", step.Observations);
                if (step.ToolCalls.Count == 0 && observationFingerprint == lastObservation)
                {
                    noProgressRounds++;
                }
                else
                {
                    noProgressRounds = 0;
                    lastObservation = observationFingerprint;
                }

                result.Steps.Add(step);
                result.Answer = stepText;
                progress?.Report(step);

                if (noProgressRounds >= 2)
                {
                    result.Completed = false;
                    result.StopReason = "no_progress";
                    _logger.Warn($"智能体循环连续 {noProgressRounds} 轮无进展，提前结束");
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                result.StopReason = "cancelled";
                result.Error = "任务已取消";
                _logger.Info($"智能体循环被取消（第 {i} 轮）");
                break;
            }
            catch (Exception ex)
            {
                step.ElapsedMs = stepWatch.ElapsedMilliseconds;
                result.Steps.Add(step);
                result.StopReason = "error";
                result.Error = ex.Message;
                _logger.Error($"智能体循环第 {i} 轮失败", ex);
                break;
            }
        }

        if (string.IsNullOrEmpty(result.StopReason))
        {
            result.StopReason = "max_iterations";
            _logger.Warn($"智能体循环达到最大轮数 {maxIterations}，未检测到完成标记");
        }

        totalWatch.Stop();
        result.TotalElapsedMs = totalWatch.ElapsedMilliseconds;
        _logger.Info($"智能体循环结束：reason={result.StopReason}, steps={result.Steps.Count}, {result.TotalElapsedMs}ms");
        return result;
    }

    /// <inheritdoc/>
    public async Task<List<AgentLoopResult>> RunSubAgentsAsync(
        IReadOnlyList<string> subGoals,
        AgentConfig config,
        IProgress<AgentStep>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (subGoals.Count == 0)
        {
            return new List<AgentLoopResult>();
        }

        var concurrency = Math.Clamp(config.MaxSubAgents, 1, Math.Max(1, subGoals.Count));
        using var gate = new SemaphoreSlim(concurrency, concurrency);
        var outcomes = new ConcurrentDictionary<int, AgentLoopResult>();

        _logger.Info($"启动 {subGoals.Count} 个子智能体，并发上限 {concurrency}");

        var workers = subGoals.Select((subGoal, index) => Task.Run(async () =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var subResult = await RunAsync(subGoal, config, progress, cancellationToken).ConfigureAwait(false);
                outcomes[index] = subResult;
            }
            finally
            {
                gate.Release();
            }
        }, cancellationToken));

        await Task.WhenAll(workers).ConfigureAwait(false);

        return Enumerable.Range(0, subGoals.Count)
            .Select(i => outcomes.GetValueOrDefault(i, new AgentLoopResult
            {
                StopReason = "error",
                Error = "子智能体未返回结果"
            }))
            .ToList();
    }

    /// <summary>
    /// 调用一次模型（含自动工具调用），并在失败时重试
    /// </summary>
    private async Task<string> InvokeWithRetryAsync(
        string prompt,
        AgentConfig config,
        AgentStep step,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        for (var attempt = 0; attempt <= MAX_RETRY; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await InvokeOnceAsync(prompt, config, step, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                _logger.Warn($"模型调用失败（第 {attempt + 1} 次）：{ex.Message}");

                if (attempt < MAX_RETRY)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(300 * (attempt + 1)), cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        throw lastError ?? new InvalidOperationException("模型调用失败且无异常信息");
    }

    /// <summary>
    /// 执行一次 Kernel 调用，自动调用工具，并通过观察过滤器采集函数调用与返回内容
    /// </summary>
    private async Task<string> InvokeOnceAsync(
        string prompt,
        AgentConfig config,
        AgentStep step,
        CancellationToken cancellationToken)
    {
        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: true),
            Temperature = config.Temperature,
            MaxTokens = config.MaxTokens
        };

        var arguments = new KernelArguments(settings)
        {
            ["input"] = prompt
        };

        var function = _kernel!.CreateFunctionFromPrompt(
            "{{$input}}",
            settings,
            functionName: "agent_step",
            description: "鹿流智能体单步执行");

        var observer = new StepObserver(step);
        var kernel = _kernel!;

        // AutoFunctionInvocationFilters 是共享 Kernel 上的可变集合，登记/注销需串行化
        lock (FilterLock)
        {
            kernel.AutoFunctionInvocationFilters.Add(observer);
        }

        try
        {
            var text = new System.Text.StringBuilder();

            await foreach (var chunk in kernel.InvokeStreamingAsync<StreamingKernelContent>(
                function, arguments, cancellationToken).ConfigureAwait(false))
            {
                text.Append(chunk.ToString());
            }

            return text.ToString().Trim();
        }
        finally
        {
            lock (FilterLock)
            {
                kernel.AutoFunctionInvocationFilters.Remove(observer);
            }
        }
    }

    /// <summary>
    /// 截断过长的观察文本，避免上下文无限膨胀
    /// </summary>
    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "...(截断)";

    /// <summary>
    /// 解析最大轮数：由任务超时时间推导，避免长时间占用
    /// </summary>
    private static int ResolveMaxIterations(AgentConfig config)
    {
        if (config.TimeoutMinutes <= 0)
        {
            return DEFAULT_MAX_ITERATIONS;
        }

        // 每轮预留约 30 秒，至少 2 轮、至多 20 轮
        var byTimeout = config.TimeoutMinutes * 60 / 30;
        return Math.Clamp(byTimeout, 2, 20);
    }

    /// <summary>
    /// 自动函数调用观察者：把每次工具调用的名称、参数与返回值写入当前步骤
    /// </summary>
    private sealed class StepObserver : IAutoFunctionInvocationFilter
    {
        private readonly AgentStep _step;

        public StepObserver(AgentStep step) => _step = step;

        public async Task OnAutoFunctionInvocationAsync(
            AutoFunctionInvocationContext context,
            Func<AutoFunctionInvocationContext, Task> next)
        {
            var function = context.Function;
            var name = function is null
                ? "unknown"
                : string.IsNullOrEmpty(function.PluginName)
                    ? function.Name
                    : $"{function.PluginName}.{function.Name}";

            var args = context.Arguments is null
                ? string.Empty
                : string.Join(", ", context.Arguments.Select(kv => $"{kv.Key}={kv.Value}"));

            _step.ToolCalls.Add(string.IsNullOrEmpty(args) ? name : $"{name}({args})");

            await next(context).ConfigureAwait(false);

            var observation = context.Result?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(observation))
            {
                _step.Observations.Add(observation);
            }
        }
    }
}
