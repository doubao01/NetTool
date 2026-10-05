using Xunit;
using Moq;
using DeerFlow.WPF.Services;
using DeerFlow.WPF.Models;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace DeerFlow.WPF.Tests.Services;

public class DocumentGenerationServiceTests
{
    private readonly DocumentGenerationService _service;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<ISelfReflectionService> _reflectionServiceMock;
    private readonly Mock<IPatternMiner> _patternMinerMock;
    private readonly Mock<IExperienceMemoryStore> _experienceStoreMock;

    public DocumentGenerationServiceTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        _reflectionServiceMock = new Mock<ISelfReflectionService>();
        _patternMinerMock = new Mock<IPatternMiner>();
        _experienceStoreMock = new Mock<IExperienceMemoryStore>();

        _service = new DocumentGenerationService(
            _loggerFactoryMock.Object,
            _reflectionServiceMock.Object,
            _patternMinerMock.Object,
            _experienceStoreMock.Object);
    }

    [Fact]
    public async Task GeneratePatternDocumentationAsync_GeneratesValidMarkdown()
    {
        // Arrange
        var patterns = new List<PatternItem>
        {
            new PatternItem
            {
                Name = "测试模式 1",
                Category = "问题解决",
                Description = "这是一个测试模式",
                Score = 0.85,
                UsageCount = 10,
                Status = "Verified",
                Steps = new List<string> { "步骤 1", "步骤 2" }
            },
            new PatternItem
            {
                Name = "测试模式 2",
                Category = "工具使用",
                Description = "这是另一个测试模式",
                Score = 0.92,
                UsageCount = 20,
                Status = "Verified"
            }
        };

        // Act
        var content = await _service.GeneratePatternDocumentationAsync(patterns);

        // Assert
        Assert.NotNull(content);
        Assert.Contains("# 模式库文档", content);
        Assert.Contains("**模式总数**: 2", content);
        Assert.Contains("测试模式 1", content);
        Assert.Contains("测试模式 2", content);
        Assert.Contains("问题解决", content);
        Assert.Contains("工具使用", content);
    }

    [Fact]
    public async Task GeneratePatternDocumentationAsync_IncludesStatistics()
    {
        // Arrange
        var patterns = new List<PatternItem>
        {
            new PatternItem { Name = "模式 1", Category = "A", Score = 0.9, UsageCount = 5 },
            new PatternItem { Name = "模式 2", Category = "A", Score = 0.8, UsageCount = 3 },
            new PatternItem { Name = "模式 3", Category = "B", Score = 0.7, UsageCount = 7 }
        };

        // Act
        var content = await _service.GeneratePatternDocumentationAsync(patterns);

        // Assert
        Assert.Contains("分类概览", content);
        Assert.Contains("**A**: 2 个模式", content);
        Assert.Contains("**B**: 1 个模式", content);
    }

    [Fact]
    public async Task GenerateExperienceDocumentationAsync_GeneratesValidMarkdown()
    {
        // Arrange
        var experiences = new List<ExperienceMemoryItem>
        {
            new ExperienceMemoryItem
            {
                Summary = "测试经验 1",
                ExperienceType = "成功",
                Confidence = 0.85,
                DetailedContent = "详细内容",
                ActionableTips = new List<string> { "提示 1", "提示 2" }
            },
            new ExperienceMemoryItem
            {
                Summary = "测试经验 2",
                ExperienceType = "教训",
                Confidence = 0.75,
                DetailedContent = "详细内容 2"
            }
        };

        // Act
        var content = await _service.GenerateExperienceDocumentationAsync(experiences);

        // Assert
        Assert.NotNull(content);
        Assert.Contains("# 经验记忆文档", content);
        Assert.Contains("**经验总数**: 2", content);
        Assert.Contains("测试经验 1", content);
        Assert.Contains("测试经验 2", content);
    }

    [Fact]
    public async Task GenerateExperienceDocumentationAsync_HighlightsHighConfidence()
    {
        // Arrange
        var experiences = new List<ExperienceMemoryItem>
        {
            new ExperienceMemoryItem { Summary = "高置信度", Confidence = 0.90, ExperienceType = "成功" },
            new ExperienceMemoryItem { Summary = "低置信度", Confidence = 0.50, ExperienceType = "失败" }
        };

        // Act
        var content = await _service.GenerateExperienceDocumentationAsync(experiences);

        // Assert
        Assert.Contains("高置信度经验（≥70%）", content);
        Assert.Contains("高置信度", content);
        Assert.DoesNotContain("低置信度", content); // Should not be in high confidence section
    }

    [Fact]
    public async Task GenerateReflectionReportAsync_GeneratesValidMarkdown()
    {
        // Arrange
        var reflections = new List<SelfReflectionItem>
        {
            new SelfReflectionItem
            {
                TaskDescription = "任务 1",
                TaskType = "chat",
                Outcome = "Success",
                ExecutionTimeMs = 1000,
                ToolsUsed = new List<string> { "tool1", "tool2" },
                ReflectionTimestamp = DateTime.Now.AddDays(-1)
            },
            new SelfReflectionItem
            {
                TaskDescription = "任务 2",
                TaskType = "code",
                Outcome = "Failed",
                ExecutionTimeMs = 5000,
                ToolsUsed = new List<string> { "tool1" },
                ReflectionTimestamp = DateTime.Now
            }
        };

        var startDate = DateTime.Now.AddDays(-30);
        var endDate = DateTime.Now;

        // Act
        var content = await _service.GenerateReflectionReportAsync(reflections, startDate, endDate);

        // Assert
        Assert.NotNull(content);
        Assert.Contains("# 任务反思总结报告", content);
        Assert.Contains("总任务数**: 2", content);
        Assert.Contains("成功任务**: 1", content);
        Assert.Contains("失败任务**: 1", content);
    }

    [Fact]
    public async Task GenerateReflectionReportAsync_CalculatesStatistics()
    {
        // Arrange
        var reflections = new List<SelfReflectionItem>
        {
            new SelfReflectionItem { Outcome = "Success", ExecutionTimeMs = 1000, ReflectionTimestamp = DateTime.Now },
            new SelfReflectionItem { Outcome = "Success", ExecutionTimeMs = 2000, ReflectionTimestamp = DateTime.Now },
            new SelfReflectionItem { Outcome = "Failed", ExecutionTimeMs = 3000, ReflectionTimestamp = DateTime.Now }
        };

        // Act
        var content = await _service.GenerateReflectionReportAsync(
            reflections, DateTime.Now.AddDays(-1), DateTime.Now);

        // Assert
        Assert.Contains("成功率**: 66.7%", content); // 2/3
        Assert.Contains("平均执行时间", content);
        Assert.Contains("性能指标", content);
    }

    [Fact]
    public async Task GenerateSystemHealthReportAsync_GeneratesValidMarkdown()
    {
        // Arrange
        var statistics = new Dictionary<string, object>
        {
            ["TotalPatterns"] = 50,
            ["TotalExperiences"] = 100,
            ["ActiveExperiments"] = 3,
            ["AverageRating"] = 4.5
        };

        // Act
        var content = await _service.GenerateSystemHealthReportAsync(statistics);

        // Assert
        Assert.NotNull(content);
        Assert.Contains("# 系统健康状态报告", content);
        Assert.Contains("TotalPatterns", content);
        Assert.Contains("50", content);
        Assert.Contains("TotalExperiences", content);
        Assert.Contains("100", content);
    }

    [Fact]
    public async Task ExportDocumentAsync_SavesToFile()
    {
        // Arrange
        var content = "# 测试文档\n这是测试内容";
        var fileName = "test_doc.md";
        var outputDir = Path.Combine(Path.GetTempPath(), "DeerFlowTests");

        // Act
        var filePath = await _service.ExportDocumentAsync(content, fileName, outputDir);

        // Assert
        Assert.NotNull(filePath);
        Assert.NotEmpty(filePath);
        Assert.True(File.Exists(filePath));
        Assert.Equal(content, await File.ReadAllTextAsync(filePath));

        // Cleanup
        File.Delete(filePath);
    }

    [Fact]
    public async Task ExportDocumentAsync_CreatesDirectory_IfNotExists()
    {
        // Arrange
        var content = "# 测试文档";
        var fileName = "test_doc.md";
        var outputDir = Path.Combine(Path.GetTempPath(), "DeerFlowTests", "NewDir");

        if (Directory.Exists(outputDir))
            Directory.Delete(outputDir, true);

        // Act
        var filePath = await _service.ExportDocumentAsync(content, fileName, outputDir);

        // Assert
        Assert.True(Directory.Exists(outputDir));
        Assert.True(File.Exists(filePath));

        // Cleanup
        Directory.Delete(outputDir, true);
    }

    [Fact]
    public async Task GenerateAllDocumentsAsync_GeneratesAllTypes()
    {
        // Arrange
        _patternMinerMock.Setup(x => x.GetPatterns(limit: 100))
            .Returns(new List<PatternItem> { new PatternItem { Name = "Test" } });
        
        _experienceStoreMock.Setup(x => x.GetAllExperiences(limit: 100))
            .Returns(new List<ExperienceMemoryItem> { new ExperienceMemoryItem { Summary = "Test" } });
        
        _reflectionServiceMock.Setup(x => x.GetReflections(null, 200))
            .Returns(new List<SelfReflectionItem> { new SelfReflectionItem { TaskDescription = "Test" } });

        // Act
        var results = await _service.GenerateAllDocumentsAsync();

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains("Patterns", results.Keys);
        Assert.Contains("Experiences", results.Keys);
        Assert.Contains("ReflectionReport", results.Keys);
    }

    [Fact]
    public async Task GeneratePatternDocumentationAsync_HandlesEmptyList()
    {
        // Arrange
        var patterns = new List<PatternItem>();

        // Act
        var content = await _service.GeneratePatternDocumentationAsync(patterns);

        // Assert
        Assert.NotNull(content);
        Assert.Contains("模式总数**: 0", content);
    }

    [Fact]
    public async Task GenerateReflectionReportAsync_FiltersByDate()
    {
        // Arrange
        var reflections = new List<SelfReflectionItem>
        {
            new SelfReflectionItem { Outcome = "Success", ReflectionTimestamp = DateTime.Now.AddDays(-2) },
            new SelfReflectionItem { Outcome = "Success", ReflectionTimestamp = DateTime.Now.AddDays(-1) },
            new SelfReflectionItem { Outcome = "Failed", ReflectionTimestamp = DateTime.Now }
        };

        var startDate = DateTime.Now.AddDays(-1).Date;
        var endDate = DateTime.Now.AddDays(1).Date;

        // Act
        var content = await _service.GenerateReflectionReportAsync(reflections, startDate, endDate);

        // Assert
        Assert.Contains("总任务数**: 2", content); // Only 2 within date range
    }
}
