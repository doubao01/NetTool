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

    public RelayCommand CreateWorkflowCommand { get; }
    public RelayCommand StartWorkflowCommand { get; }
    public RelayCommand CancelWorkflowCommand { get; }
    public RelayCommand IncreaseMaxSubAgentsCommand { get; }
    public RelayCommand DecreaseMaxSubAgentsCommand { get; }

    /// <summary>最大子智能体数的最小值</summary>
    private const int MIN_SUB_AGENTS = 1;

    /// <summary>最大子智能体数的最大值</summary>
    private const int MAX_SUB_AGENTS = 20;

    public AgentOrchestrationViewModel(
        ITaskWindowManager taskManager,
        IAgentLoop agentLoop,
        ILoggerService logger)
    {
        _taskManager = taskManager;
        _agentLoop = agentLoop;
        _logger = logger;

        CreateWorkflowCommand = new RelayCommand(_ => CreateWorkflow());
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
        var progress = new Progress<AgentStep>(step =>
        {
            ExecutionLog.Add(FormatStep(step));
        });

        StatusText = $"运行中：{WorkflowName}（{steps.Count} 步，并发 {config.MaxSubAgents}）";
        _logger.Info($"启动编排工作流: {WorkflowName}");

        try
        {
            var goals = BuildGoals(steps);
            List<AgentLoopResult> results;

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

            var completed = results.Count(r => r.Completed);
            StatusText = $"完成：{completed}/{results.Count} 步成功收敛";
            _logger.Info($"编排工作流结束: {WorkflowName}，{completed}/{results.Count} 成功");
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消";
            _logger.Info($"编排工作流已取消: {WorkflowName}");
        }
        catch (Exception ex)
        {
            StatusText = $"失败：{ex.Message}";
            _logger.Error($"编排工作流失败: {WorkflowName}", ex);
        }
        finally
        {
            IsRunning = false;
            _runCts?.Dispose();
            _runCts = null;
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
        var tools = step.ToolCalls.Count > 0 ? string.Join(", ", step.ToolCalls) : "无";
        var mark = step.IsFinal ? " [完成]" : string.Empty;
        return $"#{step.Index} ({step.ElapsedMs}ms) 工具: {tools}{mark}";
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
        }

        base.Dispose(disposing);
    }
}
