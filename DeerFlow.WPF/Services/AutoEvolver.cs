using System.Collections.Concurrent;
using DeerFlow.WPF.Models;
using Microsoft.Extensions.Logging;

namespace DeerFlow.WPF.Services;

/// <summary>
/// 自动演化服务接口
/// </summary>
public interface IAutoEvolver
{
    /// <summary>
    /// 启动 A/B 测试实验
    /// </summary>
    Task<string> StartExperimentAsync(
        string strategyName,
        string optimizationTarget,
        string currentConfig,
        string candidateConfig,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 记录实验指标
    /// </summary>
    void RecordMetric(string experimentId, string metricName, double value);

    /// <summary>
    /// 结束实验并选择优胜策略
    /// </summary>
    Task<EvolutionStrategy?> ConcludeExperimentAsync(
        string experimentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前生效的策略配置
    /// </summary>
    string GetActiveConfig(string target);

    /// <summary>
    /// 获取所有实验的统计信息
    /// </summary>
    Dictionary<string, object> GetExperimentStatistics();

    /// <summary>
    /// 获取活跃的实验列表
    /// </summary>
    List<Experiment> GetActiveExperiments();

    /// <summary>
    /// 选择最优策略
    /// </summary>
    string SelectBestStrategy();
}

/// <summary>
/// 自动演化服务 - 通过 A/B 测试自动优化系统配置和策略
/// </summary>
public class AutoEvolver : IAutoEvolver
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, EvolutionStrategy> _strategies = new();
    private readonly ConcurrentDictionary<string, string> _activeConfigs = new();
    private readonly object _lock = new();
    private int _experimentCounter = 0;

    public AutoEvolver(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger("AutoEvolver");
        InitializeDefaultStrategies();
    }

    /// <inheritdoc/>
    public async Task<string> StartExperimentAsync(
        string strategyName,
        string optimizationTarget,
        string currentConfig,
        string candidateConfig,
        CancellationToken cancellationToken = default)
    {
        var experimentId = $"EXP-{Interlocked.Increment(ref _experimentCounter)}-D{DateTime.Now:yyyyMMdd}";

        var strategy = new EvolutionStrategy
        {
            Id = experimentId,
            Name = strategyName,
            Description = $"{strategyName}的 A/B 测试实验",
            OptimizationTarget = optimizationTarget,
            CurrentConfig = currentConfig,
            CandidateConfig = candidateConfig,
            ABTestGroup = "A", // 初始为 A 组
            Status = "Experimenting",
            ExperimentStartDate = DateTime.Now,
            Metrics = new Dictionary<string, double>()
        };

        _strategies.TryAdd(experimentId, strategy);
        _activeConfigs[optimizationTarget] = candidateConfig; // 启用候选配置进行测试

        _logger.LogInformation(
            "[AutoEvolver] 启动实验：{ExperimentId} - {StrategyName}",
            experimentId, strategyName);

        await Task.Yield(); // 异步完成
        return experimentId;
    }

    /// <inheritdoc/>
    public void RecordMetric(string experimentId, string metricName, double value)
    {
        if (_strategies.TryGetValue(experimentId, out var strategy))
        {
            lock (_lock)
            {
                if (!strategy.Metrics.ContainsKey(metricName))
                {
                    strategy.Metrics[metricName] = 0;
                }

                // 累积指标（可改为平均或其他聚合方式）
                strategy.Metrics[metricName] += value;
            }

            _logger.LogDebug(
                "[AutoEvolver] 记录指标：{ExperimentId} - {MetricName} = {Value}",
                experimentId, metricName, value);
        }
        else
        {
            _logger.LogWarning(
                "[AutoEvolver] 实验不存在：{ExperimentId}",
                experimentId);
        }
    }

    /// <inheritdoc/>
    public async Task<EvolutionStrategy?> ConcludeExperimentAsync(
        string experimentId,
        CancellationToken cancellationToken = default)
    {
        if (!_strategies.TryGetValue(experimentId, out var strategy))
        {
            _logger.LogWarning("[AutoEvolver] 实验不存在：{ExperimentId}", experimentId);
            return null;
        }

        if (strategy.Status != "Experimenting")
        {
            _logger.LogWarning("[AutoEvolver] 实验已结束：{ExperimentId}", experimentId);
            return strategy;
        }

        try
        {
            // 分析实验结果
            var winner = AnalyzeExperimentResults(strategy);

            strategy.ExperimentEndDate = DateTime.Now;
            strategy.Status = winner == "Candidate" ? "Adopted" : "Rejected";
            strategy.ExperimentSummary = $"实验完成 - 获胜方：{winner}";

            if (winner == "Candidate")
            {
                // 采纳候选配置
                _activeConfigs[strategy.OptimizationTarget] = strategy.CandidateConfig;
                _logger.LogInformation(
                    "[AutoEvolver] 采纳新配置：{StrategyName} - {Target}",
                    strategy.Name, strategy.OptimizationTarget);
            }
            else
            {
                // 回退到当前配置
                _activeConfigs[strategy.OptimizationTarget] = strategy.CurrentConfig;
                _logger.LogInformation(
                    "[AutoEvolver] 保留原配置：{StrategyName} - {Target}",
                    strategy.Name, strategy.OptimizationTarget);
            }

            return strategy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AutoEvolver] 实验结束失败：{ExperimentId}", experimentId);
            return null;
        }
    }

