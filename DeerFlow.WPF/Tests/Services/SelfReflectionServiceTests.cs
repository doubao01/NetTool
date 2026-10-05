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

public class SelfReflectionServiceTests
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly SelfReflectionService _service;

    public SelfReflectionServiceTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        _service = new SelfReflectionService(_loggerFactoryMock.Object);
    }

    [Fact]
    public async Task ReflectOnTaskAsync_CreatesReflection_WithBasicInfo()
    {
        // Arrange
        const string taskId = "task-001";
        const string description = "执行代码分析任务";
        const string taskType = "code";
        const long executionTime = 1500;

        // Act
        var result = await _service.ReflectOnTaskAsync(
            taskId,
            description,
            taskType,
            isSuccess: true,
            toolsUsed: new List<string> { "sandbox", "memory" },
            executionTimeMs: executionTime);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(taskId, result.TaskId);
        Assert.Equal(description, result.TaskDescription);
        Assert.Equal(taskType, result.TaskType);
        Assert.Equal("Success", result.Outcome);
        Assert.Equal(1.0, result.SuccessRate);
        Assert.Equal(executionTime, result.ExecutionTimeMs);
        Assert.Equal(2, result.ToolsUsed.Count);
    }

    [Fact]
    public async Task ReflectOnTaskAsync_FailedTask_LowSuccessRate()
    {
        // Arrange
        const string taskId = "task-002";

        // Act
        var result = await _service.ReflectOnTaskAsync(
            taskId,
            "失败的任务",
            "chat",
            isSuccess: false,
            toolsUsed: new List<string>(),
            executionTimeMs: 5000);

        // Assert
        Assert.Equal("Failed", result.Outcome);
        Assert.Equal(0.0, result.SuccessRate);
        Assert.NotEmpty(result.Improvements); // 基础反思会添加改进建议
    }

    [Fact]
    public async Task ReflectOnTaskAsync_LongExecutionTime_AddsImprovement()
    {
        // Arrange
        const string taskId = "task-003";

        // Act
        var result = await _service.ReflectOnTaskAsync(
            taskId,
            "长时间任务",
            "file",
            isSuccess: true,
            toolsUsed: new List<string>(),
            executionTimeMs: 6000); // > 5000ms

        // Assert
        Assert.Contains(result.Improvements, i => i.Contains("执行时间过长"));
    }

    [Fact]
    public async Task ReflectOnTaskAsync_ManyTools_AddsImprovement()
    {
        // Arrange
        const string taskId = "task-004";
        var tools = new List<string> { "tool1", "tool2", "tool3", "tool4", "tool5", "tool6" };

        // Act
        var result = await _service.ReflectOnTaskAsync(
            taskId,
            "多工具任务",
            "search",
            isSuccess: true,
            toolsUsed: tools,
            executionTimeMs: 1000);

        // Assert
        Assert.Contains(result.Improvements, i => i.Contains("使用了过多工具"));
    }

    [Fact]
    public void GetReflections_ReturnsAllReflections_ByDefault()
    {
        // Arrange
        const int reflectionCount = 10;

        // Act & Assert
        for (int i = 0; i < reflectionCount; i++)
        {
            _service.ReflectOnTaskAsync(
                $"task-{i}",
                $"任务{i}",
                "chat",
                true,
                new List<string>(),
                1000).Wait();
        }

        var reflections = _service.GetReflections();
        Assert.Equal(reflectionCount, reflections.Count());
    }

    [Fact]
    public void GetReflections_FiltersByTaskType()
    {
        // Arrange
        _service.ReflectOnTaskAsync("task-1", "任务 1", "code", true, new List<string>(), 1000).Wait();
        _service.ReflectOnTaskAsync("task-2", "任务 2", "chat", true, new List<string>(), 1000).Wait();
        _service.ReflectOnTaskAsync("task-3", "任务 3", "code", true, new List<string>(), 1000).Wait();

        // Act
        var codeReflections = _service.GetReflections(taskType: "code");

        // Assert
        Assert.Equal(2, codeReflections.Count());
        Assert.All(codeReflections, r => Assert.Equal("code", r.TaskType));
    }

    [Fact]
    public void GetReflections_LimitsResults()
    {
        // Arrange
        for (int i = 0; i < 100; i++)
        {
            _service.ReflectOnTaskAsync($"task-{i}", $"任务{i}", "chat", true, new List<string>(), 1000).Wait();
        }

        // Act
        var reflections = _service.GetReflections(limit: 10);

        // Assert
        Assert.Equal(10, reflections.Count());
    }

    [Fact]
    public async Task ExtractPatternAsync_ReturnsNull_WithInsufficientReflections()
    {
        // Arrange
        var reflections = new List<SelfReflectionItem>
        {
            new SelfReflectionItem { TaskId = "1", TaskType = "code", SuccessRate = 0.9 },
            new SelfReflectionItem { TaskId = "2", TaskType = "code", SuccessRate = 0.8 }
        }; // Only 2 reflections, need >= 3

        // Act
        var result = await _service.ExtractPatternAsync(reflections);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ExtractPatternAsync_CreatesPattern_WithSufficientReflections()
    {
        // Arrange
        var reflections = new List<SelfReflectionItem>
        {
            new SelfReflectionItem { TaskId = "1", TaskType = "code", SuccessRate = 0.9, ToolsUsed = new List<string> { "sandbox" } },
            new SelfReflectionItem { TaskId = "2", TaskType = "code", SuccessRate = 0.85, ToolsUsed = new List<string> { "sandbox" } },
            new SelfReflectionItem { TaskId = "3", TaskType = "code", SuccessRate = 0.95, ToolsUsed = new List<string> { "sandbox" } }
        };

        // Act
        var result = await _service.ExtractPatternAsync(reflections);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Verified", result.Status);
        Assert.Equal(3, result.SourceReflectionIds.Count);
        Assert.Equal("代码执行", result.Category);
        Assert.InRange(result.Score, 0.8, 1.0);
    }

    [Fact]
    public void RecordPatternUsage_IncrementsUsageCount()
    {
        // Arrange
        var reflections = new List<SelfReflectionItem>
        {
            new SelfReflectionItem { TaskId = "1", TaskType = "code", SuccessRate = 0.9, ToolsUsed = new List<string> { "sandbox" } },
            new SelfReflectionItem { TaskId = "2", TaskType = "code", SuccessRate = 0.85, ToolsUsed = new List<string> { "sandbox" } },
            new SelfReflectionItem { TaskId = "3", TaskType = "code", SuccessRate = 0.95, ToolsUsed = new List<string> { "sandbox" } }
        };

        var pattern = _service.ExtractPatternAsync(reflections).Result;
        Assert.NotNull(pattern);

        // Act
        _service.RecordPatternUsage(pattern.Id, isSuccess: true);

        // Assert
        var patterns = _service.GetPatterns();
        var updatedPattern = patterns.First(p => p.Id == pattern.Id);
        Assert.Equal(1, updatedPattern.UsageCount);
        Assert.Equal(1, updatedPattern.SuccessCount);
    }
}
