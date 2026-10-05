using System.Collections.Concurrent;
using DeerFlow.WPF.Models;
using Microsoft.Extensions.Logging;

namespace DeerFlow.WPF.Services;

/// <summary>
/// 模式挖掘服务接口
/// </summary>
public interface IPatternMiner
{
    /// <summary>
    /// 从历史任务中挖掘新模式
    /// </summary>
    Task<IEnumerable<PatternItem>> MinePatternsAsync(
        int minSupport = 3,
        double minConfidence = 0.7,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 推荐最佳模式
    /// </summary>
    PatternItem? RecommendPattern(
        string taskDescription,
        string context,
        double minScore = 0.6);

    /// <summary>
    /// 获取模式统计信息
    /// </summary>
    Dictionary<string, object> GetPatternStatistics();

    /// <summary>
    /// 获取已挖掘的模式列表
    /// </summary>
    IEnumerable<PatternItem> GetPatterns(int limit = 10);
}

/// <summary>
/// 模式挖掘服务 - 从历史数据中挖掘可复用模式
/// </summary>
public class PatternMiner : IPatternMiner
{
    private readonly ISelfReflectionService _reflectionService;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, PatternItem> _minedPatterns = new();
    private DateTime _lastMiningTime = DateTime.MinValue;
    private const int MinTasksForMining = 10;

    public PatternMiner(
        ISelfReflectionService reflectionService,
        ILoggerFactory loggerFactory)
    {
        _reflectionService = reflectionService;
        _logger = loggerFactory.CreateLogger("PatternMiner");
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<PatternItem>> MinePatternsAsync(
        int minSupport = 3,
        double minConfidence = 0.7,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[PatternMiner] 开始模式挖掘 - 最小支持度：{MinSupport}, 最小置信度：{MinConfidence}",
            minSupport, minConfidence);

        var allReflections = _reflectionService.GetReflections(limit: 1000).ToList();

        if (allReflections.Count < MinTasksForMining)
        {
            _logger.LogWarning(
                "[PatternMiner] 任务数量不足（{Count}/{MinRequired}），跳过挖掘",
                allReflections.Count, MinTasksForMining);
            return Enumerable.Empty<PatternItem>();
        }

        var newPatterns = new List<PatternItem>();

        try
        {
            // 1. 按任务类型分组
            var groupedByType = allReflections
                .Where(r => r.SuccessRate >= minConfidence)
                .GroupBy(r => r.TaskType);

            foreach (var group in groupedByType)
            {
                if (group.Count() >= minSupport)
                {
                    var pattern = await _reflectionService.ExtractPatternAsync(
                        group, cancellationToken);

                    if (pattern is not null && !_minedPatterns.ContainsKey(pattern.Id))
                    {
                        _minedPatterns.TryAdd(pattern.Id, pattern);
                        newPatterns.Add(pattern);
                        _logger.LogInformation(
                            "[PatternMiner] 挖掘到新模式：{PatternName} (支持度：{Support})",
                            pattern.Name, group.Count());
                    }
                }
            }

            // 2. 挖掘工具使用模式
            var toolPatterns = MineToolUsagePatterns(allReflections, minSupport);
            foreach (var pattern in toolPatterns)
            {
                if (!_minedPatterns.ContainsKey(pattern.Id))
                {
                    _minedPatterns.TryAdd(pattern.Id, pattern);
                    newPatterns.Add(pattern);
                }
            }

            // 3. 挖掘性能优化模式
            var performancePatterns = MinePerformancePatterns(allReflections, minSupport);
            foreach (var pattern in performancePatterns)
            {
                if (!_minedPatterns.ContainsKey(pattern.Id))
                {
                    _minedPatterns.TryAdd(pattern.Id, pattern);
                    newPatterns.Add(pattern);
                }
            }

            _lastMiningTime = DateTime.Now;
            _logger.LogInformation(
                "[PatternMiner] 挖掘完成 - 新增{Count}个模式",
                newPatterns.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PatternMiner] 模式挖掘失败");
        }

        return newPatterns;
    }

    /// <inheritdoc/>
    public PatternItem? RecommendPattern(
        string taskDescription,
        string context,
        double minScore = 0.6)
    {
        var candidates = _minedPatterns.Values
            .Where(p => p.Score >= minScore)
            .Where(p => IsPatternRelevant(p, taskDescription, context))
            .OrderByDescending(p => p.Score)
            .ThenByDescending(p => p.UsageCount)
            .FirstOrDefault();

        if (candidates is not null)
        {
            _logger.LogInformation(
                "[PatternMiner] 推荐模式：{PatternName} (评分：{Score})",
                candidates.Name, candidates.Score);
        }

        return candidates;
    }

