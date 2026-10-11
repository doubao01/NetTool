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

    /// <summary>产生该步骤的智能体名称（子智能体场景非空）</summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>本步骤中模型产出的文本（思考 / 回答）</summary>
    public string Thought { get; set; } = string.Empty;

    /// <summary>本步骤触发的工具调用（插件.函数）</summary>
    public List<string> ToolCalls { get; set; } = new();

    /// <summary>工具执行返回的观察结果</summary>
    public List<string> Observations { get; set; } = new();

    /// <summary>工具执行失败信息（插件.函数 → 异常消息）</summary>
    public List<string> ToolErrors { get; set; } = new();

    /// <summary>每个工具调用的执行耗时（毫秒，与 ToolCalls 顺序对应）</summary>
    public List<long> ToolElapsedMs { get; set; } = new();

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

    /// <summary>
    /// 由总目标让 LLM 分解出编号子目标；分解失败时返回只含原目标的列表
    /// </summary>
    Task<List<string>> PlanSubGoalsAsync(
        string goal,
        AgentConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 由 LLM 汇总各子任务的执行结果；LLM 不可用时回退为本地拼接摘要
    /// </summary>
    Task<string> SummarizeResultsAsync(
        string goal,
        IReadOnlyList<string> subGoals,
        IReadOnlyList<AgentLoopResult> results,
        AgentConfig config,
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

    /// <summary>任务分解允许的最大子目标数</summary>
    private const int MaxPlanSubGoals = 10;

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
        => await RunNamedAsync(string.Empty, goal, config, progress, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// 以指定智能体名称运行循环；名称用于并发子智能体场景下区分步骤轨迹来源
    /// </summary>
    private async Task<AgentLoopResult> RunNamedAsync(
        string agentName,
        string goal,
        AgentConfig config,
        IProgress<AgentStep>? progress,
        CancellationToken cancellationToken)
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
        var consecutiveToolFailures = 0;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (config.TimeoutMinutes > 0)
        {
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(config.TimeoutMinutes));
        }
        var runToken = timeoutCts.Token;

        for (var i = 1; i <= maxIterations; i++)
        {
            var step = new AgentStep { Index = i, AgentName = agentName };
            var stepWatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                runToken.ThrowIfCancellationRequested();

                var stepText = await InvokeWithRetryAsync(transcript.ToString(), config, step, runToken)
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
                foreach (var toolError in step.ToolErrors)
                {
                    transcript.AppendLine($"  · 工具失败：{Truncate(toolError, 500)}");
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

                if (step.ToolErrors.Count > 0)
                {
                    consecutiveToolFailures += step.ToolErrors.Count;
                }
                else if (step.ToolCalls.Count > 0)
                {
                    consecutiveToolFailures = 0;
                }

                var failureLimit = Math.Max(1, config.MaxConsecutiveToolFailures);
                if (consecutiveToolFailures >= failureLimit)
                {
                    result.Completed = false;
                    result.StopReason = "tool_circuit_open";
                    result.Error = $"连续工具失败 {consecutiveToolFailures} 次，已熔断";
                    _logger.Warn(result.Error);
                    break;
                }

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
                if (cancellationToken.IsCancellationRequested)
                {
                    result.StopReason = "cancelled";
                    result.Error = "任务已取消";
                }
                else
                {
                    result.StopReason = "timeout";
                    result.Error = $"超过 {config.TimeoutMinutes} 分钟超时";
                }
                _logger.Info($"智能体循环结束（第 {i} 轮）：{result.StopReason}");
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
                var agentName = $"子智能体{index + 1}";
                var subResult = await RunNamedAsync(agentName, subGoal, config, progress, cancellationToken)
                    .ConfigureAwait(false);
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

    /// <inheritdoc/>
    public async Task<List<string>> PlanSubGoalsAsync(
        string goal,
        AgentConfig config,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(goal))
        {
            return new List<string>();
        }

        if (_kernel is null || !_kernel.GetAllServices<IChatCompletionService>().Any())
        {
            _logger.Warn("任务分解未执行：Kernel/IChatCompletionService 不可用，返回原目标");
            return new List<string> { goal.Trim() };
        }

        var maxSubAgents = Math.Clamp(config.MaxSubAgents, 1, MaxPlanSubGoals);
        var prompt = $@"你是任务规划器。请把下面这个总目标分解为 {maxSubAgents} 个以内、可独立执行的子目标。
要求：
- 每行一个子目标，不要编号，不要输出任何其他内容
- 子目标必须具体、可验证，且合在一起能覆盖总目标

总目标：{goal.Trim()}";

        try
        {
            var chat = _kernel.GetRequiredService<IChatCompletionService>();
            var settings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.2,
                MaxTokens = config.MaxTokens
            };

            var response = await chat.GetChatMessageContentsAsync(
                new ChatHistory(prompt), settings, _kernel, cancellationToken).ConfigureAwait(false);
            var text = response.Count > 0 ? response[^1].Content ?? string.Empty : string.Empty;

            var subGoals = text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim().TrimStart('-', '*', '•', ' ', '\t'))
                .Select(line => System.Text.RegularExpressions.Regex.Replace(line, @"^\d+[.、:：]\s*", string.Empty))
                .Where(line => line.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(maxSubAgents)
                .ToList();

            if (subGoals.Count == 0)
            {
                _logger.Warn("任务分解结果为空，回退为原目标");
                return new List<string> { goal.Trim() };
            }

            _logger.Info($"任务分解完成：{goal.Trim()} → {subGoals.Count} 个子目标");
            return subGoals;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error("任务分解失败，回退为原目标", ex);
            return new List<string> { goal.Trim() };
        }
    }

    /// <inheritdoc/>
    public async Task<string> SummarizeResultsAsync(
        string goal,
        IReadOnlyList<string> subGoals,
        IReadOnlyList<AgentLoopResult> results,
        AgentConfig config,
        CancellationToken cancellationToken = default)
    {
        var fallback = BuildLocalSummary(goal, subGoals, results);

        if (_kernel is null || !_kernel.GetAllServices<IChatCompletionService>().Any())
        {
            return fallback;
        }

        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"总目标：{goal.Trim()}");
        for (var i = 0; i < subGoals.Count; i++)
        {
            var result = i < results.Count ? results[i] : null;
            builder.AppendLine($"--- 子任务 {i + 1}：{subGoals[i]}");
            builder.AppendLine($"结束原因：{result?.StopReason ?? "无结果"}");
            builder.AppendLine($"输出：{Truncate(result?.Answer ?? string.Empty, 1500)}");
            if (!string.IsNullOrEmpty(result?.Error))
            {
                builder.AppendLine($"错误：{result.Error}");
            }
        }
        builder.AppendLine();
        builder.Append("请基于以上各子任务的实际输出，用中文写一份简明的执行总结（200 字以内），指出整体完成情况与未解决事项。");

        try
        {
            var chat = _kernel.GetRequiredService<IChatCompletionService>();
            var settings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.3,
                MaxTokens = Math.Min(config.MaxTokens, 1024)
            };

            var response = await chat.GetChatMessageContentsAsync(
                new ChatHistory(builder.ToString()), settings, _kernel, cancellationToken).ConfigureAwait(false);
            var summary = response.Count > 0 ? response[^1].Content?.Trim() ?? string.Empty : string.Empty;

            return string.IsNullOrWhiteSpace(summary) ? fallback : summary;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error("AI 汇总失败，回退为本地摘要", ex);
            return fallback;
        }
    }

    /// <summary>
    /// LLM 不可用时的本地结果摘要
    /// </summary>
    private static string BuildLocalSummary(
        string goal,
        IReadOnlyList<string> subGoals,
        IReadOnlyList<AgentLoopResult> results)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"总目标：{goal.Trim()}");
        var completed = results.Count(r => r.Completed);
        builder.AppendLine($"完成情况：{completed}/{subGoals.Count} 个子任务成功收敛。");
        for (var i = 0; i < subGoals.Count; i++)
        {
            var result = i < results.Count ? results[i] : null;
            builder.AppendLine($"- 子任务 {i + 1} {subGoals[i]}（{result?.StopReason ?? "无结果"}）：{FirstLineOf(result?.Answer)}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string FirstLineOf(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var idx = value.IndexOf('\n');
        var line = idx < 0 ? value : value[..idx];
        return Truncate(line, 120);
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
    /// 执行一次模型调用并手动执行返回的工具调用。
    /// SK 1.52 的连接器内自动函数调用循环只在连接器自身的聊天服务中运行，
    /// 且 IAutoFunctionInvocationFilter 无法通过运行时注册触发，因此这里改用
    /// 官方 FunctionCallContent 手动模式：请求禁用工具选择，从流式块拼装工具调用，
    /// 由本类直接执行并记录调用、耗时、结果与异常。
    /// </summary>
    private async Task<string> InvokeOnceAsync(
        string prompt,
        AgentConfig config,
        AgentStep step,
        CancellationToken cancellationToken)
    {
        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.None(),
            Temperature = config.Temperature,
            MaxTokens = config.MaxTokens
        };

        var arguments = new KernelArguments(settings)
        {
            ["input"] = prompt
        };

        var function = _kernel!.CreateFunctionFromPrompt(
            "{{$input}}",
            functionName: "agent_step",
            description: "鹿流智能体单步执行");

        var kernel = _kernel!;
        var text = new System.Text.StringBuilder();
        var callBuilder = new FunctionCallContentBuilder();

        await foreach (var chunk in kernel.InvokeStreamingAsync<StreamingChatMessageContent>(
            function, arguments, cancellationToken).ConfigureAwait(false))
        {
            text.Append(chunk.Content);
            callBuilder.Append(chunk);
        }

        var toolCalls = callBuilder.Build();
        if (toolCalls.Count == 0)
        {
            return text.ToString().Trim();
        }

        // 逐个执行模型请求的工具调用，结果回灌到下一轮提示
        var observations = new System.Text.StringBuilder();
        foreach (var call in toolCalls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 流式更新里工具名是模型视角的全名（连接器上报为 插件名-函数名），
            // FunctionCallContentBuilder 不做拆分，这里按 SK 的 NameSeparator 还原插件名
            var pluginName = call.PluginName;
            var functionName = call.FunctionName;
            if (string.IsNullOrEmpty(pluginName))
            {
                var separatorIndex = functionName.IndexOf('-');
                if (separatorIndex > 0)
                {
                    pluginName = functionName[..separatorIndex];
                    functionName = functionName[(separatorIndex + 1)..];
                }
            }

            var resolvedCall = string.IsNullOrEmpty(pluginName)
                ? call
                : new FunctionCallContent(functionName, pluginName, call.Id, call.Arguments);

            var toolName = $"{pluginName}.{functionName}";
            var argsText = call.Arguments is null
                ? string.Empty
                : string.Join(", ", call.Arguments.Select(kv => $"{kv.Key}={kv.Value}"));

            step.ToolCalls.Add(string.IsNullOrEmpty(argsText) ? toolName : $"{toolName}({argsText})");
            var toolWatch = System.Diagnostics.Stopwatch.StartNew();
            var toolTimeout = TimeSpan.FromSeconds(Math.Clamp(config.ToolTimeoutSeconds, 3, 120));
            using var toolCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            toolCts.CancelAfter(toolTimeout);

            object toolResult;
            try
            {
                var invocation = await resolvedCall.InvokeAsync(kernel, toolCts.Token).ConfigureAwait(false);
                toolResult = invocation.Result ?? string.Empty;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                step.ToolErrors.Add($"{toolName}: 超时 {config.ToolTimeoutSeconds}s");
                toolResult = $"工具执行超时：{toolName}";
            }
            catch (Exception ex)
            {
                step.ToolErrors.Add($"{toolName}: {ex.Message}");
                toolResult = $"工具执行失败：{ex.Message}";
            }
            finally
            {
                toolWatch.Stop();
                step.ToolElapsedMs.Add(toolWatch.ElapsedMilliseconds);
            }

            var resultText = toolResult?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(resultText))
            {
                step.Observations.Add(resultText);
                observations.AppendLine($"- {toolName} 返回：{resultText}");
            }
        }

        var toolSection = observations.Length > 0
            ? $"\n工具执行结果：\n{observations}"
            : string.Empty;
        return $"{text}{toolSection}".Trim();
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
}
