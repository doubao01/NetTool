using Xunit;
using Moq;
using DeerFlow.WPF.Services;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Linq;

namespace DeerFlow.WPF.Tests.Services;

public class AlertServiceTests
{
    private readonly AlertService _service;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;

    public AlertServiceTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        _service = new AlertService(_loggerFactoryMock.Object);
    }

    [Fact]
    public void RaiseAlert_CreatesAlert_WithValidData()
    {
        // Arrange
        var type = AlertType.NegativeFeedback;
        var severity = AlertSeverity.High;
        var title = "测试告警";
        var message = "这是一个测试告警消息";

        // Act
        _service.RaiseAlert(type, severity, title, message);

        // Assert
        var alerts = _service.GetAllAlerts();
        Assert.Single(alerts);
        Assert.Equal(type, alerts[0].Type);
        Assert.Equal(severity, alerts[0].Severity);
        Assert.Equal(title, alerts[0].Title);
        Assert.Equal(message, alerts[0].Message);
    }

    [Fact]
    public void RaiseAlert_AddsContext_WhenProvided()
    {
        // Arrange
        var context = new System.Collections.Generic.Dictionary<string, object>
        {
            ["Key1"] = "Value1",
            ["Key2"] = 123
        };

        // Act
        _service.RaiseAlert(AlertType.PerformanceThreshold, AlertSeverity.Medium, 
            "性能告警", "响应时间过长", context);

        // Assert
        var alert = _service.GetAllAlerts().First();
        Assert.Equal(2, alert.Context.Count);
        Assert.Equal("Value1", alert.Context["Key1"]);
        Assert.Equal(123, alert.Context["Key2"]);
    }

    [Fact]
    public void GetUnacknowledgedAlerts_ReturnsOnlyUnacknowledged()
    {
        // Arrange
        _service.RaiseAlert(AlertType.NegativeFeedback, AlertSeverity.High, "告警 1", "消息 1");
        _service.RaiseAlert(AlertType.PerformanceThreshold, AlertSeverity.Medium, "告警 2", "消息 2");
        var alert3Id = _service.RaiseAlert(AlertType.SystemError, AlertSeverity.Critical, "告警 3", "消息 3");

        // Act
        _service.AcknowledgeAlert(alert3Id);
        var unacknowledged = _service.GetUnacknowledgedAlerts();

        // Assert
        Assert.Equal(2, unacknowledged.Count);
        Assert.DoesNotContain(unacknowledged, a => a.Id == alert3Id);
    }

    [Fact]
    public void AcknowledgeAlert_SetsAcknowledgedFlag()
    {
        // Arrange
        var alertId = _service.RaiseAlert(AlertType.NegativeFeedback, AlertSeverity.High, "测试", "消息");

        // Act
        _service.AcknowledgeAlert(alertId);

        // Assert
        var alert = _service.GetAllAlerts().First(a => a.Id == alertId);
        Assert.True(alert.IsAcknowledged);
    }

    [Fact]
    public void AcknowledgeAlert_IgnoresInvalidId()
    {
        // Act & Assert
        // Should not throw exception
        _service.AcknowledgeAlert("invalid-id");
    }

    [Fact]
    public void GetAlertStatistics_ReturnsCorrectStats()
    {
        // Arrange
        _service.RaiseAlert(AlertType.NegativeFeedback, AlertSeverity.Low, "告警 1", "消息 1");
        _service.RaiseAlert(AlertType.PerformanceThreshold, AlertSeverity.Medium, "告警 2", "消息 2");
        _service.RaiseAlert(AlertType.ExperimentAnomaly, AlertSeverity.High, "告警 3", "消息 3");
        _service.RaiseAlert(AlertType.SystemError, AlertSeverity.Critical, "告警 4", "消息 4");
        _service.RaiseAlert(AlertType.NegativeFeedback, AlertSeverity.Medium, "告警 5", "消息 5");

        // Act
        var stats = _service.GetAlertStatistics();

        // Assert
        Assert.Equal(5, stats["TotalAlerts"]);
        Assert.Equal(5, stats["UnacknowledgedCount"]);
        Assert.Equal(0, stats["AcknowledgedCount"]);
        Assert.Equal(1, stats["CriticalCount"]);
        Assert.Equal(1, stats["HighCount"]);
        Assert.Equal(2, stats["MediumCount"]);
        Assert.Equal(1, stats["LowCount"]);

        var byType = Assert.IsType<System.Collections.Generic.Dictionary<string, int>>(stats["ByType"]);
        Assert.Equal(2, byType["NegativeFeedback"]);
        Assert.Equal(1, byType["PerformanceThreshold"]);
    }

    [Fact]
    public void ClearAcknowledgedAlerts_RemovesAcknowledgedOnly()
    {
        // Arrange
        _service.RaiseAlert(AlertType.NegativeFeedback, AlertSeverity.High, "告警 1", "消息 1");
        var alert2Id = _service.RaiseAlert(AlertType.PerformanceThreshold, AlertSeverity.Medium, "告警 2", "消息 2");
        var alert3Id = _service.RaiseAlert(AlertType.SystemError, AlertSeverity.Low, "告警 3", "消息 3");

        _service.AcknowledgeAlert(alert2Id);
        _service.AcknowledgeAlert(alert3Id);

        // Act
        _service.ClearAcknowledgedAlerts();

        // Assert
        var remaining = _service.GetAllAlerts();
        Assert.Single(remaining);
        Assert.Equal("告警 1", remaining[0].Title);
    }

    [Fact]
    public void CheckNegativeFeedbackRate_TriggerAlert_WhenThresholdExceeded()
    {
        // Arrange
        var negativeRate = 0.35; // 35% > 30% threshold
        var totalFeedback = 10;

        // Act
        _service.CheckNegativeFeedbackRate(negativeRate, totalFeedback);

        // Assert
        var alerts = _service.GetAllAlerts();
        Assert.Single(alerts);
        Assert.Equal(AlertType.NegativeFeedback, alerts[0].Type);
        Assert.Equal(AlertSeverity.High, alerts[0].Severity);
    }

    [Fact]
    public void CheckNegativeFeedbackRate_NoAlert_WhenBelowThreshold()
    {
        // Arrange
        var negativeRate = 0.25; // 25% < 30% threshold
        var totalFeedback = 10;

        // Act
        _service.CheckNegativeFeedbackRate(negativeRate, totalFeedback);

        // Assert
        var alerts = _service.GetAllAlerts();
        Assert.Empty(alerts);
    }

    [Fact]
    public void CheckNegativeFeedbackRate_NoAlert_WhenInsufficientSamples()
    {
        // Arrange
        var negativeRate = 0.50; // 50% > 30% threshold
        var totalFeedback = 3; // < 5 samples

        // Act
        _service.CheckNegativeFeedbackRate(negativeRate, totalFeedback);

        // Assert
        var alerts = _service.GetAllAlerts();
        Assert.Empty(alerts);
    }

    [Fact]
    public void CheckResponseTime_TriggersAlert_WhenExceedsThreshold()
    {
        // Arrange
        var responseTimeMs = 6000L; // > 5000ms threshold
        var taskId = "test-task-123";

        // Act
        _service.CheckResponseTime(responseTimeMs, taskId);

        // Assert
        var alerts = _service.GetAllAlerts();
        Assert.Single(alerts);
        Assert.Equal(AlertType.PerformanceThreshold, alerts[0].Type);
        Assert.Equal(AlertSeverity.Medium, alerts[0].Severity);
        Assert.Contains("6000ms", alerts[0].Message);
    }

    [Fact]
    public void CheckResponseTime_NoAlert_WhenBelowThreshold()
    {
        // Arrange
        var responseTimeMs = 3000L; // < 5000ms threshold
        var taskId = "test-task-123";

        // Act
        _service.CheckResponseTime(responseTimeMs, taskId);

        // Assert
        var alerts = _service.GetAllAlerts();
        Assert.Empty(alerts);
    }

    [Fact]
    public void CheckExperimentAnomaly_TriggersAlert_WhenBelowThreshold()
    {
        // Arrange
        var experimentName = "test-experiment";
        var score = 0.40; // < 50% threshold

        // Act
        _service.CheckExperimentAnomaly(experimentName, score);

        // Assert
        var alerts = _service.GetAllAlerts();
        Assert.Single(alerts);
        Assert.Equal(AlertType.ExperimentAnomaly, alerts[0].Type);
        Assert.Equal(AlertSeverity.Medium, alerts[0].Severity);
        Assert.Contains("40.0%", alerts[0].Message);
    }

    [Fact]
    public void CheckExperimentAnomaly_NoAlert_WhenAboveThreshold()
    {
        // Arrange
        var experimentName = "test-experiment";
        var score = 0.60; // > 50% threshold

        // Act
        _service.CheckExperimentAnomaly(experimentName, score);

        // Assert
        var alerts = _service.GetAllAlerts();
        Assert.Empty(alerts);
    }
}