    /// <inheritdoc/>
    public Dictionary<string, object> GetPatternStatistics()
    {
        var patterns = _minedPatterns.Values.ToList();

        return new Dictionary<string, object>
        {
            ["TotalPatterns"] = patterns.Count,
            ["VerifiedPatterns"] = patterns.Count(p => p.Status == "Verified"),
            ["DraftPatterns"] = patterns.Count(p => p.Status == "Draft"),
            ["ArchivedPatterns"] = patterns.Count(p => p.Status == "Archived"),
            ["AverageScore"] = patterns.Count > 0 ? patterns.Average(p => p.Score) : 0,
            ["TotalUsageCount"] = patterns.Sum(p => p.UsageCount),
            ["TotalSuccessCount"] = patterns.Sum(p => p.SuccessCount),
            ["LastMiningTime"] = _lastMiningTime,
            ["PatternsByCategory"] = patterns
                .GroupBy(p => p.Category)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    /// <inheritdoc/>
    public IEnumerable<PatternItem> GetPatterns(int limit = 10)
    {
        if (limit <= 0)
        {
            return Enumerable.Empty<PatternItem>();
        }

        return _minedPatterns.Values
            .OrderByDescending(p => p.Score)
            .ThenByDescending(p => p.UsageCount)
            .Take(limit)
            .ToList();
    }

    #region Private Mining Methods

    /// <summary>
    /// 挖掘工具使用模式
    /// </summary>
    private IEnumerable<PatternItem> MineToolUsagePatterns(
        List<SelfReflectionItem> reflections,
        int minSupport)
    {
        // 分析频繁共同出现的工具组合
        var toolCooccurrence = new Dictionary<string, int>();

        foreach (var reflection in reflections.Where(r => r.SuccessRate >= 0.8))
        {
            var tools = reflection.ToolsUsed.OrderBy(t => t).ToList();
            for (int i = 0; i < tools.Count; i++)
            {
                for (int j = i + 1; j < tools.Count; j++)
                {
                    var pair = $"{tools[i]}+{tools[j]}";
                    if (!toolCooccurrence.ContainsKey(pair))
                    {
                        toolCooccurrence[pair] = 0;
                    }
                    toolCooccurrence[pair]++;
                }
            }
        }

        // 提取高频工具组合模式
        var patterns = new List<PatternItem>();
        foreach (var kvp in toolCooccurrence.Where(k => k.Value >= minSupport))
        {
            var tools = kvp.Key.Split('+');
            patterns.Add(new PatternItem
            {
                Name = $"工具组合模式：{kvp.Key}",
                Description = $"{tools[0]}和{tools[1]}经常一起使用，已成功{kvp.Value}次",
                Category = "工具使用",
                ApplicableScenarios = "需要多种工具协作的任务",
                Steps = new List<string>
                {
                    $"首先使用{tools[0]}进行初步处理",
                    $"然后使用{tools[1]}进行深度处理",
                    "验证结果一致性"
                },
                Preconditions = new List<string>
                {
                    $"{tools[0]}可用",
                    $"{tools[1]}可用"
                },
                ExpectedOutcomes = new List<string>
                {
                    "任务质量提升",
                    "执行效率优化"
                },
                SourceReflectionIds = reflections
                    .Where(r => r.ToolsUsed.Contains(tools[0]) && r.ToolsUsed.Contains(tools[1]))
                    .Select(r => r.TaskId)
                    .ToList(),
                Score = 0.9,
                UsageCount = kvp.Value,
                SuccessCount = kvp.Value,
                CreatedAt = DateTime.Now,
                Status = "Verified"
            });
        }

        return patterns;
    }

    /// <summary>
    /// 挖掘性能优化模式
    /// </summary>
    private IEnumerable<PatternItem> MinePerformancePatterns(
        List<SelfReflectionItem> reflections,
        int minSupport)
    {
        // 分析高性能任务的共同特征
        var fastTasks = reflections
            .Where(r => r.ExecutionTimeMs < 2000 && r.SuccessRate >= 0.9)
            .ToList();

        if (fastTasks.Count < minSupport)
        {
            return Enumerable.Empty<PatternItem>();
        }

        // 分析最佳实践关键词
        var practiceFreq = fastTasks
            .SelectMany(r => r.BestPractices)
            .GroupBy(p => p)
            .OrderByDescending(g => g.Count())
            .Take(5);

        var patterns = new List<PatternItem>();
        foreach (var practice in practiceFreq.Where(p => p.Count() >= minSupport))
        {
            patterns.Add(new PatternItem
            {
                Name = $"性能优化模式：{practice.Key}",
                Description = $"{practice.Count()}次高性能任务中总结的经验",
                Category = "性能优化",
                ApplicableScenarios = "对响应时间敏感的任务",
                Steps = new List<string>
                {
                    practice.Key,
                    "监控执行时间",
                    "优化瓶颈环节"
                },
                Preconditions = new List<string>
                {
                    "性能监控已启用",
                    "基线时间已测量"
                },
                ExpectedOutcomes = new List<string>
                {
                    "执行时间<2 秒",
                    "用户感知流畅"
                },
                SourceReflectionIds = fastTasks.Select(r => r.TaskId).ToList(),
                Score = 0.95,
                UsageCount = practice.Count(),
                SuccessCount = practice.Count(),
                CreatedAt = DateTime.Now,
                Status = "Verified"
            });
        }

        return patterns;
    }

    /// <summary>
    /// 判断模式是否相关
    /// </summary>
    private static bool IsPatternRelevant(
        PatternItem pattern,
        string taskDescription,
        string context)
    {
        var descLower = taskDescription.ToLower();
        var categoryLower = pattern.Category.ToLower();

        // 基于类别的相关性
        if (descLower.Contains("代码") && categoryLower.Contains("代码"))
            return true;
        if (descLower.Contains("搜索") && categoryLower.Contains("搜索"))
            return true;
        if (descLower.Contains("文件") && categoryLower.Contains("文件"))
            return true;

        // 基于关键词的相关性
        var keywords = pattern.Description
            .ToLower()
            .Split(new[] { ' ', ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
        return keywords.Length > 0 && keywords.Any(k => descLower.Contains(k));
    }

    #endregion
}
