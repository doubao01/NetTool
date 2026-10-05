using Xunit;
using DeerFlow.WPF.Services;
using DeerFlow.WPF.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Linq;

namespace DeerFlow.WPF.Tests.Integration;

/// <summary>
/// 集成测试 - 测试 Phase 2 新增服务的协同工作
/// </summary>
public class Phase2IntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public Phase2IntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FeedbackAndAlert_Flow_TriggerNegativeFeedbackAlert()
    {
        // Arrange
        var feedbackService = _fixture.Services.GetRequiredService<IFeedbackService>();
        var alertService = _fixture.Services.GetRequiredService<IAlertService>();

        // Act - Submit multiple negative feedbacks
        for (int i = 0; i < 5; i++)
        {
            await feedbackService.SubmitFeedbackAsync($"task-{i}", "Dislike", 1, "不好用");
        }

        // Check negative feedback rate
        var stats = feedbackService.GetFeedbackStatistics();
        var negativeRate = 1.0; // All are dislikes

        // Trigger alert check
        alertService.CheckNegativeFeedbackRate(negativeRate, 5);

        // Assert
        var alerts = alertService.GetUnacknowledgedAlerts();
        Assert.Single(alerts);
        Assert.Equal(AlertType.NegativeFeedback, alerts[0].Type);
        Assert.Equal(AlertSeverity.High, alerts[0].Severity);
    }

    [Fact]
    public async Task SelfReflectionWithAlert_ChecksPerformanceThreshold()
    {
        // Arrange
        var reflectionService = _fixture.Services.GetRequiredService<ISelfReflectionService>();
        var alertService = _fixture.Services.GetRequiredService<IAlertService>();

        // Act - Submit reflection with long execution time
        await reflectionService.ReflectOnTaskAsync(
            "test-task",
            "测试长耗时任务",
            "chat",
            isSuccess: true,
            toolsUsed: new System.Collections.Generic.List<string> { "tool1" },
            executionTimeMs: 6000 // > 5000ms threshold
        );

        // Assert
        var alerts = alertService.GetUnacknowledgedAlerts();
        var performanceAlert = alerts.FirstOrDefault(a => a.Type == AlertType.PerformanceThreshold);
        Assert.NotNull(performanceAlert);
        Assert.Contains("6000ms", performanceAlert.Message);
    }

    [Fact]
    public async Task FullWorkflow_TaskExecution_ToDocumentGeneration()
    {
        // Arrange
        var reflectionService = _fixture.Services.GetRequiredService<ISelfReflectionService>();
        var patternMiner = _fixture.Services.GetRequiredService<IPatternMiner>();
        var experienceStore = _fixture.Services.GetRequiredService<IExperienceMemoryStore>();
        var feedbackService = _fixture.Services.GetRequiredService<IFeedbackService>();
        var documentService = _fixture.Services.GetRequiredService<IDocumentGenerationService>();

        // Act 1 - Execute tasks and collect reflections
        for (int i = 0; i < 5; i++)
        {
            await reflectionService.ReflectOnTaskAsync(
                $"task-{i}",
                $"测试任务 {i}",
                "chat",
                isSuccess: true,
                toolsUsed: new System.Collections.Generic.List<string> { "tool1", "tool2" },
                executionTimeMs: 1000 + i * 100);

            await feedbackService.SubmitFeedbackAsync($"task-{i}", "Like", 5);
        }

        // Act 2 - Extract patterns
        for (int i = 0; i < 5; i++)
        {
            await reflectionService.ExtractPatternAsync(reflectionService.GetReflections(null, 10));
        }

        // Act 3 - Store experiences
        await experienceStore.SaveExperienceAsync(new ExperienceMemoryItem
        {
            Summary = "集成测试经验",
            ExperienceType = "成功",
            Confidence = 0.8,
            DetailedContent = "测试内容"
        });

        // Act 4 - Generate documents
        var patterns = patternMiner.GetPatterns(limit: 100).ToList();
        var experiences = experienceStore.GetAllExperiences(limit: 100).ToList();
        var reflections = reflectionService.GetReflections(null, 200).ToList();

        var patternDoc = await documentService.GeneratePatternDocumentationAsync(patterns);
        var experienceDoc = await documentService.GenerateExperienceDocumentationAsync(experiences);
        var reflectionDoc = await documentService.GenerateReflectionReportAsync(
            reflections, System.DateTime.Now.AddDays(-30), System.DateTime.Now);

        // Assert
        Assert.NotNull(patternDoc);
        Assert.Contains("模式库文档", patternDoc);
        Assert.NotNull(experienceDoc);
        Assert.Contains("经验记忆文档", experienceDoc);
        Assert.NotNull(reflectionDoc);
        Assert.Contains("任务反思总结报告", reflectionDoc);
    }

    [Fact]
    public void SystemHealth_CheckAllMetrics()
    {
        // Arrange
        var feedbackService = _fixture.Services.GetRequiredService<IFeedbackService>();
        var alertService = _fixture.Services.GetRequiredService<IAlertService>();
        var patternMiner = _fixture.Services.GetRequiredService<IPatternMiner>();
        var experienceStore = _fixture.Services.GetRequiredService<IExperienceMemoryStore>();
        var autoEvolver = _fixture.Services.GetRequiredService<IAutoEvolver>();

        // Act - Collect all statistics
        var fbStats = feedbackService.GetFeedbackStatistics();
        var alertStats = alertService.GetAlertStatistics();
        var patternStats = patternMiner.GetPatternStatistics();
        var expStats = experienceStore.GetStatistics();
        var experimentStats = autoEvolver.GetExperimentStatistics();

        // Assert - All should return valid statistics
        Assert.NotNull(fbStats);
        Assert.NotEmpty(fbStats);
        Assert.NotNull(alertStats);
        Assert.NotEmpty(alertStats);
        Assert.NotNull(patternStats);
        Assert.NotNull(expStats);
        Assert.NotNull(experimentStats);
    }

    [Fact]
    public async Task ExperimentAndAlert_ChecksExperimentAnomaly()
    {
        // Arrange
        var autoEvolver = _fixture.Services.GetRequiredService<IAutoEvolver>();
        var alertService = _fixture.Services.GetRequiredService<IAlertService>();

        // Act - Start experiment
        var experimentId = await autoEvolver.StartExperimentAsync(
            "test-strategy",
            "Performance",
            "{\"timeout\": 5000}",
            "{\"timeout\": 10000}");

        // Record poor metrics
        autoEvolver.RecordMetric(experimentId, "SuccessRate", 0.30); // < 50% threshold
        autoEvolver.RecordMetric(experimentId, "ResponseTime", 8000);

        // Check anomaly
        var stats = autoEvolver.GetExperimentStatistics();
        var score = 0.30; // Low score

        alertService.CheckExperimentAnomaly("test-strategy", score);

        // Assert
        var alerts = alertService.GetUnacknowledgedAlerts();
        var experimentAlert = alerts.FirstOrDefault(a => a.Type == AlertType.ExperimentAnomaly);
        Assert.NotNull(experimentAlert);
        Assert.Contains("30.0%", experimentAlert.Message);
    }

    [Fact]
    public async Task DocumentExport_FullCycle()
    {
        // Arrange
        var documentService = _fixture.Services.GetRequiredService<IDocumentGenerationService>();
        var outputDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DeerFlowIntegrationTests");

        if (System.IO.Directory.Exists(outputDir))
            System.IO.Directory.Delete(outputDir, true);

        // Act - Generate and export document
        var content = "# 集成测试文档\n测试内容";
        var filePath = await documentService.ExportDocumentAsync(content, "integration_test.md", outputDir);

        // Assert
        Assert.NotNull(filePath);
        Assert.True(System.IO.File.Exists(filePath));
        Assert.Equal(content, await System.IO.File.ReadAllTextAsync(filePath));

        // Cleanup
        System.IO.Directory.Delete(outputDir, true);
    }
}

/// <summary>
/// 集成测试夹具 - 配置 DI 容器
/// </summary>
public class IntegrationTestFixture
{
    public IServiceProvider Services { get; private set; }

    public IntegrationTestFixture()
    {
        var services = new ServiceCollection();

        // Register logging
        services.AddLogging();

        // Register Phase 2 services
        services.AddSingleton<IFeedbackService, FeedbackService>();
        services.AddSingleton<IAlertService, AlertService>();
        services.AddSingleton<IDocumentGenerationService, DocumentGenerationService>();
        services.AddSingleton<ISelfReflectionService, SelfReflectionService>();
        services.AddSingleton<IPatternMiner, PatternMiner>();
        services.AddSingleton<IExperienceMemoryStore, ExperienceMemoryStore>();
        services.AddSingleton<IAutoEvolver, AutoEvolver>();

        Services = services.BuildServiceProvider();
    }
}