    /// <inheritdoc/>
    public string GetActiveConfig(string target)
    {
        return _activeConfigs.TryGetValue(target, out var config)
            ? config
            : string.Empty;
    }

    /// <inheritdoc/>
    public Dictionary<string, object> GetExperimentStatistics()
    {
        var strategies = _strategies.Values.ToList();

        return new Dictionary<string, object>
        {
            ["TotalExperiments"] = strategies.Count,
            ["ExperimentingCount"] = strategies.Count(s => s.Status == "Experimenting"),
            ["AdoptedCount"] = strategies.Count(s => s.Status == "Adopted"),
            ["RejectedCount"] = strategies.Count(s => s.Status == "Rejected"),
            ["SuccessRate"] = strategies.Count > 0
                ? (double)strategies.Count(s => s.Status == "Adopted") / strategies.Count
                : 0,
            ["AverageExperimentDuration"] = strategies.Any(s => s.ExperimentEndDate.HasValue)
                ? strategies
                    .Where(s => s.ExperimentEndDate.HasValue)
                    .Average(s => (s.ExperimentEndDate!.Value - s.ExperimentStartDate).TotalHours)
                : 0,
            ["ActiveTargets"] = _activeConfigs.Keys.ToList()
        };
    }

    /// <inheritdoc/>
    public List<Experiment> GetActiveExperiments()
    {
        return _strategies.Values
            .Where(s => s.Status == "Experimenting" || s.Status == "Adopted")
            .Select(s => new Experiment
            {
                Name = s.Name,
                Type = s.OptimizationTarget,
                Score = s.Metrics.Count > 0 ? s.Metrics.Average(m => m.Value) : 0,
                Status = s.Status,
                StartTime = s.ExperimentStartDate,
                EndTime = s.ExperimentEndDate,
                Description = s.Description,
                Metrics = s.Metrics
            })
            .ToList();
    }

    /// <inheritdoc/>
    public string SelectBestStrategy()
    {
        var bestStrategy = _strategies.Values
            .Where(s => s.Status == "Adopted")
            .OrderByDescending(s => s.Metrics.Count > 0 ? s.Metrics.Average(m => m.Value) : 0)
            .FirstOrDefault();

        if (bestStrategy is not null)
        {
            foreach (var kvp in bestStrategy.Metrics)
            {
                if (kvp.Value > 0.75)
                {
                    _activeConfigs[bestStrategy.OptimizationTarget] = bestStrategy.CandidateConfig;
                    _logger.LogInformation("[AutoEvolver] 采纳优胜策略：{StrategyName} - {Target} 更新为 {Config}",
                        bestStrategy.Name, bestStrategy.OptimizationTarget, bestStrategy.CandidateConfig);
                    return bestStrategy.Name;
                }
            }
        }

        return string.Empty;
    }

    #region Private Methods

    /// <summary>
    /// 初始化默认策略
    /// </summary>
    private void InitializeDefaultStrategies()
    {
        // 默认优化目标
        _activeConfigs.TryAdd("Temperature", "0.7");
        _activeConfigs.TryAdd("MaxTokens", "2048");
        _activeConfigs.TryAdd("FunctionChoiceBehavior", "Auto");
        _activeConfigs.TryAdd("VirtualizationCacheLength", "0.8");

        _logger.LogInformation("[AutoEvolver] 初始化默认策略完成");
    }

    /// <summary>
    /// 分析实验结果，返回获胜方（Current/Candidate）
    /// </summary>
    private string AnalyzeExperimentResults(EvolutionStrategy strategy)
    {
        if (strategy.Metrics.Count == 0)
        {
            _logger.LogWarning("[AutoEvolver] 无实验数据，回退到当前配置");
            return "Current";
        }

        // 基于优化目标的关键指标
        var keyMetrics = new[] { "SuccessRate", "ResponseTime", "UserSatisfaction" };
        var relevantMetrics = strategy.Metrics
            .Where(m => keyMetrics.Any(k => m.Key.Contains(k)))
            .ToList();

        if (relevantMetrics.Count == 0)
        {
            relevantMetrics = strategy.Metrics.ToList();
        }

        // 简单规则：如果候选配置在关键指标上表现更好，则采纳
        var avgPerformance = relevantMetrics.Average(m => m.Value);
        return avgPerformance > 0.75 ? "Candidate" : "Current";
    }

    #endregion
}
