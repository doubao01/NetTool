using System.IO;
using System.Text;
using DeerFlow.WPF.Models;
using Microsoft.Extensions.Logging;

namespace DeerFlow.WPF.Services;

/// <summary>
/// 文档生成服务接口
/// </summary>
public interface IDocumentGenerationService
{
    /// <summary>
    /// 生成模式库文档
    /// </summary>
    Task<string> GeneratePatternDocumentationAsync(
        List<PatternItem> patterns,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成经验记忆文档
    /// </summary>
    Task<string> GenerateExperienceDocumentationAsync(
        List<ExperienceMemoryItem> experiences,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成反思总结报告
    /// </summary>
    Task<string> GenerateReflectionReportAsync(
        List<SelfReflectionItem> reflections,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成系统健康报告
    /// </summary>
    Task<string> GenerateSystemHealthReportAsync(
        Dictionary<string, object> statistics,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 导出文档到文件
    /// </summary>
    Task<string> ExportDocumentAsync(
        string content,
        string fileName,
        string? outputDir = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量生成所有文档
    /// </summary>
    Task<Dictionary<string, string>> GenerateAllDocumentsAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 文档生成服务 - 基于反思记录和模式数据自动生成文档
/// </summary>
public class DocumentGenerationService : IDocumentGenerationService
{
    private readonly ILogger _logger;
    private readonly ISelfReflectionService _reflectionService;
    private readonly IPatternMiner _patternMiner;
    private readonly IExperienceMemoryStore _experienceStore;
    private readonly string _defaultOutputDir;

    public DocumentGenerationService(
        ILoggerFactory loggerFactory,
        ISelfReflectionService reflectionService,
        IPatternMiner patternMiner,
        IExperienceMemoryStore experienceStore)
    {
        _logger = loggerFactory.CreateLogger("DocumentGeneration");
        _reflectionService = reflectionService;
        _patternMiner = patternMiner;
        _experienceStore = experienceStore;
        _defaultOutputDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "DeerFlow.WPF", "Docs");
    }

    /// <inheritdoc/>
    public Task<string> GeneratePatternDocumentationAsync(
        List<PatternItem> patterns,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                var sb = new StringBuilder();

                // 文档标题
                sb.AppendLine("# 模式库文档");
                sb.AppendLine();
                sb.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"**模式总数**: {patterns.Count}");
                sb.AppendLine();

                // 分类统计
                var categoryGroups = patterns.GroupBy(p => p.Category);
                sb.AppendLine("## 分类概览");
                sb.AppendLine();
                foreach (var group in categoryGroups)
                {
                    sb.AppendLine($"- **{group.Key}**: {group.Count()} 个模式");
                }
                sb.AppendLine();

                // 详细模式列表
                sb.AppendLine("## 模式详情");
                sb.AppendLine();

                foreach (var pattern in patterns.OrderByDescending(p => p.Score).ThenByDescending(p => p.UsageCount))
                {
                    sb.AppendLine($"### {pattern.Name}");
                    sb.AppendLine();
                    sb.AppendLine($"**类别**: {pattern.Category}");
                    sb.AppendLine();
                    sb.AppendLine($"**状态**: {pattern.Status}");
                    sb.AppendLine();
                    sb.AppendLine($"**评分**: {pattern.Score:P1}");
                    sb.AppendLine();
                    sb.AppendLine($"**使用次数**: {pattern.UsageCount}");
                    sb.AppendLine();
                    sb.AppendLine($"**描述**: {pattern.Description}");
                    sb.AppendLine();

                    if (pattern.Steps.Count > 0)
                    {
                        sb.AppendLine("**执行步骤**:");
                        sb.AppendLine();
                        for (int i = 0; i < pattern.Steps.Count; i++)
                        {
                            sb.AppendLine($"{i + 1}. {pattern.Steps[i]}");
                        }
                        sb.AppendLine();
                    }

                    if (pattern.ApplicableScenarios.Length > 0)
                    {
                        sb.AppendLine($"**适用场景**: {pattern.ApplicableScenarios}");
                        sb.AppendLine();
                    }

                    if (pattern.Preconditions.Count > 0)
                    {
                        sb.AppendLine("**前置条件**:");
                        sb.AppendLine();
                        foreach (var precond in pattern.Preconditions)
                        {
                            sb.AppendLine($"- {precond}");
                        }
                        sb.AppendLine();
                    }

                    sb.AppendLine("---");
                    sb.AppendLine();
                }

                _logger.LogInformation("[DocumentGeneration] 生成模式文档完成，共 {Count} 个模式", patterns.Count);
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DocumentGeneration] 生成模式文档失败");
                return $"生成失败：{ex.Message}";
            }
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<string> GenerateExperienceDocumentationAsync(
        List<ExperienceMemoryItem> experiences,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                var sb = new StringBuilder();

                sb.AppendLine("# 经验记忆文档");
                sb.AppendLine();
                sb.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"**经验总数**: {experiences.Count}");
                sb.AppendLine();

                // 按类型分类
                var typeGroups = experiences.GroupBy(e => e.ExperienceType);
                sb.AppendLine("## 经验分类");
                sb.AppendLine();
                foreach (var group in typeGroups)
                {
                    sb.AppendLine($"- **{group.Key}**: {group.Count()} 条");
                }
                sb.AppendLine();

                // 高置信度经验
                var highConfidence = experiences.Where(e => e.Confidence >= 0.7).ToList();
                if (highConfidence.Count > 0)
                {
                    sb.AppendLine("## 高置信度经验（≥70%）");
                    sb.AppendLine();
                    foreach (var exp in highConfidence.OrderByDescending(e => e.Confidence))
                    {
                        sb.AppendLine($"### {exp.Summary}");
                        sb.AppendLine();
                        sb.AppendLine($"**置信度**: {exp.Confidence:P1}");
                        sb.AppendLine();
                        sb.AppendLine($"**详细内容**: {exp.DetailedContent}");
                        sb.AppendLine();

                        if (exp.ActionableTips.Count > 0)
                        {
                            sb.AppendLine("**可操作建议**:");
                            sb.AppendLine();
                            foreach (var tip in exp.ActionableTips)
                            {
                                sb.AppendLine($"- {tip}");
                            }
                            sb.AppendLine();
                        }

                        sb.AppendLine("---");
                        sb.AppendLine();
                    }
                }

                _logger.LogInformation("[DocumentGeneration] 生成经验文档完成，共 {Count} 条经验", experiences.Count);
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DocumentGeneration] 生成经验文档失败");
                return $"生成失败：{ex.Message}";
            }
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<string> GenerateReflectionReportAsync(
        List<SelfReflectionItem> reflections,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                var sb = new StringBuilder();

                sb.AppendLine("# 任务反思总结报告");
                sb.AppendLine();
                sb.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"**统计周期**: {startDate:yyyy-MM-dd} 至 {endDate:yyyy-MM-dd}");
                sb.AppendLine();

                var filtered = reflections.Where(r =>
                    r.ReflectionTimestamp >= startDate &&
                    r.ReflectionTimestamp <= endDate).ToList();

                sb.AppendLine("## 总体统计");
                sb.AppendLine();
                sb.AppendLine($"- **总任务数**: {filtered.Count}");

                var successCount = filtered.Count(r => r.Outcome == "Success");
                var successRate = filtered.Count > 0 ? (double)successCount / filtered.Count : 0;
                sb.AppendLine($"- **成功任务**: {successCount} ({successRate:P1})");
                sb.AppendLine($"- **失败任务**: {filtered.Count - successCount}");
                sb.AppendLine();

                // 按类型统计
                var typeGroups = filtered.GroupBy(r => r.TaskType);
                sb.AppendLine("## 任务类型分布");
                sb.AppendLine();
                foreach (var group in typeGroups)
                {
                    var typeSuccessRate = group.Count(r => r.Outcome == "Success") / (double)group.Count();
                    sb.AppendLine($"- **{group.Key}**: {group.Count()} 个，成功率 {typeSuccessRate:P1}");
                }
                sb.AppendLine();

                // 平均执行时间
                var avgTime = filtered.Average(r => r.ExecutionTimeMs);
                sb.AppendLine("## 性能指标");
                sb.AppendLine();
                sb.AppendLine($"- **平均执行时间**: {avgTime:F0}ms");
                sb.AppendLine($"- **最长执行时间**: {filtered.Max(r => r.ExecutionTimeMs)}ms");
                sb.AppendLine($"- **最短执行时间**: {filtered.Min(r => r.ExecutionTimeMs)}ms");
                sb.AppendLine();

                // 工具使用统计
                var toolUsage = filtered.SelectMany(r => r.ToolsUsed)
                    .GroupBy(t => t)
                    .OrderByDescending(g => g.Count())
                    .Take(10);
                sb.AppendLine("## 常用工具 Top 10");
                sb.AppendLine();
                int toolRank = 1;
                foreach (var tool in toolUsage)
                {
                    sb.AppendLine($"{toolRank}. **{tool.Key}**: 使用 {tool.Count()} 次");
                    toolRank++;
                }
                sb.AppendLine();

                // 改进建议汇总
                var allImprovements = filtered.SelectMany(r => r.Improvements)
                    .GroupBy(i => i)
                    .OrderByDescending(g => g.Count())
                    .Take(5);
                if (allImprovements.Any())
                {
                    sb.AppendLine("## 主要改进建议");
                    sb.AppendLine();
                    foreach (var improvement in allImprovements)
                    {
                        sb.AppendLine($"- {improvement.Key} （提及 {improvement.Count()} 次）");
                    }
                    sb.AppendLine();
                }

                // 最近反思
                sb.AppendLine("## 最近反思");
                sb.AppendLine();
                foreach (var reflection in filtered.OrderByDescending(r => r.ReflectionTimestamp).Take(10))
                {
                    sb.AppendLine($"### [{reflection.ReflectionTimestamp:yyyy-MM-dd HH:mm}] {reflection.TaskDescription}");
                    sb.AppendLine();
                    sb.AppendLine($"**结果**: {(reflection.Outcome == "Success" ? "✅ 成功" : "❌ 失败")}");
                    sb.AppendLine();
                    if (reflection.Improvements.Count > 0)
                    {
                        sb.AppendLine("**改进建议**:");
                        foreach (var imp in reflection.Improvements)
                        {
                            sb.AppendLine($"- {imp}");
                        }
                        sb.AppendLine();
                    }
                }

                _logger.LogInformation("[DocumentGeneration] 生成了反思报告，共 {Count} 条记录", filtered.Count);
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DocumentGeneration] 生成反思报告失败");
                return $"生成失败：{ex.Message}";
            }
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<string> GenerateSystemHealthReportAsync(
        Dictionary<string, object> statistics,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                var sb = new StringBuilder();

                sb.AppendLine("# 系统健康状态报告");
                sb.AppendLine();
                sb.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();

                sb.AppendLine("## 核心指标");
                sb.AppendLine();

                foreach (var kvp in statistics)
                {
                    sb.AppendLine($"- **{kvp.Key}**: {kvp.Value}");
                }

                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine("*报告由 DeerFlow.WPF 自动文档生成系统生成*");

                _logger.LogInformation("[DocumentGeneration] 生成系统健康报告完成");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DocumentGeneration] 生成系统健康报告失败");
                return $"生成失败：{ex.Message}";
            }
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<string> ExportDocumentAsync(
        string content,
        string fileName,
        string? outputDir = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                var dir = outputDir ?? _defaultOutputDir;

                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var filePath = Path.Combine(dir, fileName);

                File.WriteAllText(filePath, content, Encoding.UTF8);

                _logger.LogInformation("[DocumentGeneration] 文档已导出到：{FilePath}", filePath);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DocumentGeneration] 导出文档失败");
                return string.Empty;
            }
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, string>> GenerateAllDocumentsAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, string>();

        try
        {
            // 生成模式文档
            var patterns = _patternMiner.GetPatterns(limit: 100).ToList();
            var patternDoc = await GeneratePatternDocumentationAsync(patterns, cancellationToken);
            var patternFile = await ExportDocumentAsync(patternDoc, "Patterns.md", null, cancellationToken);
            results["Patterns"] = patternFile;

            // 生成经验文档
            var experiences = _experienceStore.GetAllExperiences(limit: 100).ToList();
            var experienceDoc = await GenerateExperienceDocumentationAsync(experiences, cancellationToken);
            var experienceFile = await ExportDocumentAsync(experienceDoc, "Experiences.md", null, cancellationToken);
            results["Experiences"] = experienceFile;

            // 生成反思报告（最近 30 天）
            var reflections = _reflectionService.GetReflections(limit: 200).ToList();
            var endDate = DateTime.Now;
            var startDate = endDate.AddDays(-30);
            var reflectionDoc = await GenerateReflectionReportAsync(reflections, startDate, endDate, cancellationToken);
            var reflectionFile = await ExportDocumentAsync(reflectionDoc, "Reflection_Report.md", null, cancellationToken);
            results["ReflectionReport"] = reflectionFile;

            _logger.LogInformation("[DocumentGeneration] 批量文档生成完成，共 {Count} 个文件", results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DocumentGeneration] 批量文档生成失败");
        }

        return results;
    }
}
