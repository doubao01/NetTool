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
        RefreshGraphCommand = new RelayCommand(_ => RefreshHealth());
        ShowStatisticsCommand = new RelayCommand(_ => ShowStatistics());
        ApplyPatternCommand = new RelayCommand(_ => ApplyPattern());
        ViewPatternDetailsCommand = new RelayCommand(_ => ViewPatternDetails());

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

    private void ShowStatistics()
    {
        _logger.Info("显示统计信息 - 待实现对话框");
    }

    private void ApplyPattern()
    {
        _logger.Info("应用模式 - 待实现对话框");
    }

    private void ViewPatternDetails()
    {
        _logger.Info("查看模式详情 - 待实现对话框");
    }
}
