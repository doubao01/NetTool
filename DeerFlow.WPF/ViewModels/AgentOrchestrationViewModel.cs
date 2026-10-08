using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeerFlow.WPF.Core;
using DeerFlow.WPF.Models;
using DeerFlow.WPF.Services;

namespace DeerFlow.WPF.ViewModels;

/// <summary>
/// 智能体编排 ViewModel，管理多步骤工作流与子智能体编排
/// </summary>
public class AgentOrchestrationViewModel : ViewModelBase
{
    private readonly ITaskWindowManager _taskManager;
    private readonly IAgentLoop _agentLoop;
    private readonly ILoggerService _logger;
    private readonly ExecutionHistoryStore? _historyStore;

    private CancellationTokenSource? _runCts;

    private string _workflowName = string.Empty;
    public string WorkflowName
    {
        get => _workflowName;
        set => SetProperty(ref _workflowName, value);
    }

    private string _workflowSteps = string.Empty;
    /// <summary>工作流步骤，每行一个子目标</summary>
    public string WorkflowSteps
    {
        get => _workflowSteps;
        set => SetProperty(ref _workflowSteps, value);
    }

    private string _goal = string.Empty;
    /// <summary>总体目标（可选，作为各步骤上下文的公共前缀）</summary>
    public string Goal
    {
        get => _goal;
        set => SetProperty(ref _goal, value);
    }

    private int _maxSubAgents = 3;
    public int MaxSubAgents
    {
        get => _maxSubAgents;
        set => SetProperty(ref _maxSubAgents, value);
    }

