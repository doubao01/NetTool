using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeerFlow.WPF.Models;
using DeerFlow.WPF.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeerFlow.WPF.Tests.Services;

public class PatternMinerTests
{
    private readonly Mock<ISelfReflectionService> _reflectionServiceMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly PatternMiner _miner;

    public PatternMinerTests()
    {
        _reflectionServiceMock = new Mock<ISelfReflectionService>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        _miner = new PatternMiner(_reflectionServiceMock.Object, _loggerFactoryMock.Object);
    }

    [Fact]
    public async Task MinePatternsAsync_ReturnsEmpty_WithInsufficientTasks()
    {
        // Arrange
        var emptyReflections = new List<SelfReflectionItem>();
        _reflectionServiceMock
            .Setup(x => x.GetReflections(It.IsAny<string>(), 1000))
            .Returns(emptyReflections);

        // Act
        var result = await _miner.MinePatternsAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task MinePatternsAsync_ExtractsPatterns_FromSuccessfulTasks()
    {
        // Arrange
        var reflections = new List<SelfReflectionItem>
        {
            CreateReflection("1", "code", 0.9, new List<string> { "sandbox", "memory" }),
            CreateReflection("2", "code", 0.85, new List<string> { "sandbox", "memory" }),
            CreateReflection("3", "code", 0.95, new List<string> { "sandbox", "memory" }),
            CreateReflection("4", "code", 0.88, new List<string> { "sandbox" }),
            CreateReflection("5", "code", 0.9, new List<string> { "sandbox", "memory" }),
            CreateReflection("6", "code", 0.86, new List<string> { "sandbox", "memory" }),
            CreateReflection("7", "code", 0.92, new List<string> { "sandbox", "memory" }),
            CreateReflection("8", "code", 0.87, new List<string> { "sandbox" }),
            CreateReflection("9", "code", 0.91, new List<string> { "sandbox", "memory" }),
            CreateReflection("10", "code", 0.84, new List<string> { "sandbox", "memory" }),
            CreateReflection("11", "code", 0.93, new List<string> { "sandbox", "memory" }),
            CreateReflection("12", "code", 0.89, new List<string> { "sandbox" }),
        };

        _reflectionServiceMock
            .Setup(x => x.GetReflections(It.IsAny<string>(), 1000))
            .Returns(reflections);

        _reflectionServiceMock
            .Setup(x => x.ExtractPatternAsync(It.IsAny<IEnumerable<SelfReflectionItem>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<SelfReflectionItem> refls, CancellationToken ct) =>
            {
                var r = refls.ToList();
                return new PatternItem
                {
                    Name = "代码执行模式",
                    Category = "代码执行",
                    Score = r.Average(x => x.SuccessRate),
                    SourceReflectionIds = r.Select(x => x.TaskId).ToList()
                };
            });

        // Act
        var result = await _miner.MinePatternsAsync(minSupport: 3, minConfidence: 0.7);

        // Assert
        Assert.NotEmpty(result);
    }

    [Fact]
    public void RecommendPattern_ReturnsBestMatch()
    {
        // Arrange
        var patterns = new List<PatternItem>
        {
            CreatePattern("pattern-1", "代码执行", 0.9, 10),
            CreatePattern("pattern-2", "代码执行", 0.85, 5),
            CreatePattern("pattern-3", "网络搜索", 0.95, 20)
        };

        var minedPatterns = new System.Collections.Concurrent.ConcurrentDictionary<string, PatternItem>();
        foreach (var pattern in patterns)
        {
            minedPatterns.TryAdd(pattern.Id, pattern);
        }

        typeof(PatternMiner)
            .GetField("_minedPatterns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(_miner, minedPatterns);

        // Act
        var result = _miner.RecommendPattern("帮我执行代码", "code");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("代码执行", result.Category);
    }

    [Fact]
    public void RecommendPattern_ReturnsNull_WhenNoRelevantPattern()
    {
        // Arrange
        var patterns = new List<PatternItem>
        {
            CreatePattern("pattern-1", "网络搜索", 0.95, 20)
        };

        var minedPatterns = new System.Collections.Concurrent.ConcurrentDictionary<string, PatternItem>();
        foreach (var pattern in patterns)
        {
            minedPatterns.TryAdd(pattern.Id, pattern);
        }

        typeof(PatternMiner)
            .GetField("_minedPatterns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(_miner, minedPatterns);

        // Act
        var result = _miner.RecommendPattern("帮我执行代码", "code");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetPatternStatistics_ReturnsCorrectStats()
    {
        // Arrange
        var patterns = new List<PatternItem>
        {
            CreatePattern("1", "代码执行", 0.9, 10, "Verified"),
            CreatePattern("2", "代码执行", 0.85, 5, "Verified"),
            CreatePattern("3", "网络搜索", 0.7, 2, "Draft"),
            CreatePattern("4", "文件操作", 0.95, 20, "Archived")
        };

        var minedPatterns = new System.Collections.Concurrent.ConcurrentDictionary<string, PatternItem>();
        foreach (var pattern in patterns)
        {
            minedPatterns.TryAdd(pattern.Id, pattern);
        }

        typeof(PatternMiner)
            .GetField("_minedPatterns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(_miner, minedPatterns);

        // Act
        var stats = _miner.GetPatternStatistics();

        // Assert
        Assert.Equal(4, stats["TotalPatterns"]);
        Assert.Equal(2, stats["VerifiedPatterns"]);
        Assert.Equal(1, stats["DraftPatterns"]);
        Assert.Equal(1, stats["ArchivedPatterns"]);
        Assert.Equal(37, stats["TotalUsageCount"]);
        Assert.All(patterns, p => Assert.True((double)stats["AverageScore"] >= 0.7 && (double)stats["AverageScore"] <= 0.95));
    }

    #region Helper Methods

    private static SelfReflectionItem CreateReflection(
        string taskId,
        string taskType,
        double successRate,
        List<string> tools)
    {
        return new SelfReflectionItem
        {
            TaskId = taskId,
            TaskType = taskType,
            SuccessRate = successRate,
            ToolsUsed = tools,
            ExecutionTimeMs = 1000,
            Outcome = successRate >= 0.8 ? "Success" : "Failed"
        };
    }

    private static PatternItem CreatePattern(
        string id,
        string category,
        double score,
        int usageCount,
        string status = "Verified")
    {
        return new PatternItem
        {
            Id = id,
            Name = $"{category}模式",
            Category = category,
            Score = score,
            UsageCount = usageCount,
            SuccessCount = (int)(usageCount * score),
            Status = status
        };
    }

    #endregion
}
