using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DeerFlow.WPF.Services;

/// <summary>
/// 告警类型
/// </summary>
public enum AlertType
{
    /// <summary>
    /// 负面反馈告警
    /// </summary>
    NegativeFeedback,

    /// <summary>
    /// 性能阈值告警
    /// </summary>
    PerformanceThreshold,

    /// <summary>
    /// 实验异常告警
    /// </summary>
    ExperimentAnomaly,

    /// <summary>
    /// 系统错误告警
    /// </summary>
    SystemError,

    /// <summary>
    /// 数据质量告警
    /// </summary>
    DataQuality
}

/// <summary>
/// 告警级别
/// </summary>
public enum AlertSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// 告警条目
/// </summary>
public class Alert
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public AlertType Type { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsAcknowledged { get; set; }
    public string? RelatedTaskId { get; set; }
    public Dictionary<string, object> Context { get; set; } = new();
}

/// <summary>
/// 告警服务接口
/// </summary>
public interface IAlertService
{
    /// <summary>
    /// 触发告警
    /// </summary>
    string RaiseAlert(AlertType type, AlertSeverity severity, string title, string message,
        Dictionary<string, object>? context = null);

    /// <summary>
    /// 获取所有告警
    /// </summary>
    List<Alert> GetAllAlerts(int limit = 50);

    /// <summary>
    /// 获取未确认告警
    /// </summary>
    List<Alert> GetUnacknowledgedAlerts();

    /// <summary>
    /// 确认告警
    /// </summary>
    void AcknowledgeAlert(string alertId);

    /// <summary>
    /// 获取告警统计
    /// </summary>
    Dictionary<string, object> GetAlertStatistics();

    /// <summary>
    /// 清除已确认告警
    /// </summary>
    void ClearAcknowledgedAlerts();

    /// <summary>
    /// 检查负面反馈率并触发告警
    /// </summary>
    void CheckNegativeFeedbackRate(double negativeRate, int totalFeedbackCount);

    /// <summary>
    /// 检查响应时间并触发告警
    /// </summary>
    void CheckResponseTime(long responseTimeMs, string taskId);

    /// <summary>
    /// 检查实验异常并触发告警
    /// </summary>
    void CheckExperimentAnomaly(string experimentName, double score);
}

