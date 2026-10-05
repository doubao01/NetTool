using System.Collections.Concurrent;
using DeerFlow.WPF.Models;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;

namespace DeerFlow.WPF.Services;

/// <summary>
/// 自我反思服务接口
/// </summary>
public interface ISelfReflectionService
{
    /// <summary>
    /// 记录任务执行结果并进行反思
    /// </summary>
    Task<SelfReflectionItem> ReflectOnTaskAsync(
        string taskId,
        string taskDescription,
        string taskType,
        bool isSuccess,
        List<string> toolsUsed,
        long executionTimeMs,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 提取最佳实践模式
    /// </summary>
    Task<PatternItem?> ExtractPatternAsync(
        IEnumerable<SelfReflectionItem> reflections,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取历史反思记录
    /// </summary>
    IEnumerable<SelfReflectionItem> GetReflections(
        string? taskType = null,
        int limit = 50);

    /// <summary>
    /// 获取模式库
    /// </summary>
    IEnumerable<PatternItem> GetPatterns(
        string? category = null,
        double minScore = 0.5);

    /// <summary>
    /// 记录模式使用反馈
    /// </summary>
    void RecordPatternUsage(string patternId, bool isSuccess);
}

/// <summary>
/// 自我反思服务实现 - 负责任务后反思和模式提取
/// </summary>
public class SelfReflectionService : ISelfReflectionService
{
    private readonly ILogger _logger;
    private readonly Kernel? _kernel;
    private readonly IAlertService? _alertService;
    private readonly ConcurrentBag<SelfReflectionItem> _reflections = new();
    private readonly ConcurrentDictionary<string, PatternItem> _patterns = new();
    private readonly object _lock = new();
    private const string ReflectionPrompt = @"
分析以下任务执行记录，提取关键洞察：

任务描述：{{taskDescription}}
任务类型：{{taskType}}
使用工具：{{toolsUsed}}
执行时间：{{executionTime}}ms
结果：{{outcome}}

请分析：
1. 哪些做法是有效的？（最佳实践）
2. 遇到了什么挑战？
3. 如何解决的？
4. 有什么可以改进的地方？
5. 这个场景下应该遵循什么模式？

请以结构化 JSON 格式返回。
";

    public SelfReflectionService(ILoggerFactory loggerFactory, Kernel? kernel = null, IAlertService? alertService = null)
    {
        _logger = loggerFactory.CreateLogger("SelfReflection");
        _kernel = kernel;
        _alertService = alertService;
    }

    /// <inheritdoc/>
    public async Task<SelfReflectionItem> ReflectOnTaskAsync(
        string taskId,
        string taskDescription,
        string taskType,
        bool isSuccess,
        List<string> toolsUsed,
        long executionTimeMs,
        CancellationToken cancellationToken = default)
    {
        var reflection = new SelfReflectionItem
        {
            TaskId = taskId,
            TaskDescription = taskDescription,
            TaskType = taskType,
            Outcome = isSuccess ? "Success" : "Failed",
            SuccessRate = isSuccess ? 1.0 : 0.0,
            ToolsUsed = toolsUsed,
            ExecutionTimeMs = executionTimeMs,
            ReflectionTimestamp = DateTime.Now,
            IsConvertedToPattern = false
        };

        try
        {
            // 使用 SK 进行深度反思分析
            if (_kernel is not null)
            {
                await AnalyzeWithKernelAsync(reflection, cancellationToken);
            }
            else
            {
                // 基础反思逻辑
                PerformBasicReflection(reflection);
            }

            _reflections.Add(reflection);
            _logger.LogInformation(
                "[SelfReflection] 完成反思：任务{TaskId} - {Outcome}",
                taskId, reflection.Outcome);

            // 性能告警检查
            _alertService?.CheckResponseTime(executionTimeMs, taskId);

            // 检查是否可以提取新模式
            if (isSuccess && toolsUsed.Count >= 2)
            {
                await TryExtractPatternAsync(reflection, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SelfReflection] 反思过程失败：{TaskId}", taskId);
            // 仍然保存基础反思记录
            _reflections.Add(reflection);
        }

        return reflection;
    }

    /// <summary>
    /// 使用 Semantic Kernel 进行深度分析
    /// </summary>
    private async Task AnalyzeWithKernelAsync(
        SelfReflectionItem reflection,
        CancellationToken cancellationToken)
    {
        try
        {
            var chatFunction = _kernel!.CreateFunctionFromPrompt(ReflectionPrompt);
            var arguments = new KernelArguments
            {
                ["taskDescription"] = reflection.TaskDescription,
                ["taskType"] = reflection.TaskType,
                ["toolsUsed"] = string.Join(", ", reflection.ToolsUsed),
                ["executionTime"] = reflection.ExecutionTimeMs,
                ["outcome"] = reflection.Outcome
            };

            var analysisResult = await _kernel.InvokeAsync(chatFunction, arguments, cancellationToken);
            var analysisText = analysisResult.ToString();

            // 解析分析结果（简化处理，实际应该用 JSON 解析）
            reflection.Challenges = ExtractChallengesFromAnalysis(analysisText);
            reflection.Solutions = ExtractSolutionsFromAnalysis(analysisText);
            reflection.BestPractices = ExtractBestPracticesFromAnalysis(analysisText);
            reflection.Improvements = ExtractImprovementsFromAnalysis(analysisText);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SelfReflection] SK 分析失败，回退到基础反思");
            PerformBasicReflection(reflection);
        }
    }

    /// <summary>
    /// 基础反思逻辑（无 SK 时）
    /// </summary>
    private void PerformBasicReflection(SelfReflectionItem reflection)
    {
        // 基于规则的简单反思
        if (reflection.ExecutionTimeMs > 5000)
        {
            reflection.Improvements.Add("执行时间过长，考虑优化性能");
        }

        if (reflection.ToolsUsed.Count > 5)
        {
            reflection.Improvements.Add("使用了过多工具，考虑简化流程");
        }

        if (reflection.Outcome == "Success")
        {
            reflection.BestPractices.Add(
                $"成功使用 {string.Join(", ", reflection.ToolsUsed)} 完成任务");
        }
        else
        {
            reflection.Challenges.Add("任务执行失败，需要分析原因");
            reflection.Improvements.Add("分析失败原因并调整执行策略");
        }
    }

    /// <summary>
    /// 尝试从单个反思中提取模式
    /// </summary>
    private async Task TryExtractPatternAsync(
        SelfReflectionItem reflection,
        CancellationToken cancellationToken)
    {
        // 累积相似的反思记录
        var similarReflections = GetReflections(reflection.TaskType, 10)
            .Where(r => r.TaskType == reflection.TaskType && r.SuccessRate >= 0.8)
            .ToList();

        if (similarReflections.Count >= 3)
        {
            var pattern = await ExtractPatternAsync(similarReflections, cancellationToken);
            if (pattern is not null)
            {
                _patterns.TryAdd(pattern.Id, pattern);
                reflection.IsConvertedToPattern = true;
                _logger.LogInformation(
                    "[SelfReflection] 提取新模式：{PatternName}", pattern.Name);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<PatternItem?> ExtractPatternAsync(
        IEnumerable<SelfReflectionItem> reflections,
        CancellationToken cancellationToken = default)
    {
        var reflectionList = reflections.ToList();
        if (reflectionList.Count < 3)
        {
            _logger.LogWarning("[SelfReflection] 反思记录不足 3 条，无法提取模式");
            return null;
        }

        try
        {
            // 统计常用工具组合
            var toolCombinations = reflectionList
                .SelectMany(r => r.ToolsUsed)
                .GroupBy(t => t)
                .OrderByDescending(g => g.Count())
                .Take(3);

            // 统计常见问题和解决方案
            var allChallenges = reflectionList.SelectMany(r => r.Challenges);
            var allSolutions = reflectionList.SelectMany(r => r.Solutions);
            var allBestPractices = reflectionList.SelectMany(r => r.BestPractices);

            // 创建模式
            var pattern = new PatternItem
            {
                Name = $"{reflectionList.First().TaskType}最佳实践模式",
                Description = $"基于{reflectionList.Count}次成功经验总结的模式",
                Category = MapTaskTypeToCategory(reflectionList.First().TaskType),
                ApplicableScenarios = reflectionList.First().TaskType + "相关任务",
                Steps = allBestPractices.Take(5).ToList(),
                Preconditions = new List<string> { "任务类型匹配", "必要工具可用" },
                ExpectedOutcomes = new List<string> { "任务成功完成", "执行时间<5 秒" },
                SourceReflectionIds = reflectionList.Select(r => r.TaskId).ToList(),
                Score = reflectionList.Average(r => r.SuccessRate),
                UsageCount = 0,
                SuccessCount = 0,
                CreatedAt = DateTime.Now,
                Status = "Verified"
            };

            // 提取出的模式入库，供 GetPatterns / RecordPatternUsage 使用
            if (_patterns.TryAdd(pattern.Id, pattern))
            {
                _logger.LogInformation(
                    "[SelfReflection] 模式已入库：{PatternName} (评分：{Score})",
                    pattern.Name, pattern.Score);
            }

            return pattern;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SelfReflection] 模式提取失败");
            return null;
        }
    }

    /// <inheritdoc/>
    public IEnumerable<SelfReflectionItem> GetReflections(
        string? taskType = null,
        int limit = 50)
    {
        var query = _reflections.AsEnumerable();

        if (!string.IsNullOrEmpty(taskType))
        {
            query = query.Where(r => r.TaskType == taskType);
        }

        return query
            .OrderByDescending(r => r.ReflectionTimestamp)
            .Take(limit);
    }

    /// <inheritdoc/>
    public IEnumerable<PatternItem> GetPatterns(
        string? category = null,
        double minScore = 0.5)
    {
        var query = _patterns.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(p => p.Category == category);
        }

        return query
            .Where(p => p.Score >= minScore)
            .OrderByDescending(p => p.Score)
            .ThenByDescending(p => p.UsageCount);
    }

    /// <inheritdoc/>
    public void RecordPatternUsage(string patternId, bool isSuccess)
    {
        if (_patterns.TryGetValue(patternId, out var pattern))
        {
            pattern.UsageCount++;
            if (isSuccess)
            {
                pattern.SuccessCount++;
                // 动态调整评分
                pattern.Score = Math.Min(1.0, (double)pattern.SuccessCount / pattern.UsageCount);
            }
            pattern.LastUsedAt = DateTime.Now;

            _logger.LogInformation(
                "[SelfReflection] 记录模式使用：{PatternId} - {Success}",
                patternId, isSuccess ? "成功" : "失败");
        }
    }

    #region Helper Methods

    private static string MapTaskTypeToCategory(string taskType)
    {
        return taskType.ToLower() switch
        {
            "chat" => "对话交互",
            "code" => "代码执行",
            "file" => "文件操作",
            "search" => "网络搜索",
            _ => "通用模式"
        };
    }

    private static List<string> ExtractChallengesFromAnalysis(string analysis)
    {
        // 简化实现，实际应使用 JSON 解析
        return new List<string> { "分析待完善" };
    }

    private static List<string> ExtractSolutionsFromAnalysis(string analysis)
    {
        return new List<string> { "分析待完善" };
    }

    private static List<string> ExtractBestPracticesFromAnalysis(string analysis)
    {
        return new List<string> { "分析待完善" };
    }

    private static List<string> ExtractImprovementsFromAnalysis(string analysis)
    {
        return new List<string> { "分析待完善" };
    }

    #endregion
}
