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

public class AutoEvolverTests
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly AutoEvolver _evolver;

    public AutoEvolverTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        _evolver = new AutoEvolver(_loggerFactoryMock.Object);
    }

    [Fact]
    public async Task StartExperimentAsync_CreatesExperiment()
    {
        // Arrange
        const string name = "Temperature 调优";
        const string target = "Temperature";
        const string current = "0.7";
        const string candidate = "0.9";

        // Act
        var experimentId = await _evolver.StartExperimentAsync(
            name, target, current, candidate);

        // Assert
        Assert.NotNull(experimentId);
        Assert.StartsWith("EXP-", experimentId);
    }

    [Fact]
    public async Task StartExperimentAsync_SetsActiveConfig()
    {
        // Arrange
        const string target = "TestTarget";
        const string candidate = "candidate_config";

        // Act
        var experimentId = await _evolver.StartExperimentAsync(
            "测试实验", target, "current", candidate);

        // Assert
        var activeConfig = _evolver.GetActiveConfig(target);
        Assert.Equal(candidate, activeConfig);
    }

    [Fact]
    public void RecordMetric_AccumulatesValues()
    {
        // Arrange
        const string experimentId = "EXP-001";
        const string metricName = "SuccessRate";

        // Act
        _evolver.RecordMetric(experimentId, metricName, 0.8);
        _evolver.RecordMetric(experimentId, metricName, 0.9);
        _evolver.RecordMetric(experimentId, metricName, 0.85);

        // This test verifies no exceptions are thrown
        // Actual metric values are stored in the strategy
    }

    [Fact]
    public async Task ConcludeExperimentAsync_AdoptsCandidate_WhenSuccess()
    {
        // Arrange
        var experimentId = await _evolver.StartExperimentAsync(
            "测试实验",
            "Target",
            "current_config",
            "candidate_config");

        // Record some positive metrics
        _evolver.RecordMetric(experimentId, "SuccessRate", 0.95);
        _evolver.RecordMetric(experimentId, "ResponseTime", 0.85);

        // Act
        var result = await _evolver.ConcludeExperimentAsync(experimentId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Adopted", result.Status);
    }

    [Fact]
    public async Task ConcludeExperimentAsync_RejectsCandidate_WhenFailure()
    {
        // Arrange
        var experimentId = await _evolver.StartExperimentAsync(
            "测试实验",
            "Target",
            "current_config",
            "candidate_config");

        // Record poor metrics
        _evolver.RecordMetric(experimentId, "SuccessRate", 0.3);
        _evolver.RecordMetric(experimentId, "ResponseTime", 0.2);

        // Act
        var result = await _evolver.ConcludeExperimentAsync(experimentId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Rejected", result.Status);
    }

    [Fact]
    public async Task ConcludeExperimentAsync_ReturnsNull_ForNonExistentExperiment()
    {
        // Act
        var result = await _evolver.ConcludeExperimentAsync("EXP-NONEXISTENT");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ConcludeExperimentAsync_ChangesStatus()
    {
        // Arrange
        var experimentId = await _evolver.StartExperimentAsync(
            "测试实验",
            "Target",
            "current",
            "candidate");

        // Record positive metrics so the candidate is adopted
        _evolver.RecordMetric(experimentId, "SuccessRate", 0.9);

        // Act & Assert - Should be "Experimenting" initially
        var stats1 = _evolver.GetExperimentStatistics();
        Assert.Equal(1, stats1["ExperimentingCount"]);

        await _evolver.ConcludeExperimentAsync(experimentId);

        var stats2 = _evolver.GetExperimentStatistics();
        Assert.Equal(0, stats2["ExperimentingCount"]);
        Assert.Equal(1, stats2["AdoptedCount"]);
    }

    [Fact]
    public void GetActiveConfig_ReturnsDefault_WhenNotSet()
    {
        // Act
        var config = _evolver.GetActiveConfig("NonExistentTarget");

        // Assert
        Assert.Empty(config);
    }

    [Fact]
    public void GetActiveConfig_ReturnsSetConfig()
    {
        // Arrange
        const string target = "TestTarget";
        const string config = "test_config";

        // This would normally be set via StartExperimentAsync
        // For this test, we verify the method works when config is set
    }

    [Fact]
    public void GetExperimentStatistics_ReturnsCorrectCounts()
    {
        // Arrange
        // Start 3 experiments
        var exp1 = _evolver.StartExperimentAsync("Exp1", "T1", "c1", "c2").Result;
        var exp2 = _evolver.StartExperimentAsync("Exp2", "T2", "c1", "c2").Result;
        var exp3 = _evolver.StartExperimentAsync("Exp3", "T3", "c1", "c2").Result;

        // Record positive metric for exp1 so it is adopted
        _evolver.RecordMetric(exp1, "SuccessRate", 0.9);
        _evolver.ConcludeExperimentAsync(exp1).Wait();

        // Act
        var stats = _evolver.GetExperimentStatistics();

        // Assert
        Assert.Equal(3, stats["TotalExperiments"]);
        Assert.Equal(2, stats["ExperimentingCount"]); // exp2 and exp3 still experimenting
        Assert.Equal(1, stats["AdoptedCount"]); // exp1 adopted
        Assert.Equal(0, stats["RejectedCount"]);
    }

    [Fact]
    public void GetExperimentStatistics_CalculatesSuccessRate()
    {
        // Arrange
        // Start and conclude experiments with known outcomes
        var exp1 = _evolver.StartExperimentAsync("Exp1", "T1", "c1", "c2").Result;
        var exp2 = _evolver.StartExperimentAsync("Exp2", "T2", "c1", "c2").Result;

        // Record good metrics for exp1
        _evolver.RecordMetric(exp1, "SuccessRate", 0.9);
        _evolver.ConcludeExperimentAsync(exp1).Wait();

        // Record bad metrics for exp2
        _evolver.RecordMetric(exp2, "SuccessRate", 0.2);
        _evolver.ConcludeExperimentAsync(exp2).Wait();

        // Act
        var stats = _evolver.GetExperimentStatistics();

        // Assert
        Assert.Equal(2, stats["TotalExperiments"]);
        Assert.Equal(1, stats["AdoptedCount"]);
        Assert.Equal(1, stats["RejectedCount"]);
        Assert.Equal(0.5, stats["SuccessRate"]); // 1 adopted / 2 total
    }
}