/// <summary>
/// 智能告警服务 - 监控系统状态并触发告警
/// </summary>
public class AlertService : IAlertService
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, Alert> _alerts = new();
    private readonly object _lock = new();

    // 告警阈值配置
    private readonly double _negativeFeedbackThreshold = 0.3; // 负面反馈率超过 30% 触发告警
    private readonly int _responseTimeThresholdMs = 5000; // 响应时间超过 5 秒触发告警
    private readonly double _experimentAnomalyThreshold = 0.5; // 实验评分低于 50% 触发告警

    public AlertService(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger("AlertService");
    }

    /// <inheritdoc/>
    public string RaiseAlert(AlertType type, AlertSeverity severity, string title, string message,
        Dictionary<string, object>? context = null)
    {
        var alert = new Alert
        {
            Type = type,
            Severity = severity,
            Title = title,
            Message = message,
            CreatedAt = DateTime.Now,
            Context = context ?? new Dictionary<string, object>()
        };

        _alerts.TryAdd(alert.Id, alert);

        // 根据级别记录日志
        switch (severity)
        {
            case AlertSeverity.Critical:
                _logger.LogCritical("[Alert] {Type} - {Title}: {Message}", type, title, message);
                break;
            case AlertSeverity.High:
                _logger.LogError("[Alert] {Type} - {Title}: {Message}", type, title, message);
                break;
            case AlertSeverity.Medium:
                _logger.LogWarning("[Alert] {Type} - {Title}: {Message}", type, title, message);
                break;
            case AlertSeverity.Low:
                _logger.LogInformation("[Alert] {Type} - {Title}: {Message}", type, title, message);
                break;
        }

        return alert.Id;
    }

    /// <inheritdoc/>
    public List<Alert> GetAllAlerts(int limit = 50)
    {
        return _alerts.Values
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToList();
    }

    /// <inheritdoc/>
    public List<Alert> GetUnacknowledgedAlerts()
    {
        return _alerts.Values
            .Where(a => !a.IsAcknowledged)
            .OrderByDescending(a => a.CreatedAt)
            .ToList();
    }

    /// <inheritdoc/>
    public void AcknowledgeAlert(string alertId)
    {
        if (_alerts.TryGetValue(alertId, out var alert))
        {
            alert.IsAcknowledged = true;
            _logger.LogInformation("[AlertService] 告警已确认：{AlertId}", alertId);
        }
    }

    /// <inheritdoc/>
    public Dictionary<string, object> GetAlertStatistics()
    {
        var alertList = _alerts.Values.ToList();
        var unacknowledged = alertList.Count(a => !a.IsAcknowledged);

        return new Dictionary<string, object>
        {
            ["TotalAlerts"] = alertList.Count,
            ["UnacknowledgedCount"] = unacknowledged,
            ["AcknowledgedCount"] = alertList.Count - unacknowledged,
            ["CriticalCount"] = alertList.Count(a => a.Severity == AlertSeverity.Critical && !a.IsAcknowledged),
            ["HighCount"] = alertList.Count(a => a.Severity == AlertSeverity.High && !a.IsAcknowledged),
            ["MediumCount"] = alertList.Count(a => a.Severity == AlertSeverity.Medium && !a.IsAcknowledged),
            ["LowCount"] = alertList.Count(a => a.Severity == AlertSeverity.Low && !a.IsAcknowledged),
            ["ByType"] = alertList.GroupBy(a => a.Type)
                .ToDictionary(g => g.Key.ToString(), g => g.Count())
        };
    }

    /// <inheritdoc/>
    public void ClearAcknowledgedAlerts()
    {
        var toRemove = _alerts.Values.Where(a => a.IsAcknowledged).Select(a => a.Id).ToList();
        foreach (var id in toRemove)
        {
            _alerts.TryRemove(id, out _);
        }
        _logger.LogInformation("[AlertService] 已清除 {Count} 条已确认告警", toRemove.Count);
    }

    /// <summary>
    /// 检查负面反馈率并触发告警
    /// </summary>
    public void CheckNegativeFeedbackRate(double negativeRate, int totalFeedbackCount)
    {
        if (negativeRate > _negativeFeedbackThreshold && totalFeedbackCount >= 5)
        {
            RaiseAlert(
                type: AlertType.NegativeFeedback,
                severity: AlertSeverity.High,
                title: "负面反馈率过高",
                message: $"当前负面反馈率为 {negativeRate:P1}，超过阈值 {_negativeFeedbackThreshold:P0}",
                context: new Dictionary<string, object>
                {
                    ["NegativeRate"] = negativeRate,
                    ["TotalFeedback"] = totalFeedbackCount,
                    ["Threshold"] = _negativeFeedbackThreshold
                });
        }
    }

    /// <summary>
    /// 检查响应时间并触发告警
    /// </summary>
    public void CheckResponseTime(long responseTimeMs, string taskId)
    {
        if (responseTimeMs > _responseTimeThresholdMs)
        {
            RaiseAlert(
                type: AlertType.PerformanceThreshold,
                severity: AlertSeverity.Medium,
                title: "响应时间过长",
                message: $"任务 {taskId} 响应时间为 {responseTimeMs}ms，超过阈值 {_responseTimeThresholdMs}ms",
                context: new Dictionary<string, object>
                {
                    ["ResponseTimeMs"] = responseTimeMs,
                    ["TaskId"] = taskId,
                    ["Threshold"] = _responseTimeThresholdMs
                });
        }
    }

    /// <summary>
    /// 检查实验异常并触发告警
    /// </summary>
    public void CheckExperimentAnomaly(string experimentName, double score)
    {
        if (score < _experimentAnomalyThreshold)
        {
            RaiseAlert(
                type: AlertType.ExperimentAnomaly,
                severity: AlertSeverity.Medium,
                title: "实验效果异常",
                message: $"实验 {experimentName} 评分为 {score:P1}，低于阈值 {_experimentAnomalyThreshold:P0}",
                context: new Dictionary<string, object>
                {
                    ["ExperimentName"] = experimentName,
                    ["Score"] = score,
                    ["Threshold"] = _experimentAnomalyThreshold
                });
        }
    }
}
