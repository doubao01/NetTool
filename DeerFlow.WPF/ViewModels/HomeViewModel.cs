using System.Collections.ObjectModel;
using DeerFlow.WPF.Core;
using DeerFlow.WPF.Models;
using DeerFlow.WPF.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DeerFlow.WPF.ViewModels;

/// <summary>
/// 首页 ViewModel，展示应用概览和快速操作入口
/// 支持系统健康监控（模式库/经验记忆/实验进度）
/// </summary>
public class HomeViewModel : ViewModelBase
{
    private readonly ITaskWindowManager _taskManager;
    private readonly ILoggerService _logger;
    private readonly Action<string>? _navigateTo;
    private readonly Action? _createTask;
    private readonly ISelfReflectionService? _reflectionService;
    private readonly IPatternMiner? _patternMiner;
    private readonly IAutoEvolver? _autoEvolver;
    private readonly IExperienceMemoryStore? _experienceStore;

    private string _welcomeMessage = "欢迎使用 DeerFlow.WPF";
    public string WelcomeMessage
    {
        get => _welcomeMessage;
        set => SetProperty(ref _welcomeMessage, value);
    }

    private string _quickStatus = "系统就绪";
    public string QuickStatus
    {
        get => _quickStatus;
        set => SetProperty(ref _quickStatus, value);
    }

    // 健康监控属性
    private int _patternCount;
    public int PatternCount
    {
        get => _patternCount;
        set => SetProperty(ref _patternCount, value);
    }

    private int _experienceCount;
    public int ExperienceCount
    {
        get => _experienceCount;
        set => SetProperty(ref _experienceCount, value);
    }

    private int _activeExperimentCount;
    public int ActiveExperimentCount
    {
        get => _activeExperimentCount;
        set => SetProperty(ref _activeExperimentCount, value);
    }

    private ObservableCollection<SelfReflectionItem> _recentReflections = new();
    public ObservableCollection<SelfReflectionItem> RecentReflections
    {
        get => _recentReflections;
        set => SetProperty(ref _recentReflections, value);
    }

    public ObservableCollection<TaskModel> RecentTasks { get; } = new();

    /// <summary>当前展示的模式列表（供图谱控件使用）</summary>
    public ObservableCollection<PatternItem> Patterns { get; } = new();