    private bool _isRunning;
    /// <summary>工作流是否正在运行</summary>
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
            {
                StartWorkflowCommand.RaiseCanExecuteChanged();
                CancelWorkflowCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private string _statusText = "就绪";
    /// <summary>运行状态文本</summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private string _resultText = string.Empty;
    /// <summary>运行结果汇总</summary>
    public string ResultText
    {
        get => _resultText;
        set => SetProperty(ref _resultText, value);
    }

    /// <summary>逐步执行轨迹</summary>
    public ObservableCollection<string> ExecutionLog { get; } = new();

    /// <summary>历史运行记录摘要行（含记录 ID 前缀，如 #12）</summary>
    public ObservableCollection<string> HistoryLog { get; } = new();

    private long? _selectedHistoryId;

    /// <summary>选中的历史行文本，用于解析记录 ID</summary>
    private string _selectedHistoryEntry = string.Empty;
    public string SelectedHistoryEntry
    {
        get => _selectedHistoryEntry;
        set
        {
            if (SetProperty(ref _selectedHistoryEntry, value))
            {
                _selectedHistoryId = ParseHistoryId(value);
                LoadSelectedHistoryCommand.RaiseCanExecuteChanged();
                DeleteSelectedHistoryCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public RelayCommand CreateWorkflowCommand { get; }
    public RelayCommand PlanSubGoalsCommand { get; }
    public RelayCommand StartWorkflowCommand { get; }
    public RelayCommand CancelWorkflowCommand { get; }
    public RelayCommand IncreaseMaxSubAgentsCommand { get; }
    public RelayCommand DecreaseMaxSubAgentsCommand { get; }
    public RelayCommand RefreshHistoryCommand { get; }
    public RelayCommand LoadSelectedHistoryCommand { get; }
    public RelayCommand DeleteSelectedHistoryCommand { get; }

    /// <summary>最大子智能体数的最小值</summary>
    private const int MIN_SUB_AGENTS = 1;

    /// <summary>最大子智能体数的最大值</summary>
    private const int MAX_SUB_AGENTS = 20;

    public AgentOrchestrationViewModel(
        ITaskWindowManager taskManager,
        IAgentLoop agentLoop,
        ILoggerService logger,
        ExecutionHistoryStore? historyStore = null)
    {
        _taskManager = taskManager;
        _agentLoop = agentLoop;
        _logger = logger;
        _historyStore = historyStore;

        CreateWorkflowCommand = new RelayCommand(_ => CreateWorkflow());
        PlanSubGoalsCommand = new RelayCommand(async _ => await PlanSubGoalsAsync(), _ => !IsRunning);
        StartWorkflowCommand = new RelayCommand(async _ => await StartWorkflowAsync(), _ => !IsRunning);
        CancelWorkflowCommand = new RelayCommand(_ => CancelWorkflow(), _ => IsRunning);
        IncreaseMaxSubAgentsCommand = new RelayCommand(_ =>
        {
            if (MaxSubAgents < MAX_SUB_AGENTS)
                MaxSubAgents++;
        });
        DecreaseMaxSubAgentsCommand = new RelayCommand(_ =>
        {
            if (MaxSubAgents > MIN_SUB_AGENTS)
                MaxSubAgents--;
        });
        RefreshHistoryCommand = new RelayCommand(_ => RefreshHistory());
        LoadSelectedHistoryCommand = new RelayCommand(_ => LoadSelectedHistory(), _ => _selectedHistoryId.HasValue);
        DeleteSelectedHistoryCommand = new RelayCommand(_ => DeleteSelectedHistory(), _ => _selectedHistoryId.HasValue);

        RefreshHistory();
    }

    /// <summary>
    /// 创建（校验）工作流定义
    /// </summary>
    private void CreateWorkflow()
    {
        var steps = ParseSteps();
        if (steps.Count == 0)
        {
            StatusText = "工作流为空：请在步骤中至少填写一行子目标";
            _logger.Warn("创建编排失败：未定义步骤");
            return;
        }

        WorkflowName = string.IsNullOrWhiteSpace(WorkflowName)
            ? $"工作流-{DateTime.Now:HHmmss}"
            : WorkflowName;

        StatusText = $"工作流就绪：{WorkflowName}，共 {steps.Count} 步";
        _logger.Info($"创建编排: {WorkflowName}（{steps.Count} 步）");
    }

    /// <summary>
    /// 用 AI 把总体目标分解为编排步骤，写入步骤文本框
    /// </summary>
    private async Task PlanSubGoalsAsync()
    {
        if (string.IsNullOrWhiteSpace(Goal))
        {
            StatusText = "请先填写总体目标，再进行 AI 分解";
            return;
        }

        IsRunning = true;
        StatusText = "AI 分解中...";
        ExecutionLog.Clear();
        _logger.Info($"AI 分解编排目标: {Goal}");

        try
        {
            _runCts?.Dispose();
            _runCts = new CancellationTokenSource();

            var config = BuildConfig();
            var subGoals = await _agentLoop.PlanSubGoalsAsync(Goal, config, _runCts.Token);

            WorkflowSteps = string.Join(Environment.NewLine, subGoals);
            WorkflowName = string.IsNullOrWhiteSpace(WorkflowName)
                ? $"工作流-{DateTime.Now:HHmmss}"
                : WorkflowName;

            StatusText = $"AI 分解完成：生成 {subGoals.Count} 个步骤";
            _logger.Info($"AI 分解完成: {subGoals.Count} 个步骤");
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消";
        }
        catch (Exception ex)
        {
            StatusText = $"AI 分解失败：{ex.Message}";
            _logger.Error("AI 分解编排目标失败", ex);
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// 启动工作流：串行执行各步骤；当允许多个子智能体时并发执行各步骤
    /// </summary>
    private async Task StartWorkflowAsync()
    {
        var steps = ParseSteps();
        if (steps.Count == 0)
        {
            StatusText = "工作流为空：请在步骤中至少填写一行子目标";
            return;
        }

        IsRunning = true;
        ResultText = string.Empty;
        ExecutionLog.Clear();

        _runCts?.Dispose();
        _runCts = new CancellationTokenSource();

        var config = BuildConfig();
        var logLines = new List<string>();
        var progress = new Progress<AgentStep>(step =>
        {
            var line = FormatStep(step);
            ExecutionLog.Add(line);
            logLines.Add(line);
        });

        StatusText = $"运行中：{WorkflowName}（{steps.Count} 步，并发 {config.MaxSubAgents}）";
        _logger.Info($"启动编排工作流: {WorkflowName}");

        try
        {
            var goals = BuildGoals(steps);
            List<AgentLoopResult> results;
            var runStatus = "完成";
            var runStartedAt = DateTime.Now;

            if (config.EnableSubAgents && goals.Count > 1)
            {
                results = await _agentLoop.RunSubAgentsAsync(goals, config, progress, _runCts.Token);
            }
            else
            {
                results = new List<AgentLoopResult>();
                foreach (var goal in goals)
                {
                    results.Add(await _agentLoop.RunAsync(goal, config, progress, _runCts.Token));
                }
            }

            WriteSummary(goals, results);
            await AppendAiSummaryAsync(goals, results, config, _runCts.Token);

            var completed = results.Count(r => r.Completed);
            StatusText = $"完成：{completed}/{results.Count} 步成功收敛";
            _logger.Info($"编排工作流结束: {WorkflowName}，{completed}/{results.Count} 成功");

            await SaveRunHistoryAsync(runStatus, goals, results, logLines, runStartedAt);
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消";
            _logger.Info($"编排工作流已取消: {WorkflowName}");
            await SaveRunHistoryAsync("已取消", new List<string>(), new List<AgentLoopResult>(), logLines, DateTime.Now);
        }
        catch (Exception ex)
        {
            StatusText = $"失败：{ex.Message}";
            _logger.Error($"编排工作流失败: {WorkflowName}", ex);
            await SaveRunHistoryAsync("失败", new List<string>(), new List<AgentLoopResult>(), logLines, DateTime.Now, ex.Message);
        }
        finally
        {
            IsRunning = false;
            _runCts?.Dispose();
            _runCts = null;
        }
    }

    /// <summary>
    /// 在本地汇总之后追加 AI 生成的执行总结
    /// </summary>
    private async Task AppendAiSummaryAsync(
        List<string> goals,
        List<AgentLoopResult> results,
        AgentConfig config,
        CancellationToken cancellationToken)
    {
        try
        {
            var summary = await _agentLoop.SummarizeResultsAsync(Goal, goals, results, config, cancellationToken);
            ResultText = $"{ResultText}{Environment.NewLine}## AI 总结{Environment.NewLine}{summary}{Environment.NewLine}";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error("AI 汇总失败，仅保留本地汇总", ex);
        }
    }

    /// <summary>
    /// 取消当前工作流
    /// </summary>
    private void CancelWorkflow()
    {
        _runCts?.Cancel();
        StatusText = "正在取消...";
    }

    /// <summary>
    /// 把本次运行落库到执行历史（存储不可用时静默降级，仅记日志）
    /// </summary>
    private async Task SaveRunHistoryAsync(
        string status,
        List<string> goals,
        List<AgentLoopResult> results,
        List<string> logLines,
        DateTime startedAt,
        string? error = null)
    {
        if (_historyStore is null)
        {
            return;
        }

        try
        {
            var record = new ExecutionHistoryRecord
            {
                WorkflowName = string.IsNullOrWhiteSpace(WorkflowName) ? "未命名工作流" : WorkflowName,
                Goal = Goal ?? string.Empty,
                SubGoalsJson = System.Text.Json.JsonSerializer.Serialize(goals),
                CompletedSteps = results.Count(r => r.Completed),
                TotalSteps = results.Count,
                ToolCallCount = results.Sum(r => r.Steps.Sum(s => s.ToolCalls.Count)),
                ToolErrorCount = results.Sum(r => r.Steps.Sum(s => s.ToolErrors.Count)),
                TotalElapsedMs = results.Sum(r => r.TotalElapsedMs),
                ExecutionLog = string.Join(Environment.NewLine, logLines),
                ResultSummary = BuildHistoryResultSummary(results, error),
                Status = status,
                CreatedAt = startedAt
            };

            await _historyStore.AddAsync(record);
            RefreshHistory();
            _logger.Info($"执行历史已保存: {record.WorkflowName}（{status}）");
        }
        catch (Exception ex)
        {
            _logger.Error("保存执行历史失败", ex);
        }
    }

    private static string BuildHistoryResultSummary(List<AgentLoopResult> results, string? error)
    {
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < results.Count; i++)
        {
            var result = results[i];
            builder.AppendLine($"[{i + 1}] {result.StopReason} ({result.TotalElapsedMs}ms): {result.Answer}");
        }

        if (!string.IsNullOrEmpty(error))
        {
            builder.AppendLine($"错误: {error}");
        }

        return builder.ToString();
    }

    /// <summary>
    /// 从历史行文本解析记录 ID（形如 "#12 ..."）
    /// </summary>
    private static long? ParseHistoryId(string? entry)
    {
        if (string.IsNullOrWhiteSpace(entry) || entry.Length < 2 || entry[0] != '#')
        {
            return null;
        }

        var span = entry.AsSpan(1);
        var end = 0;
        while (end < span.Length && char.IsDigit(span[end]))
        {
            end++;
        }

        return end > 0 && long.TryParse(span[..end], out var id) ? id : null;
    }

    /// <summary>
    /// 从存储刷新历史列表
    /// </summary>
    private void RefreshHistory()
    {
        HistoryLog.Clear();
        _selectedHistoryId = null;
        SelectedHistoryEntry = string.Empty;
        LoadSelectedHistoryCommand.RaiseCanExecuteChanged();
        DeleteSelectedHistoryCommand.RaiseCanExecuteChanged();

        if (_historyStore is null)
        {
            return;
        }

        try
        {
            foreach (var record in _historyStore.GetRecent(50))
            {
                HistoryLog.Add(FormatHistoryEntry(record));
            }
        }
        catch (Exception ex)
        {
            _logger.Error("读取执行历史失败", ex);
        }
    }

    private static string FormatHistoryEntry(ExecutionHistoryRecord record)
    {
        return $"#{record.Id} [{record.Status}] {record.WorkflowName} " +
               $"({record.CompletedSteps}/{record.TotalSteps} 步, 工具 {record.ToolCallCount} 次/失败 {record.ToolErrorCount}, " +
               $"{record.TotalElapsedMs}ms, {record.CreatedAt:MM-dd HH:mm:ss})";
    }

    /// <summary>
    /// 加载选中的历史记录到轨迹/结果区
    /// </summary>
    private void LoadSelectedHistory()
    {
        if (_historyStore is null || _selectedHistoryId is null)
        {
            return;
        }

        try
        {
            var record = _historyStore.GetRecent(50).FirstOrDefault(r => r.Id == _selectedHistoryId.Value);
            if (record is null)
            {
                StatusText = $"历史记录 #{_selectedHistoryId} 已不存在";
                return;
            }

            ExecutionLog.Clear();
            foreach (var line in record.ExecutionLog.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                ExecutionLog.Add(line);
            }

            ResultText = $"## 历史 #{record.Id}: {record.WorkflowName}（{record.Status}）{Environment.NewLine}{record.ResultSummary}";
            StatusText = $"已加载历史 #{record.Id}: {record.WorkflowName}";
        }
        catch (Exception ex)
        {
            _logger.Error($"加载执行历史 #{_selectedHistoryId} 失败", ex);
        }
    }

    /// <summary>
    /// 删除选中的历史记录
    /// </summary>
    private async void DeleteSelectedHistory()
    {
        if (_historyStore is null || _selectedHistoryId is null)
        {
            return;
        }

        try
        {
            await _historyStore.DeleteAsync(_selectedHistoryId.Value);
            _logger.Info($"已删除执行历史 #{_selectedHistoryId}");
            RefreshHistory();
            StatusText = $"已删除历史 #{_selectedHistoryId}";
        }
        catch (Exception ex)
        {
            _logger.Error($"删除执行历史 #{_selectedHistoryId} 失败", ex);
        }
    }

    /// <summary>
    /// 解析步骤文本（每行一个子目标，忽略空行与 # 注释）
    /// </summary>
    private List<string> ParseSteps()
    {
        return (WorkflowSteps ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();
    }

    /// <summary>
    /// 拼接每一步的完整目标：总体目标 + 当前步骤
    /// </summary>
    private List<string> BuildGoals(List<string> steps)
    {
        var prefix = Goal?.Trim();
        return steps
            .Select(step => string.IsNullOrEmpty(prefix) ? step : $"{prefix}\n当前步骤：{step}")
            .ToList();
    }

    /// <summary>
    /// 由当前 UI 设置构造智能体配置
    /// </summary>
    private AgentConfig BuildConfig()
    {
        var config = BuildConfigFromSettings();
        config.MaxSubAgents = MaxSubAgents;
        config.EnableSubAgents = MaxSubAgents > 1;
        return config;
    }

    /// <summary>
    /// 从全局设置读取智能体配置基础项
    /// </summary>
    private static AgentConfig BuildConfigFromSettings()
    {
        try
        {
            var optionsProvider = App.Services?.GetService(typeof(IAppOptionsProvider)) as IAppOptionsProvider;
            var options = optionsProvider?.Options;
            if (options is null)
            {
                return new AgentConfig();
            }

            return new AgentConfig
            {
                Provider = options.DefaultProvider,
                ModelName = options.DefaultModel,
                BaseUrl = options.DefaultApiBaseUrl
            };
        }
        catch
        {
            return new AgentConfig();
        }
    }

    /// <summary>
    /// 把单步轨迹格式化为一行日志
    /// </summary>
    private static string FormatStep(AgentStep step)
    {
        var agent = string.IsNullOrEmpty(step.AgentName)
            ? string.Empty
            : $"[{step.AgentName}] ";
        var tools = step.ToolCalls.Count > 0 ? string.Join(", ", step.ToolCalls) : "无";
        var errors = step.ToolErrors.Count > 0 ? $"（工具失败 {step.ToolErrors.Count}）" : string.Empty;
        var mark = step.IsFinal ? " [完成]" : string.Empty;
        return $"{agent}#{step.Index} ({step.ElapsedMs}ms) 工具: {tools}{errors}{mark}";
    }

    /// <summary>
    /// 写出工作流执行汇总
    /// </summary>
    private void WriteSummary(List<string> goals, List<AgentLoopResult> results)
    {
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < goals.Count; i++)
        {
            var goal = goals[i];
            var result = i < results.Count ? results[i] : null;

            builder.AppendLine($"## 步骤 {i + 1}: {FirstLine(goal)}");
            builder.AppendLine($"- 结束原因: {result?.StopReason ?? "无结果"}");
            builder.AppendLine($"- 耗时: {result?.TotalElapsedMs ?? 0}ms");
            builder.AppendLine($"- 输出: {result?.Answer ?? string.Empty}");
            if (result is not null)
            {
                builder.AppendLine($"- 工具调用: {result.Steps.Sum(s => s.ToolCalls.Count)} 次，失败 {result.Steps.Sum(s => s.ToolErrors.Count)} 次");
            }
            if (!string.IsNullOrEmpty(result?.Error))
            {
                builder.AppendLine($"- 错误: {result.Error}");
            }
            builder.AppendLine();
        }

        ResultText = builder.ToString();
    }

    private static string FirstLine(string value)
    {
        var idx = value.IndexOf('\n');
        return idx < 0 ? value : value[..idx];
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _runCts?.Cancel();
            _runCts?.Dispose();
            _runCts = null;
            ExecutionLog.Clear();
            HistoryLog.Clear();
        }

        base.Dispose(disposing);
    }
}
