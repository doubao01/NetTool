using Xunit;
using DeerFlow.WPF.Services;
using DeerFlow.WPF.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;

namespace DeerFlow.WPF.Tests.Benchmarks;

/// <summary>
/// Phase 2 性能基准测试
/// </summary>
public class Phase2Benchmarks
{
    private readonly IFeedbackService _feedbackService;
    private readonly IAlertService _alertService;
    private readonly IDocumentGenerationService _documentService;

    public Phase2Benchmarks()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IFeedbackService, FeedbackService>();
        services.AddSingleton<IAlertService, AlertService>();
        services.AddSingleton<IDocumentGenerationService, DocumentGenerationService>();
        services.AddSingleton<ISelfReflectionService, SelfReflectionService>();
        services.AddSingleton<IPatternMiner, PatternMiner>();
        services.AddSingleton<IExperienceMemoryStore, ExperienceMemoryStore>();

        var provider = services.BuildServiceProvider();
        _feedbackService = provider.GetRequiredService<IFeedbackService>();
        _alertService = provider.GetRequiredService<IAlertService>();
        _documentService = provider.GetRequiredService<IDocumentGenerationService>();
    }

    [Fact]
    public async Task Benchmark_FeedbackSubmission_Performance()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        const int iterations = 100;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            await _feedbackService.SubmitFeedbackAsync($"task-{i}", "Like", 5);
        }

        stopwatch.Stop();
        var avgTimeMs = stopwatch.ElapsedMilliseconds / (double)iterations;

        // Assert
        Assert.True(avgTimeMs < 50, $"平均反馈提交时间 {avgTimeMs}ms 超过目标 50ms");
        
        // Output for reporting
        TestOutputHelper.WriteLine($"反馈提交性能：{avgTimeMs:F2}ms/次 (目标：<50ms)");
    }

    [Fact]
    public async Task Benchmark_FeedbackQuery_Performance()
    {
        // Arrange - Pre-populate with data
        for (int i = 0; i < 100; i++)
        {
            await _feedbackService.SubmitFeedbackAsync($"task-{i}", i % 2 == 0 ? "Like" : "Dislike", i % 5 + 1);
        }

        var stopwatch = Stopwatch.StartNew();
        const int iterations = 100;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            _feedbackService.GetFeedbackForTask($"task-{i % 10}");
        }

        stopwatch.Stop();
        var avgTimeMs = stopwatch.ElapsedMilliseconds / (double)iterations;

        // Assert
        Assert.True(avgTimeMs < 10, $"平均反馈查询时间 {avgTimeMs}ms 超过目标 10ms");
        
        TestOutputHelper.WriteLine($"反馈查询性能：{avgTimeMs:F2}ms/次 (目标：<10ms)");
    }

    [Fact]
    public void Benchmark_AlertTrigger_Performance()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        const int iterations = 100;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            _alertService.RaiseAlert(AlertType.PerformanceThreshold, AlertSeverity.Medium, 
                $"告警{i}", $"消息{i}");
        }

        stopwatch.Stop();
        var avgTimeMs = stopwatch.ElapsedMilliseconds / (double)iterations;

        // Assert
        Assert.True(avgTimeMs < 5, $"平均告警触发时间 {avgTimeMs}ms 超过目标 5ms");
        
        TestOutputHelper.WriteLine($"告警触发性能：{avgTimeMs:F2}ms/次 (目标：<5ms)");
    }

    [Fact]
    public void Benchmark_ThresholdCheck_Performance()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        const int iterations = 1000;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            _alertService.CheckResponseTime(3000, $"task-{i}");
            _alertService.CheckNegativeFeedbackRate(0.25, 10);
            _alertService.CheckExperimentAnomaly($"exp-{i}", 0.6);
        }

        stopwatch.Stop();
        var avgTimeMs = stopwatch.ElapsedMilliseconds / (double)iterations;

        // Assert
        Assert.True(avgTimeMs < 1, $"平均阈值检查时间 {avgTimeMs}ms 超过目标 1ms");
        
        TestOutputHelper.WriteLine($"阈值检查性能：{avgTimeMs:F2}ms/次 (目标：<1ms)");
    }

    [Fact]
    public async Task Benchmark_PatternDocumentGeneration_Performance()
    {
        // Arrange
        var patterns = new List<PatternItem>();
        for (int i = 0; i < 100; i++)
        {
            patterns.Add(new PatternItem
            {
                Name = $"模式{i}",
                Category = i % 4 == 0 ? "问题解决" : i % 4 == 1 ? "工具使用" : i % 4 == 2 ? "流程优化" : "配置调优",
                Description = $"这是模式{i}的描述",
                Score = 0.7 + (i % 3) * 0.1,
                UsageCount = i * 2,
                Status = "Verified",
                Steps = new List<string> { "步骤 1", "步骤 2", "步骤 3" },
                ApplicableScenarios = $"适用场景{i}",
                Preconditions = new List<string> { "前置条件 1", "前置条件 2" }
            });
        }

        var stopwatch = Stopwatch.StartNew();

        // Act
        var content = await _documentService.GeneratePatternDocumentationAsync(patterns);

        stopwatch.Stop();
        var totalTimeMs = stopwatch.ElapsedMilliseconds;

        // Assert
        Assert.True(totalTimeMs < 2000, $"模式文档生成时间 {totalTimeMs}ms 超过目标 2000ms");
        Assert.NotEmpty(content);
        
        TestOutputHelper.WriteLine($"模式文档生成性能：{totalTimeMs}ms (100 个模式，目标：<2000ms)");
    }

    [Fact]
    public async Task Benchmark_ExperienceDocumentGeneration_Performance()
    {
        // Arrange
        var experiences = new List<ExperienceMemoryItem>();
        for (int i = 0; i < 100; i++)
        {
            experiences.Add(new ExperienceMemoryItem
            {
                Summary = $"经验{i}",
                ExperienceType = i % 3 == 0 ? "成功" : i % 3 == 1 ? "失败" : "教训",
                Confidence = 0.5 + (i % 5) * 0.1,
                DetailedContent = $"经验{i}的详细内容，包含很多信息和细节",
                ActionableTips = new List<string> { "提示 1", "提示 2", "提示 3" }
            });
        }

        var stopwatch = Stopwatch.StartNew();

        // Act
        var content = await _documentService.GenerateExperienceDocumentationAsync(experiences);

        stopwatch.Stop();
        var totalTimeMs = stopwatch.ElapsedMilliseconds;

        // Assert
        Assert.True(totalTimeMs < 2000, $"经验文档生成时间 {totalTimeMs}ms 超过目标 2000ms");
        Assert.NotEmpty(content);
        
        TestOutputHelper.WriteLine($"经验文档生成性能：{totalTimeMs}ms (100 条经验，目标：<2000ms)");
    }

    [Fact]
    public async Task Benchmark_ReflectionReportGeneration_Performance()
    {
        // Arrange
        var reflections = new List<SelfReflectionItem>();
        var baseDate = DateTime.Now.AddDays(-30);
        for (int i = 0; i < 200; i++)
        {
            reflections.Add(new SelfReflectionItem
            {
                TaskDescription = $"任务{i}",
                TaskType = i % 4 == 0 ? "chat" : i % 4 == 1 ? "code" : i % 4 == 2 ? "file" : "search",
                Outcome = i % 5 == 0 ? "Failed" : "Success",
                ExecutionTimeMs = 500 + i * 20,
                ToolsUsed = new List<string> { "tool1", "tool2", "tool3" },
                Improvements = new List<string> { "改进 1", "改进 2" },
                ReflectionTimestamp = baseDate.AddHours(i)
            });
        }

        var stopwatch = Stopwatch.StartNew();

        // Act
        var content = await _documentService.GenerateReflectionReportAsync(
            reflections, DateTime.Now.AddDays(-30), DateTime.Now);

        stopwatch.Stop();
        var totalTimeMs = stopwatch.ElapsedMilliseconds;

        // Assert
        Assert.True(totalTimeMs < 3000, $"反思报告生成时间 {totalTimeMs}ms 超过目标 3000ms");
        Assert.NotEmpty(content);
        
        TestOutputHelper.WriteLine($"反思报告生成性能：{totalTimeMs}ms (200 条记录，目标：<3000ms)");
    }

    [Fact]
    public async Task Benchmark_DocumentExport_Performance()
    {
        // Arrange
        var content = new string('#', 10000) + "\n测试文档内容";
        var fileName = "benchmark_test.md";
        var outputDir = Path.Combine(Path.GetTempPath(), "DeerFlowBenchmarks");

        if (Directory.Exists(outputDir))
            Directory.Delete(outputDir, true);

        var stopwatch = Stopwatch.StartNew();

        // Act
        var filePath = await _documentService.ExportDocumentAsync(content, fileName, outputDir);

        stopwatch.Stop();
        var totalTimeMs = stopwatch.ElapsedMilliseconds;

        // Assert
        Assert.True(totalTimeMs < 500, $"文档导出时间 {totalTimeMs}ms 超过目标 500ms");
        Assert.True(File.Exists(filePath));
        
        TestOutputHelper.WriteLine($"文档导出性能：{totalTimeMs}ms (目标：<500ms)");

        // Cleanup
        Directory.Delete(outputDir, true);
    }

    [Fact]
    public async Task Benchmark_FeedbackStatistics_Performance()
    {
        // Arrange - Pre-populate with data
        for (int i = 0; i < 100; i++)
        {
            await _feedbackService.SubmitFeedbackAsync($"task-{i}", i % 3 == 0 ? "Like" : "Dislike", i % 5 + 1);
        }

        var stopwatch = Stopwatch.StartNew();
        const int iterations = 100;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            _feedbackService.GetFeedbackStatistics();
        }

        stopwatch.Stop();
        var avgTimeMs = stopwatch.ElapsedMilliseconds / (double)iterations;

        // Assert
        Assert.True(avgTimeMs < 20, $"平均统计计算时间 {avgTimeMs}ms 超过目标 20ms");
        
        TestOutputHelper.WriteLine($"反馈统计性能：{avgTimeMs:F2}ms/次 (目标：<20ms)");
    }

    [Fact]
    public void Benchmark_AlertQuery_Performance()
    {
        // Arrange - Pre-populate with alerts
        for (int i = 0; i < 100; i++)
        {
            _alertService.RaiseAlert(
                (AlertType)(i % 5),
                (AlertSeverity)(i % 4),
                $"告警{i}",
                $"消息{i}");
        }

        var stopwatch = Stopwatch.StartNew();
        const int iterations = 100;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            _alertService.GetAllAlerts(50);
            _alertService.GetUnacknowledgedAlerts();
        }

        stopwatch.Stop();
        var avgTimeMs = stopwatch.ElapsedMilliseconds / (double)iterations;

        // Assert
        Assert.True(avgTimeMs < 10, $"平均告警查询时间 {avgTimeMs}ms 超过目标 10ms");
        
        TestOutputHelper.WriteLine($"告警查询性能：{avgTimeMs:F2}ms/次 (目标：<10ms)");
    }
}

/// <summary>
/// 测试输出辅助类
/// </summary>
public static class TestOutputHelper
{
    public static void WriteLine(string message)
    {
        System.Console.WriteLine($"[Benchmark] {message}");
    }
}
