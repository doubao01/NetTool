using System.ComponentModel;
using DeerFlow.WPF.Models;
using Microsoft.SemanticKernel;

namespace DeerFlow.WPF.Services.Plugins;

/// <summary>
/// 自我改进插件 - 提供元学习和自我优化能力
/// </summary>
public sealed class SelfImprovementPlugin
{
    private readonly ISelfReflectionService _reflectionService;
    private readonly IPatternMiner _patternMiner;
    private readonly IAutoEvolver _autoEvolver;
    private readonly IExperienceMemoryStore _experienceStore;

    public SelfImprovementPlugin(
        ISelfReflectionService reflectionService,
        IPatternMiner patternMiner,
        IAutoEvolver autoEvolver,
        IExperienceMemoryStore experienceStore)
    {
        _reflectionService = reflectionService;
        _patternMiner = patternMiner;
        _autoEvolver = autoEvolver;
        _experienceStore = experienceStore;
    }

    /// <summary>
    /// 对最近的任务执行进行反思
    /// </summary>
    [KernelFunction("reflect_on_recent_tasks")]
    [Description("对最近的任务执行进行反思，提取经验教训和最佳实践")]
    public async Task<string> ReflectOnRecentTasks(
        [Description("任务类型过滤器（chat/code/file/search）")]
        string? taskType = null,
        [Description("要反思的任务数量")]
        int limit = 10)
    {
        try
        {
            var reflections = _reflectionService.GetReflections(taskType, limit);
            var reflectionList = reflections.ToList();

            if (reflectionList.Count == 0)
            {
                return "暂无反思记录";
            }

            var summary = $"""
                ## 最近任务反思总结

                **任务数量**: {reflectionList.Count}
                **平均成功率**: {reflectionList.Average(r => r.SuccessRate):P1}
                **平均执行时间**: {reflectionList.Average(r => r.ExecutionTimeMs):F0}ms

                ### 常见问题
                {string.Join("\n", reflectionList.SelectMany(r => r.Challenges).Distinct().Take(5))}

                ### 最佳实践
                {string.Join("\n", reflectionList.SelectMany(r => r.BestPractices).Distinct().Take(5))}

                ### 改进建议
                {string.Join("\n", reflectionList.SelectMany(r => r.Improvements).Distinct().Take(5))}
                """;

            return summary;
        }
        catch (Exception ex)
        {
            return $"反思失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 挖掘新的模式
    /// </summary>
    [KernelFunction("mine_patterns")]
    [Description("从历史任务中挖掘新的最佳实践模式")]
    public async Task<string> MinePatterns(
        [Description("最小支持度（任务数量）")]
        int minSupport = 3,
        [Description("最小置信度（成功率阈值）")]
        double minConfidence = 0.7)
    {
        try
        {
            var patterns = await _patternMiner.MinePatternsAsync(minSupport, minConfidence);
            var patternList = patterns.ToList();

            if (patternList.Count == 0)
            {
                return "未挖掘到新模式";
            }

            var summary = $"""
                ## 新挖掘模式 ({patternList.Count}个)

                {string.Join("\n\n", patternList.Select(p => $"""
                    ### {p.Name}
                    **类别**: {p.Category}
                    **评分**: {p.Score:P1}
                    **使用次数**: {p.UsageCount}
                    **适用场景**: {p.ApplicableScenarios}

                    **步骤**:
                    {string.Join("\n", p.Steps)}
                    """))}
                """;

            return summary;
        }
        catch (Exception ex)
        {
            return $"模式挖掘失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 推荐最佳模式
    /// </summary>
    [KernelFunction("recommend_pattern")]
    [Description("根据当前任务描述推荐最佳实践模式")]
    public string RecommendPattern(
        [Description("当前任务描述")]
        string taskDescription,
        [Description("上下文信息")]
        string context = "")
    {
        try
        {
            var pattern = _patternMiner.RecommendPattern(taskDescription, context);

            if (pattern is null)
            {
                return "暂无推荐模式";
            }

            return $"""
                ## 推荐模式

                **名称**: {pattern.Name}
                **类别**: {pattern.Category}
                **评分**: {pattern.Score:P1}
                **成功次数**: {pattern.SuccessCount}

                **描述**: {pattern.Description}

                **执行步骤**:
                {string.Join("\n", pattern.Steps)}

                **预期结果**:
                {string.Join("\n", pattern.ExpectedOutcomes)}
                """;
        }
        catch (Exception ex)
        {
            return $"推荐失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 检索相关经验
    /// </summary>
    [KernelFunction("retrieve_experiences")]
    [Description("从经验记忆中检索相关经验")]
    public string RetrieveExperiences(
        [Description("搜索关键词")]
        string query,
        [Description("返回结果数量")]
        int limit = 5)
    {
        try
        {
            var experiences = _experienceStore.RetrieveExperiences(query, limit);
            var experienceList = experiences.ToList();

            if (experienceList.Count == 0)
            {
                return "未找到相关经验";
            }

            var summary = $"""
                ## 相关经验 ({experienceList.Count}条)

                {string.Join("\n\n", experienceList.Select(e => $"""
                    ### {e.Summary}
                    **类型**: {e.ExperienceType}
                    **置信度**: {e.Confidence:P1}
                    **验证次数**: {e.ValidationCount}

                    {e.DetailedContent}

                    **建议**:
                    {string.Join("\n", e.ActionableTips)}
                    """))}
                """;

            return summary;
        }
        catch (Exception ex)
        {
            return $"检索失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 记录任务执行反馈
    /// </summary>
    [KernelFunction("record_feedback")]
    [Description("记录任务执行的反馈，用于自我改进")]
    public async Task<string> RecordFeedback(
        [Description("任务 ID")]
        string taskId,
        [Description("任务描述")]
        string taskDescription,
        [Description("任务类型")]
        string taskType,
        [Description("是否成功")]
        bool isSuccess,
        [Description("使用的工具列表（逗号分隔）")]
        string toolsUsed,
        [Description("执行时间（毫秒）")]
        long executionTimeMs,
        [Description("经验教训")]
        string lessonsLearned = "")
    {
        try
        {
            var toolsList = toolsUsed.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim()).ToList();

            var reflection = await _reflectionService.ReflectOnTaskAsync(
                taskId, taskDescription, taskType, isSuccess, toolsList, executionTimeMs);

            // 如果有经验教训，保存到经验记忆
            if (!string.IsNullOrEmpty(lessonsLearned))
            {
                var experience = new ExperienceMemoryItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Content = lessonsLearned,
                    ExperienceType = isSuccess ? "Success" : "Lesson",
                    RelatedTaskId = taskId,
                    Summary = $"{taskType}任务{(isSuccess ? "成功" : "失败")}经验",
                    DetailedContent = lessonsLearned,
                    Confidence = 0.5,
                    ValidationCount = 1,
                    CreatedAt = DateTime.Now
                };

                await _experienceStore.SaveExperienceAsync(experience);
            }

            return $"反馈已记录：任务{taskId} - {(isSuccess ? "成功" : "失败")}";
        }
        catch (Exception ex)
        {
            return $"记录反馈失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 获取系统健康统计
    /// </summary>
    [KernelFunction("get_system_health")]
    [Description("获取自我改进系统的健康统计信息")]
    public string GetSystemHealth()
    {
        try
        {
            var reflectionStats = new Dictionary<string, object>(); // Placeholder
            var patternStats = _patternMiner.GetPatternStatistics();
            var evolutionStats = _autoEvolver.GetExperimentStatistics();
            var experienceStats = _experienceStore.GetStatistics();

            return $"""
                ## 自我改进系统健康状态

                ### 反思系统
                总反思数：{(reflectionStats.ContainsKey("TotalReflections") ? reflectionStats["TotalReflections"] : "N/A")}

                ### 模式库
                模式总数：{patternStats["TotalPatterns"]}
                已验证模式：{patternStats["VerifiedPatterns"]}
                平均评分：{((double)patternStats["AverageScore"]):P1}

                ### 演化实验
                总实验数：{evolutionStats["TotalExperiments"]}
                进行中实验：{evolutionStats["ExperimentingCount"]}
                已采纳：{evolutionStats["AdoptedCount"]}
                成功率：{((double)evolutionStats["SuccessRate"]):P1}

                ### 经验记忆
                总经验数：{experienceStats["TotalExperiences"]}
                高置信度：{experienceStats["HighConfidenceCount"]}
                平均置信度：{((double)experienceStats["AverageConfidence"]):P1}
                """;
        }
        catch (Exception ex)
        {
            return $"获取健康状态失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 启动优化实验
    /// </summary>
    [KernelFunction("start_optimization_experiment")]
    [Description("启动 A/B 测试实验来优化系统配置")]
    public async Task<string> StartOptimizationExperiment(
        [Description("实验名称")]
        string experimentName,
        [Description("优化目标")]
        string target,
        [Description("当前配置")]
        string currentConfig,
        [Description("候选配置")]
        string candidateConfig)
    {
        try
        {
            var experimentId = await _autoEvolver.StartExperimentAsync(
                experimentName, target, currentConfig, candidateConfig);

            return $"实验已启动：{experimentId}\n目标：{target}\n请记录指标以评估效果";
        }
        catch (Exception ex)
        {
            return $"启动实验失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 记录实验指标
    /// </summary>
    [KernelFunction("record_experiment_metric")]
    [Description("记录 A/B 测试实验的指标数据")]
    public void RecordExperimentMetric(
        [Description("实验 ID")]
        string experimentId,
        [Description("指标名称")]
        string metricName,
        [Description("指标值")]
        double value)
    {
        _autoEvolver.RecordMetric(experimentId, metricName, value);
    }
}