    /// <summary>当前选中的模式</summary>
    private PatternItem? _selectedPattern;
    public PatternItem? SelectedPattern
    {
        get => _selectedPattern;
        set
        {
            if (SetProperty(ref _selectedPattern, value))
            {
                ApplyPatternCommand.RaiseCanExecuteChanged();
                ViewPatternDetailsCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>统计信息文本，空串表示无内容可展示</summary>
    private string _statisticsText = string.Empty;
    public string StatisticsText
    {
        get => _statisticsText;
        set
        {
            if (SetProperty(ref _statisticsText, value))
            {
                OnPropertyChanged(nameof(HasPatternFeedback));
            }
        }
    }

    /// <summary>操作提示文本</summary>
    private string _actionStatus = string.Empty;
    public string ActionStatus
    {
        get => _actionStatus;
        set
        {
            if (SetProperty(ref _actionStatus, value))
            {
                OnPropertyChanged(nameof(HasPatternFeedback));
            }
        }
    }

    /// <summary>是否存在模式统计或操作反馈可展示</summary>
    public bool HasPatternFeedback =>
        !string.IsNullOrWhiteSpace(StatisticsText) || !string.IsNullOrWhiteSpace(ActionStatus);

    public RelayCommand NewChatCommand { get; }
    public RelayCommand NewTaskCommand { get; }
    public RelayCommand OpenSkillsCommand { get; }
    public RelayCommand RefreshHealthCommand { get; }
    public RelayCommand RefreshGraphCommand { get; }
    public RelayCommand ShowStatisticsCommand { get; }
    public RelayCommand ApplyPatternCommand { get; }
    public RelayCommand ViewPatternDetailsCommand { get; }

    public HomeViewModel(
        ITaskWindowManager taskManager,
        ILoggerService logger,
        Action<string>? navigateTo = null,
        Action? createTask = null)
    {
        _taskManager = taskManager;
        _logger = logger;
        _navigateTo = navigateTo;
        _createTask = createTask;

        // 解析自我迭代系统服务
        _reflectionService = App.Services.GetService<ISelfReflectionService>();
        _patternMiner = App.Services.GetService<IPatternMiner>();
        _autoEvolver = App.Services.GetService<IAutoEvolver>();
        _experienceStore = App.Services.GetService<IExperienceMemoryStore>();

        NewChatCommand = new RelayCommand(_ => _navigateTo?.Invoke("Chat"));
        NewTaskCommand = new RelayCommand(_ => _createTask?.Invoke());
        OpenSkillsCommand = new RelayCommand(_ => _navigateTo?.Invoke("Skills"));
        RefreshHealthCommand = new RelayCommand(_ => RefreshHealth());
        RefreshGraphCommand = new RelayCommand(_ => RefreshGraph());
        ShowStatisticsCommand = new RelayCommand(_ => ShowStatistics());
        ApplyPatternCommand = new RelayCommand(_ => ApplyPattern(), _ => SelectedPattern is not null);
        ViewPatternDetailsCommand = new RelayCommand(_ => ViewPatternDetails(), _ => SelectedPattern is not null);

        RefreshRecentTasks();
        RefreshHealth();
    }

    private void RefreshRecentTasks()
    {
        RecentTasks.Clear();
        foreach (var task in _taskManager.GetActiveTasks().Take(5))
        {
            RecentTasks.Add(task);
        }
        QuickStatus = $"活跃任务：{RecentTasks.Count} 个";
    }

    private void RefreshHealth()
    {
        try
        {
            if (_patternMiner is not null)
            {
                var patternStats = _patternMiner.GetPatternStatistics();
                PatternCount = (int)(patternStats.ContainsKey("TotalPatterns") 
                    ? patternStats["TotalPatterns"] : 0);
            }

            Patterns.Clear();
            if (_patternMiner is not null)
            {
                foreach (var pattern in _patternMiner.GetPatterns(limit: 12))
                {
                    Patterns.Add(pattern);
                }
            }

            if (_experienceStore is not null)
            {
                var experienceStats = _experienceStore.GetStatistics();
                ExperienceCount = (int)(experienceStats.ContainsKey("TotalExperiences")
                    ? experienceStats["TotalExperiences"] : 0);
            }

            if (_autoEvolver is not null)
            {
                var experimentStats = _autoEvolver.GetExperimentStatistics();
                ActiveExperimentCount = (int)(experimentStats.ContainsKey("ExperimentingCount")
                    ? experimentStats["ExperimentingCount"] : 0);
            }

            if (_reflectionService is not null)
            {
                var reflections = _reflectionService.GetReflections(limit: 5);
                RecentReflections.Clear();
                foreach (var reflection in reflections)
                {
                    RecentReflections.Add(reflection);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"刷新健康状态失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 重新加载模式图谱数据
    /// </summary>
    private void RefreshGraph()
    {
        RefreshHealth();
        ActionStatus = Patterns.Count == 0
            ? "暂无模式数据，请先完成若干任务以积累经验"
            : $"已加载 {Patterns.Count} 个模式";
    }

    private void ShowStatistics()
    {
        if (_patternMiner is null)
        {
            StatisticsText = "模式挖掘服务未启用";
            return;
        }

        try
        {
            var stats = _patternMiner.GetPatternStatistics();
            var total = stats.TryGetValue("TotalPatterns", out var t) ? t : 0;
            var verified = stats.TryGetValue("VerifiedPatterns", out var v) ? v : 0;
            var draft = stats.TryGetValue("DraftPatterns", out var d) ? d : 0;
            var avgScore = stats.TryGetValue("AverageScore", out var a) && a is double avg
                ? avg.ToString("P1")
                : "0%";
            var totalUsage = stats.TryGetValue("TotalUsageCount", out var u) ? u : 0;

            StatisticsText =
                $"模式总数 {total}，已验证 {verified}，草稿 {draft}；" +
                $"平均评分 {avgScore}，累计使用 {totalUsage} 次。";
        }
        catch (Exception ex)
        {
            StatisticsText = $"统计失败：{ex.Message}";
            _logger.Warn($"显示模式统计失败：{ex.Message}");
        }
    }

    private void ApplyPattern()
    {
        if (SelectedPattern is null)
        {
            ActionStatus = "请先在图谱中选择一个模式";
            return;
        }

        ActionStatus = $"已选用模式「{SelectedPattern.Name}」，将在后续对话中优先参考";
        _logger.Info($"应用模式：{SelectedPattern.Name}");
    }

    private void ViewPatternDetails()
    {
        if (SelectedPattern is null)
        {
            ActionStatus = "请先在图谱中选择一个模式";
            return;
        }

        var pattern = SelectedPattern;
        var steps = pattern.Steps.Count > 0 ? string.Join(" → ", pattern.Steps) : "（无步骤记录）";
        ActionStatus =
            $"「{pattern.Name}」类别 {pattern.Category}，评分 {pattern.Score:P1}，" +
            $"使用 {pattern.UsageCount} 次；步骤：{steps}";
    }
}
